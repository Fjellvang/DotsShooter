// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Application;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Cloud.RuntimeOptions
{
    /// <summary>
    /// Set of sources (files, environment variables, command line) for a given <see cref="RuntimeOptionsRegistry"/>.
    /// Also creates a per-source <see cref="IConfigurationRoot"/> for the purposes of tracking which members were
    /// defined in each source. This is quite a hack and there's the sources and values may temporarily mismatch
    /// when options are updated.
    /// </summary>
    public class RuntimeOptionsSourceSet
    {
        public struct SourceDeclaration
        {
            public readonly string                          Name;
            public readonly string                          Description;
            public readonly bool                            StrictBindingChecks;
            public readonly Action<ConfigurationBuilder>    RegisterFunc;

            public SourceDeclaration(string name, string description, bool strictBindingChecks, Action<ConfigurationBuilder> registerFunc)
            {
                Name = name;
                Description = description;
                StrictBindingChecks = strictBindingChecks;
                RegisterFunc = registerFunc;
            }
        }

        public class Source
        {
            public readonly string              Name;
            public readonly string              Description;
            public readonly bool                StrictBindingChecks;
            public readonly IConfigurationRoot  ConfigRoot;

            public bool TolerateUnknownFields => !StrictBindingChecks;
            public bool TolerateEmptyObjectValues => !StrictBindingChecks;

            public Source(string name, string description, bool strictBindingChecks, IConfigurationRoot configRoot)
            {
                Name                    = name;
                Description             = description;
                StrictBindingChecks     = strictBindingChecks;
                ConfigRoot              = configRoot;
            }
        }

        ConfigurationBuilder    _globalBuilder  = new ConfigurationBuilder();
        List<Source>            _sources        = new List<Source>();

        public IEnumerable<Source> Sources => _sources;

        public RuntimeOptionsSourceSet()
        {
        }

        public Source AddSource(SourceDeclaration declaration)
        {
            // Register to combined builder
            declaration.RegisterFunc(_globalBuilder);

            // Create individual builder
            ConfigurationBuilder builder = new ConfigurationBuilder();
            declaration.RegisterFunc(builder);
            IConfigurationRoot configRoot = builder.Build();
            Source source = new Source(declaration.Name, declaration.Description, declaration.StrictBindingChecks, configRoot);
            _sources.Add(source);
            return source;
        }

        public IConfigurationRoot BuildGlobalConfig() =>
            _globalBuilder.Build();
    }

    public class RuntimeEnvironmentInfo
    {
        public static RuntimeEnvironmentInfo Instance { get; set; }
        public bool              IsRunningInContainer { get; }
        public string            ApplicationName      { get; }
        public EnvironmentFamily EnvironmentFamily    { get; }

        public RuntimeEnvironmentInfo(string applicationName)
        {
            // Store application name
            if (string.IsNullOrEmpty(applicationName))
                throw new ArgumentException("Must provide valid application name", nameof(applicationName));
            ApplicationName = applicationName;
            // Check whether running inside a container
            IsRunningInContainer = bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out bool inContainer) && inContainer;

            // Figure out current environment family (for environment-dependent default values)
            string envFamilyStr = Environment.GetEnvironmentVariable("METAPLAY_ENVIRONMENT_FAMILY");
            if (IsRunningInContainer && string.IsNullOrEmpty(envFamilyStr))
                throw new InvalidOperationException("The METAPLAY_ENVIRONMENT_FAMILY value must be defined when running in a container! You probably need to upgrade to the latest Helm chart.");
            EnvironmentFamily = string.IsNullOrEmpty(envFamilyStr) ? EnvironmentFamily.Local : EnumUtil.Parse<EnvironmentFamily>(envFamilyStr);
        }
    }

    /// <summary>
    /// Registry for registering and accessing all the options classes. Discovers the classes via
    /// reflection, manages the updating due to changes in sources, and allows querying the current
    /// versions of each.
    /// </summary>
    public class RuntimeOptionsRegistry : IHostedService, IDisposable
    {
        public static RuntimeOptionsRegistry Instance => MetaplayServices.Get<RuntimeOptionsRegistry>();

        static IMetaLogger _log = MetaLogger.ForContext<RuntimeOptionsRegistry>();

        const string DefaultOptionsPaths = "Config/Options.base.yaml;Config/Options.local.yaml";

        const string DefaultValue = "Default";
        const string ComputedValue = "Computed";

        class Entry
        {
            public readonly Type                        Type;
            public readonly RuntimeOptionsAttribute     Attribute;

            // Dynamic members, accessed under lock
            public string                               ContentHash     = null; // Hash of the current content, for filtering spurious updates
            public RuntimeOptionsBase                   Options         = null; // Current Options values
            public MetaDictionary<string, string>    ValueSources    = null; // Source for each option value
            public MetaDictionary<string, string>    Descriptions    = null; // Sparsely populated dictionary of descriptions for each option value

            // Helpers
            public string SectionName        => Attribute.SectionName;
            public string SectionDescription => Attribute.SectionDescription;

            public Entry(Type type, RuntimeOptionsAttribute attribute)
            {
                Type        = type ?? throw new ArgumentNullException(nameof(type));
                Attribute   = attribute ?? throw new ArgumentNullException(nameof(attribute));
            }

            public override string ToString() => $"RuntimeOptions.Entry(section={SectionName})";

            public class OptionValue
            {
                public readonly string      Name;
                public readonly string      Description;
                public readonly string      CommandLineAlias;
                public readonly string[]    EnvironmentVariables;
                public readonly bool        IsComputedValue;

                public OptionValue(string name, string description, string commandLineAlias, string[] environmentVariables, bool isComputedValue)
                {
                    Name = name;
                    Description = description;

                    if (commandLineAlias != null)
                    {
                        if (commandLineAlias.Length < 2 || commandLineAlias[0] != '-' || commandLineAlias[1] == '-')
                            throw new InvalidOperationException($"invalid command line format: {commandLineAlias}");
                        CommandLineAlias = commandLineAlias.Substring(1);
                    }
                    else
                        CommandLineAlias = null;

                    EnvironmentVariables = environmentVariables;
                    IsComputedValue = isComputedValue;
                }
            }

            public List<OptionValue> GetValues()
            {
                List<OptionValue> values = new List<OptionValue>();
                foreach (PropertyInfo prop in Type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    string description;
                    if (prop.GetCustomAttribute<MetaDescriptionAttribute>() is MetaDescriptionAttribute descriptionAttr)
                        description = descriptionAttr.Description;
                    else
                        description = null;

                    string alias;
                    if (prop.GetCustomAttribute<CommandLineAliasAttribute>() is CommandLineAliasAttribute aliasAttr)
                        alias = aliasAttr.Alias;
                    else
                        alias = null;

                    string[] environmentVariables = prop.GetCustomAttributes<EnvironmentVariableAttribute>().Select(attr => attr.Alias).ToArray();

                    bool isComputed;
                    if (prop.GetSetMethodOnDeclaringType() == null)
                        isComputed = true;
                    else if (prop.HasCustomAttribute<ComputedValueAttribute>())
                        isComputed = true;
                    else
                        isComputed = false;
                    values.Add(new OptionValue(prop.Name, description, alias, environmentVariables, isComputed));
                }
                return values;
            }
        }

        RuntimeOptionsSourceSet _sourceSet = new RuntimeOptionsSourceSet();
        object                  _lock      = new object();
        List<Entry>             _entries;
        RuntimeEnvironmentInfo  _envInfo;
        AsyncManualResetEvent   _updatedEvent   = new AsyncManualResetEvent();
        IDisposable             _changeTokenRegistration;
        Task                    _updateTask;
        public MetaTime         _lastUpdateTime         { get; private set; }

        public RuntimeOptionsRegistry(RuntimeEnvironmentInfo envInfo, string[] cmdLineArgs)
        {
            _envInfo = envInfo;

            if (RuntimeEnvironmentInfo.Instance != null)
                throw new InvalidOperationException("RuntimeOptionsRegistry currently doesn't support multiple instances");
            RuntimeEnvironmentInfo.Instance = envInfo;

            // Find all classes deriving from RuntimeOptionsBase (or use the passed in list of types)
            _entries = ResolveEntriesFromTypes(TypeScanner.GetAllTypes());

            // Parse command line and environment
            CommandLineRuntimeOptions commandLineOptions = ParseCommandLineOrDie(cmdLineArgs);
            EnvironmentRuntimeOptionsSource environmentOptions = ParseEnvironmentValues();

            _log.Information("Initializing RuntimeOptions with IsRunningInContainer={IsRunningInContainer}, ApplicationName={ApplicationName}, EnvironmentFamily={EnvironmentFamily}..", envInfo.IsRunningInContainer, envInfo.ApplicationName, envInfo.EnvironmentFamily);

            // Configure options sources to use
            foreach (RuntimeOptionsSourceSet.SourceDeclaration declaration in GetOptionDeclarations(commandLineOptions, environmentOptions))
            {
                _log.Information("Reading options {0} ({1})", declaration.Name, declaration.Description);
                try
                {
                    _sourceSet.AddSource(declaration);
                }
                catch (Exception ex)
                {
                    _log.Error("Fatal parse error when parsing {0}: {1}", declaration.Name, ex);
                    Application.Application.ForceTerminate(SysexitsExUsage, "Fatal initial options parse error");
                    return; // unreachable
                }
            }
        }

        public void Dispose()
        {
            RuntimeEnvironmentInfo.Instance = null;
        }

        static List<Entry> ResolveEntriesFromTypes(IEnumerable<Type> candidateTypes)
        {
            List<Entry> entries = new List<Entry>();

            // Resolve all classes deriving from RuntimeOptions
            foreach (Type type in candidateTypes)
            {
                if (type.IsClass && !type.IsAbstract && type.IsDerivedFrom<RuntimeOptionsBase>())
                {
                    // Store the entry
                    RuntimeOptionsAttribute attrib = type.GetCustomAttribute<RuntimeOptionsAttribute>();
                    if (attrib == null)
                        throw new InvalidOperationException($"Invalid RuntimeOption {type.ToNamespaceQualifiedTypeString()}. Type must be annotated with a RuntimeOptionsAttribute.");

                    // Check the type is well-behaving
                    RuntimeOptionsBinder.CheckRuntimeOptionsIsParseable(type);

                    entries.Add(new Entry(type, attrib));
                }
                else
                {
                    RuntimeOptionsAttribute strayAttrib = type.GetCustomAttribute<RuntimeOptionsAttribute>();
                    if (strayAttrib != null)
                        throw new InvalidOperationException($"Invalid RuntimeOption attribute on {type.ToNamespaceQualifiedTypeString()}. Runtime options must be a concrete class that inherits RuntimeOptionsBase.");
                }
            }

            return entries;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            _log.Information("Reading initial values for runtime options");

            // Trim the list of entries based on feature conditions
            _entries = _entries.Where(x => x.Type.IsMetaFeatureEnabled()).ToList();

            // Update loop (triggered on each change to sources). Using a separate background "thread" with
            // signaling to ensure that only a single update action can be in-flight at a time.
            IHostApplicationLifetime lifetime = MetaplayServices.Get<IHostApplicationLifetime>();
            _updateTask = Task.Run(async () =>
            {
                while (true)
                {
                    // Wait for next update event
                    await _updatedEvent.WaitAsync().ConfigureAwait(false);
                    _updatedEvent.Reset();

                    if (lifetime.ApplicationStopping.IsCancellationRequested)
                        break;

                    // Perform the update of Options classes
                    try
                    {
                        await UpdateOptions(isInitial: false).ConfigureAwait(false);
                        _lastUpdateTime = MetaTime.Now;
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Failed to update RuntimeOptions:\n{Exception}", ex);
                    }
                }
            });

            // Register handler for configuration files changes to signal the updater above
            IConfigurationRoot configRoot = _sourceSet.BuildGlobalConfig();
            _changeTokenRegistration = ChangeToken.OnChange(
                () => configRoot.GetReloadToken(),
                () => _updatedEvent.Set());

            // Initialize options with initial values
            await UpdateOptions(isInitial: true).ConfigureAwait(false);

            if (_envInfo.EnvironmentFamily == EnvironmentFamily.Local)
            {
                if (GetCurrent<EnvironmentOptions>().EnableKeyboardInput)
                    _log.Information("Press '{Key}' key to print runtime options to console", ConsoleKey.O);
            }
            else
            {
                Print();
            }
        }

        public async Task StopAsync(CancellationToken ct)
        {
            _changeTokenRegistration.Dispose();
            _updatedEvent.Set();
            await _updateTask;
        }

        public static bool TryValidateOptionsFiles(string[] filePaths)
        {
            List<Entry> entries   = ResolveEntriesFromTypes(TypeScanner.GetAllTypes());
            bool        hasErrors = false;
            foreach (string path in filePaths)
            {
                string shortName = path.Split('/').Last();
                _log.Information("Validating options file {shortName}...", shortName);
                RuntimeOptionsSourceSet sourceSet = new RuntimeOptionsSourceSet();
                sourceSet.AddSource(new RuntimeOptionsSourceSet.SourceDeclaration(
                    name:                   shortName,
                    description:            $"From the file {path}",
                    strictBindingChecks:    true,
                    registerFunc:           builder =>
                    {
                        builder.SetBasePath(Directory.GetCurrentDirectory());
                        builder.AddYamlFile(path, optional: false, reloadOnChange: false);
                    }));
                RuntimeOptionsBinder.BindingResults results = BindConfigToOptions(entries, sourceSet, false);
                foreach (Exception ex in results.Errors)
                    _log.Error("{Error}", ex);
                foreach (RuntimeOptionsBinder.Warning warning in results.Warnings)
                    _log.Warning("{Warning}", warning.Message);
                // There should be no warnings in baked configs, so break the build also on warnings to notify developer
                if (results.Errors.Length > 0 || results.Warnings.Length > 0)
                    hasErrors = true;
            }
            return !hasErrors;
        }

        /// <summary>
        /// Update all the current options blocks from sources. Called at start (isInitial=true) and
        /// whenever any of the sources change (isInitial=false).
        ///
        /// Duplicate updates are skipped on per-block level by hash comparisons. The options are only
        /// updated once the call to <see cref="RuntimeOptionsBase.OnLoadedAsync()"/> has successfully
        /// completed. If the callback fails, it is retried a few times before giving up.
        /// </summary>
        /// <param name="isInitial"></param>
        /// <returns></returns>
        async Task UpdateOptions(bool isInitial)
        {
            // Bind values to a new set of runtime options
            RuntimeOptionsBinder.BindingResults results = BindConfigToOptions(_entries, _sourceSet, false);

            if (results.Errors.Length > 0)
            {
                // Binding errors mean the configuration is broken. If initial config, halt immediately. For update, tolerate
                // and try again when the underlying file/source is updated. When that happens, this method will get called again.
                // Warnings might be useful, so print them too.

                foreach (Exception ex in results.Errors)
                    _log.Error("{Error}", ex);
                foreach (RuntimeOptionsBinder.Warning warning in results.Warnings)
                    _log.Warning("{Warning}", warning.Message);

                _log.Error($"Could not parse application configuration.");

                if (isInitial)
                {
                    Application.Application.ForceTerminate(SysexitsExUsage, "Invalid options given");
                    return; // unreachable
                }

                // Update failed. Try again later.
                return;
            }

            // Handle all warnings. If warnings are marked as fatal or if the warning source is the command line, kill the
            // application but only on launch. Killing application when it's running would be unexpected behavior.

            foreach (RuntimeOptionsBinder.Warning warning in results.Warnings)
                _log.Warning("{Warning}", warning.Message);

            if (isInitial && results.Warnings.Any(warning => warning.Source.Name == "CLI"))
            {
                PrintCliUsageErrorAndExitNoReturn(results.Warnings.Where(warning => warning.Source.Name == "CLI").Select(warning => warning.Message).ToArray());
                return; // unreachable
            }
            if (isInitial && results.Warnings.Length > 0 && ((EnvironmentOptions)results.Sections[typeof(EnvironmentOptions)].Options).ExitOnUnknownOptions)
            {
                _log.Error($"Unknown options were found and Environment:{nameof(EnvironmentOptions.ExitOnUnknownOptions)} is true; exiting.");
                Application.Application.ForceTerminate(SysexitsExUsage, "Unknown options given");
                return; // unreachable
            }

            // Values are bound to the runtime options, but they are not yet loaded. Load entries that need updating

            List<Entry> invalidatedEntries = new List<Entry>();
            foreach (Entry entry in _entries)
            {
                RuntimeOptionsBinder.BindingResults.Section section = results.Sections[entry.Type];

                // Skip static on update
                if (entry.Attribute.IsStatic && !isInitial)
                    continue;

                // Skip if contents haven't changed
                if (!isInitial && section.ContentHash == entry.ContentHash)
                    continue;

                invalidatedEntries.Add(entry);
            }

            // Load entries.
            await Task.WhenAll(invalidatedEntries.Select(async entry =>
            {
                RuntimeOptionsBinder.BindingResults.Section section = results.Sections[entry.Type];

                // Resolve sources for options
                // \note Sources are only resolved for the top-level members of the Options block
                MetaDictionary<string, string> memberSources = new MetaDictionary<string, string>();
                foreach (Entry.OptionValue value in entry.GetValues())
                {
                    string srcName = DefaultValue;

                    if (value.IsComputedValue)
                    {
                        // Read-only properties are computed values (derived data)
                        srcName = ComputedValue;
                    }
                    else if (section.MappingsSources.TryGetValue($"{entry.SectionName}:{value.Name}", out RuntimeOptionsSourceSet.Source source))
                    {
                        srcName = source.Name;
                    }

                    memberSources.Add(value.Name, srcName);
                }

                // Resolve descriptions for options
                // \note The resulting list is sparsely populated. Properties without descriptions do not have an entry
                MetaDictionary<string, string> memberDescriptions = new MetaDictionary<string, string>();
                foreach (Entry.OptionValue value in entry.GetValues())
                {
                    if (!string.IsNullOrEmpty(value.Description))
                        memberDescriptions.Add(value.Name, value.Description);
                }

                // Invoke on-loaded handler
                const int NumTries = 5;
                for (int tryNdx = 0; tryNdx < NumTries; tryNdx++)
                {
                    try
                    {
                        // Invoke the handler
                        await section.Options.OnLoadedAsync(this).ConfigureAwait(false);

                        // Set as current & store hash
                        lock (_lock)
                        {
                            entry.Options = section.Options;
                            entry.ValueSources = memberSources;
                            entry.Descriptions = memberDescriptions;
                            entry.ContentHash = section.ContentHash;
                        }

                        // Exit loop
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (tryNdx < NumTries - 1)
                        {
                            _log.Warning("{OptionsName}.OnLoadedAsync() failed, retrying..\n{Exception}", entry.Type.Name, ex);

                            // Wait a bit before trying again
                            await Task.Delay(1_000);
                        }
                        else
                        {
                            // Print with the partially computed values
                            _log.Information("{OptionsName}.OnLoadedAsync() failed too many times. Contents:\n{Options}", entry.Type.Name, OptionToMultilineLogString(section.Options));

                            // On final try, re-throw
                            throw;
                        }
                    }
                }

                // Log contents of dynamically updated option blocks (after computed values were updated)
                if (!isInitial)
                    _log.Information("Updating\n{Options}", OptionToMultilineLogString(section.Options));
            }));
        }

        public IEnumerable<RuntimeOptionsBase> ReadStartupOptions(List<Type> optionTypes)
        {
            RuntimeOptionsBinder.BindingResults results = BindConfigToOptions(_entries.Where(x => optionTypes.Contains(x.Type)).ToList(), _sourceSet, true);
            if (results.Errors.Length > 0)
            {
                foreach (Exception ex in results.Errors)
                    _log.Error("{Error}", ex);
                throw new InvalidOperationException("Reading startup options failed");
            }
            return optionTypes.Select(x => results.Sections[x].Options);

        }

        static RuntimeOptionsBinder.BindingResults BindConfigToOptions(List<Entry> entries, RuntimeOptionsSourceSet sourceSet, bool readingStartupOptions)
        {
            List<RuntimeOptionsBinder.RuntimeOptionDefinition> definitions = new List<RuntimeOptionsBinder.RuntimeOptionDefinition>();
            foreach (Entry entry in entries)
                definitions.Add(new RuntimeOptionsBinder.RuntimeOptionDefinition(entry.Type, entry.SectionName));
            return RuntimeOptionsBinder.BindToRuntimeOptions(definitions, sourceSet, readingStartupOptions);
        }

        public void Print()
        {
            lock (_lock)
            {
                MetaDictionary<string, RuntimeOptionsBase> optsDict =
                    _entries.ToMetaDictionary(entry => entry.SectionName, entry => entry.Options);

                _log.Information("Runtime options:\n{Options}", OptionToMultilineLogString(optsDict));
            }
        }

        static string OptionToMultilineLogString(object options)
        {
            // Add one space in front of each line. This prevents log lines to be treated as separate elements.
            string baseString = PrettyPrint.Verbose(options).ToString();
            return " " + baseString.Replace("\n", "\n ").TrimEnd();
        }

        /// <summary>
        /// <para>
        /// Get the current version of a given options block. Every time this method is called,
        /// the underlying value may have changed, so only call this once for each logical block
        /// and keep copy of the returned <paramref name="optionsType"/> reference.
        /// </para>
        /// <para>
        /// Can return <c>null</c> if given options type is not found.
        /// </para>
        /// </summary>
        public RuntimeOptionsBase TryGetCurrent(System.Type optionsType)
        {
            lock (_lock)
            {
                return _entries.Single(e => e.Type == optionsType)?.Options;
            }
        }

        /// <summary>
        /// Get the current version of a given options block. Every time this method is called,
        /// the underlying value may have changed, so only call this once for each logical block
        /// and keep copy of the returned <typeparamref name="TOptions"/> reference.
        /// </summary>
        /// <typeparam name="TOptions"></typeparam>
        /// <returns></returns>
        public TOptions GetCurrent<TOptions>() where TOptions : RuntimeOptionsBase
        {
            lock (_lock)
            {
                return (TOptions)_entries.Single(e => e.Type == typeof(TOptions)).Options;
            }
        }

        public (TOptions1, TOptions2) GetCurrent<TOptions1, TOptions2>() where TOptions1 : RuntimeOptionsBase where TOptions2 : RuntimeOptionsBase
        {
            lock (_lock)
            {
                TOptions1 opts1 = (TOptions1)_entries.Single(e => e.Type == typeof(TOptions1)).Options;
                TOptions2 opts2 = (TOptions2)_entries.Single(e => e.Type == typeof(TOptions2)).Options;
                return (opts1, opts2);
            }
        }

        public (TOptions1, TOptions2, TOptions3) GetCurrent<TOptions1, TOptions2, TOptions3>() where TOptions1 : RuntimeOptionsBase where TOptions2 : RuntimeOptionsBase where TOptions3 : RuntimeOptionsBase
        {
            lock (_lock)
            {
                TOptions1 opts1 = (TOptions1)_entries.Single(e => e.Type == typeof(TOptions1)).Options;
                TOptions2 opts2 = (TOptions2)_entries.Single(e => e.Type == typeof(TOptions2)).Options;
                TOptions3 opts3 = (TOptions3)_entries.Single(e => e.Type == typeof(TOptions3)).Options;
                return (opts1, opts2, opts3);
            }
        }

        public (TOptions1, TOptions2, TOptions3, TOptions4) GetCurrent<TOptions1, TOptions2, TOptions3, TOptions4>() where TOptions1 : RuntimeOptionsBase where TOptions2 : RuntimeOptionsBase where TOptions3 : RuntimeOptionsBase where TOptions4 : RuntimeOptionsBase
        {
            lock (_lock)
            {
                TOptions1 opts1 = (TOptions1)_entries.Single(e => e.Type == typeof(TOptions1)).Options;
                TOptions2 opts2 = (TOptions2)_entries.Single(e => e.Type == typeof(TOptions2)).Options;
                TOptions3 opts3 = (TOptions3)_entries.Single(e => e.Type == typeof(TOptions3)).Options;
                TOptions4 opts4 = (TOptions4)_entries.Single(e => e.Type == typeof(TOptions4)).Options;
                return (opts1, opts2, opts3, opts4);
            }
        }

        public (TOptions1, TOptions2, TOptions3, TOptions4, TOptions5) GetCurrent<TOptions1, TOptions2, TOptions3, TOptions4, TOptions5>() where TOptions1 : RuntimeOptionsBase where TOptions2 : RuntimeOptionsBase where TOptions3 : RuntimeOptionsBase where TOptions4 : RuntimeOptionsBase where TOptions5 : RuntimeOptionsBase
        {
            lock (_lock)
            {
                TOptions1 opts1 = (TOptions1)_entries.Single(e => e.Type == typeof(TOptions1)).Options;
                TOptions2 opts2 = (TOptions2)_entries.Single(e => e.Type == typeof(TOptions2)).Options;
                TOptions3 opts3 = (TOptions3)_entries.Single(e => e.Type == typeof(TOptions3)).Options;
                TOptions4 opts4 = (TOptions4)_entries.Single(e => e.Type == typeof(TOptions4)).Options;
                TOptions5 opts5 = (TOptions5)_entries.Single(e => e.Type == typeof(TOptions5)).Options;
                return (opts1, opts2, opts3, opts4, opts5);
            }
        }

        public (TOptions1, TOptions2, TOptions3, TOptions4, TOptions5, TOptions6) GetCurrent<TOptions1, TOptions2, TOptions3, TOptions4, TOptions5, TOptions6>() where TOptions1 : RuntimeOptionsBase where TOptions2 : RuntimeOptionsBase where TOptions3 : RuntimeOptionsBase where TOptions4 : RuntimeOptionsBase where TOptions5 : RuntimeOptionsBase where TOptions6 : RuntimeOptionsBase
        {
            lock (_lock)
            {
                TOptions1 opts1 = (TOptions1)_entries.Single(e => e.Type == typeof(TOptions1)).Options;
                TOptions2 opts2 = (TOptions2)_entries.Single(e => e.Type == typeof(TOptions2)).Options;
                TOptions3 opts3 = (TOptions3)_entries.Single(e => e.Type == typeof(TOptions3)).Options;
                TOptions4 opts4 = (TOptions4)_entries.Single(e => e.Type == typeof(TOptions4)).Options;
                TOptions5 opts5 = (TOptions5)_entries.Single(e => e.Type == typeof(TOptions5)).Options;
                TOptions6 opts6 = (TOptions6)_entries.Single(e => e.Type == typeof(TOptions6)).Options;
                return (opts1, opts2, opts3, opts4, opts5, opts6);
            }
        }

        public (TOptions1, TOptions2, TOptions3, TOptions4, TOptions5, TOptions6, TOptions7) GetCurrent<TOptions1, TOptions2, TOptions3, TOptions4, TOptions5, TOptions6, TOptions7>() where TOptions1 : RuntimeOptionsBase where TOptions2 : RuntimeOptionsBase where TOptions3 : RuntimeOptionsBase where TOptions4 : RuntimeOptionsBase where TOptions5 : RuntimeOptionsBase where TOptions6 : RuntimeOptionsBase where TOptions7 : RuntimeOptionsBase
        {
            lock (_lock)
            {
                TOptions1 opts1 = (TOptions1)_entries.Single(e => e.Type == typeof(TOptions1)).Options;
                TOptions2 opts2 = (TOptions2)_entries.Single(e => e.Type == typeof(TOptions2)).Options;
                TOptions3 opts3 = (TOptions3)_entries.Single(e => e.Type == typeof(TOptions3)).Options;
                TOptions4 opts4 = (TOptions4)_entries.Single(e => e.Type == typeof(TOptions4)).Options;
                TOptions5 opts5 = (TOptions5)_entries.Single(e => e.Type == typeof(TOptions5)).Options;
                TOptions6 opts6 = (TOptions6)_entries.Single(e => e.Type == typeof(TOptions6)).Options;
                TOptions7 opts7 = (TOptions7)_entries.Single(e => e.Type == typeof(TOptions7)).Options;
                return (opts1, opts2, opts3, opts4, opts5, opts6, opts7);
            }
        }

        /// <summary>
        /// Helper class for returning more palatable versions of the options to dashboard.
        /// </summary>
        public class OptionsWithSource
        {
            public readonly string                              Name;
            public readonly string                              Description;
            public readonly bool                                IsStatic;
            public readonly RuntimeOptionsBase                  Values;
            public readonly MetaDictionary<string, string>   Sources;
            public readonly MetaDictionary<string, string>   Descriptions;

            public OptionsWithSource(string name, string description, bool isStatic, RuntimeOptionsBase values, MetaDictionary<string, string> sources, MetaDictionary<string, string> descriptions)
            {
                Name         = name;
                Description  = description;
                IsStatic     = isStatic;
                Values       = values;
                Sources      = sources;
                Descriptions = descriptions;
            }
        }

        /// <summary>
        /// Getter for returning all the registered sources to the dashboard.
        /// </summary>
        /// <returns></returns>
        public object[] GetAllSources()
        {
            return
                new object[]
                {
                    new { Name = DefaultValue, Description = "Default value, defined in Game Server code" },
                    new { Name = ComputedValue, Description = "Computed from other values" }
                }
                .Concat(_sourceSet.Sources.Select(src => new { Name = src.Name, Description = src.Description }))
                .ToArray();
        }

        /// <summary>
        /// Getter for returning a single options block to the dashboard.
        /// </summary>
        /// <returns></returns>
        public OptionsWithSource GetOptions<TOptions>() where TOptions : RuntimeOptionsBase
        {
            lock (_lock)
            {
                Entry entry = _entries.Single(entry => entry.Type == typeof(TOptions));
                return new OptionsWithSource(entry.SectionName, entry.SectionDescription, entry.Attribute.IsStatic, entry.Options, entry.ValueSources, entry.Descriptions);
            }
        }

        /// <summary>
        /// Getter for returning the options to the dashboard.
        /// </summary>
        /// <returns></returns>
        public OptionsWithSource[] GetAllOptions()
        {
            lock (_lock)
            {
                return _entries.Select(entry => new OptionsWithSource(entry.SectionName, entry.SectionDescription, entry.Attribute.IsStatic, entry.Options, entry.ValueSources, entry.Descriptions)).ToArray();
            }
        }

        /// <summary>
        /// Returns declarations of all option sources.
        /// </summary>
        static List<RuntimeOptionsSourceSet.SourceDeclaration> GetOptionDeclarations(CommandLineRuntimeOptions commandLineOptions, EnvironmentRuntimeOptionsSource environmentOptions)
        {
            List<RuntimeOptionsSourceSet.SourceDeclaration> declarations = new List<RuntimeOptionsSourceSet.SourceDeclaration>();

            void AddPath(string path, string sourceDescription, bool strictBindingChecks)
            {
                if (path.StartsWith("env-yaml-base64:", StringComparison.Ordinal))
                {
                    // Indirection into another environment variable. In the environment value, the value is a base64-encoded literal
                    string environmentVariableName = path.Substring("env-yaml-base64:".Length);
                    string b64Encoded = Environment.GetEnvironmentVariable(environmentVariableName);

                    // Must exist but may be empty
                    if (b64Encoded == null)
                        throw new InvalidOperationException($"Invalid RuntimeOption path in {sourceDescription}. Environment variable {environmentVariableName} is not defined");
                    if (string.IsNullOrWhiteSpace(b64Encoded))
                        return;

                    string content;
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(b64Encoded);
                        content = Encoding.UTF8.GetString(bytes);
                    }
                    catch
                    {
                        throw new InvalidOperationException($"Invalid RuntimeOption path in {sourceDescription}. Environment variable '{environmentVariableName}' does not contain valid base64 data. Got '{b64Encoded}'");
                    }

                    declarations.Add(new RuntimeOptionsSourceSet.SourceDeclaration(
                        name:                   environmentVariableName,
                        description:            $"Environment variable {environmentVariableName}, via {sourceDescription}",
                        strictBindingChecks:    strictBindingChecks,
                        registerFunc:           builder =>
                                                {
                                                    builder.AddYaml(content);
                                                }));
                }
                else if (!path.Contains(':'))
                {
                    // Normal file
                    switch (Path.GetExtension(path))
                    {
                        case ".yaml":
                            string shortPath = path.Split('/').Last();
                            declarations.Add(new RuntimeOptionsSourceSet.SourceDeclaration(
                                name:                   shortPath,
                                description:            $"From the file {path}, via {sourceDescription}",
                                strictBindingChecks:    strictBindingChecks,
                                registerFunc:           builder =>
                                                        {
                                                            builder.SetBasePath(Directory.GetCurrentDirectory());
                                                            builder.AddYamlFile(path, optional: false, reloadOnChange: true);
                                                        }));
                            break;

                        default:
                            throw new InvalidOperationException($"Unsupported Options file type: {path}");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Invalid RuntimeOption path in {sourceDescription}. Unrecognized scheme in path: {path}");
                }
            }

            string optionsPathOverride = Environment.GetEnvironmentVariable("METAPLAY_OPTIONS");
            string[] optionPaths = (optionsPathOverride ?? DefaultOptionsPaths).Split(";").Where(path => !string.IsNullOrEmpty(path)).ToArray();
            foreach (string path in optionPaths)
                AddPath(path, optionsPathOverride == null ? "built-in source" : "METAPLAY_OPTIONS environment variable", strictBindingChecks: true);

            // \note: EXTRA options are indended for infrastructure-injected variables. We tolerated unknown fields there to allow for forwards compatibility.
            string extraOptions = Environment.GetEnvironmentVariable("METAPLAY_EXTRA_OPTIONS");
            string[] extraOptionsPaths = extraOptions != null ? extraOptions.Split(";").Where(path => !string.IsNullOrEmpty(path)).ToArray() : Array.Empty<string>();
            foreach (string path in extraOptionsPaths)
                AddPath(path, "METAPLAY_EXTRA_OPTIONS environment variable", strictBindingChecks: false);

            declarations.Add(new RuntimeOptionsSourceSet.SourceDeclaration("Environment", "Passed in as an environment variable", strictBindingChecks: false, builder => builder.Add(environmentOptions.ConfigurationSource)));
            declarations.Add(new RuntimeOptionsSourceSet.SourceDeclaration("CLI", "Passed in as a command line option", strictBindingChecks: true, builder => builder.Add(commandLineOptions.ConfigurationSource)));
            return declarations;
        }

        /// <summary>
        /// Parses command line. On failure, kills the application and does not return.
        /// </summary>
        CommandLineRuntimeOptions ParseCommandLineOrDie(string[] cmdLineArgs)
        {
            // Resolve all the command line aliases for all the entries
            Dictionary<string, string> cmdLineAliases = new Dictionary<string, string>();
            foreach (Entry entry in _entries)
            {
                foreach (Entry.OptionValue value in entry.GetValues())
                {
                    if (value.CommandLineAlias != null)
                        cmdLineAliases.Add(value.CommandLineAlias, $"{entry.SectionName}:{value.Name}");
                }
            }

            try
            {
                return CommandLineRuntimeOptions.Parse(cmdLineArgs, cmdLineAliases);
            }
            catch (CommandLineRuntimeOptions.ParseError ex)
            {
                PrintCliUsageErrorAndExitNoReturn(new string[] { ex.Message });
                throw; // Unreachable, but compiler does not know
            }
        }

        const int SysexitsExUsage = 64; // EX_USAGE. See sysexits for more details

        /// <summary>
        /// Prints invalid command line error message to console and exits. Does not return.
        /// </summary>
        static void PrintCliUsageErrorAndExitNoReturn(string[] errors)
        {
            foreach (string error in errors)
                _log.Error("{CLIParseError}", error);

            Application.Application.ForceTerminate(SysexitsExUsage, "Invalid command line");
        }

        /// <summary>
        /// Parses options from environment variables.
        /// </summary>
        EnvironmentRuntimeOptionsSource ParseEnvironmentValues()
        {
            // Resolve all the environment variable aliases
            List<(string, string)> environmentAliases = new List<(string, string)>();
            foreach (Entry entry in _entries)
            {
                foreach (Entry.OptionValue value in entry.GetValues())
                {
                    foreach (string environmentVar in value.EnvironmentVariables)
                        environmentAliases.Add((environmentVar, $"{entry.SectionName}:{value.Name}"));
                }
            }

            return EnvironmentRuntimeOptionsSource.Parse(prefix: "Metaplay_", environmentAliases);
        }
    }
}
