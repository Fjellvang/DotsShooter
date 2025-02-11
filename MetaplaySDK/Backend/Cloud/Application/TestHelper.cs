// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Analytics;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Metaplay.Cloud.Application
{
    public static class TestHelper
    {
        /// <summary>
        /// Initializes Metaplay, including MetaSerialization. Sets the working directory to the current test project's
        /// root (eg, MetaplaySDK/Backend/Cloud.Tests/ or MetaplaySDK/Backend/Server.Tests/) to make file accesses be able
        /// to use canonical paths. This is needed because running the tests from different environments (eg, Visual Studio,
        /// command line, or Rider) results in different initial working directories.
        /// </summary>
        public static void SetupForTests(Action<IServiceInitializers> configureServices = null)
        {
            // Pipe serilog to Console. This will be then captured by the test framework.
            Serilog.Log.Logger =
                new LoggerConfiguration()
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture, theme: ConsoleTheme.None)
                .CreateLogger();

            // \note Using the AppDomain base directory as that returns the directory where the build outputs
            //       reside (eg, <project>/bin/Debug/net8.0/) when run with `dotnet test`, Visual Studio, and Rider
            //       (unlike `Assembly.GetEntryAssembly()). It is also more stable than `GetCurrentDirectory()` as
            //       the working directory can change.
            string appDomainDir = AppDomain.CurrentDomain.BaseDirectory;

            // Initialize Metaplay core
            MetaplayCore.Initialize(init =>
            {
                init.ConfigureMetaSerialization("Test", appDomainDir, generateRuntimeTypeInfo: true);
                configureServices?.Invoke(init);
            });

            // Change directory to project directory (eg, MetaplaySDK/Backend/Cloud.Tests/ or MetaplaySDK/Backend/Server.Tests/) to enable relative file
            // paths to work properly. Depending on the environment where the tests are run, the build outputs may
            // be in slightly differing directories, so we search the 'bin/' path here to find the right location.
            string      fullPath    = Path.GetFullPath(appDomainDir);
            string[]    parts       = fullPath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            int         binIndex    = Array.IndexOf(parts, "bin");
            if (binIndex == -1)
                throw new InvalidOperationException($"The AppDomain.BaseDirectory path '{appDomainDir}' does not contain the directory 'bin/' in it -- unable to figure out what working directory to use for the tests!");
            string runDir = string.Join(Path.DirectorySeparatorChar, parts.Take(binIndex).ToArray());
            Log.Logger.Information("Changing working directory to {WorkingDirectory}", runDir);
            Directory.SetCurrentDirectory(runDir);
        }
    }

    public class TestSerializerTypeScanner : MetaSerializerTypeScanner
    {
        readonly List<Type> _typesToScan;

        public TestSerializerTypeScanner(List<Type> typesToScan)
        {
            _typesToScan = new List<Type>(typesToScan);

            // Add the internal types.
            _typesToScan.Add(typeof(MetaMessage));
            _typesToScan.Add(typeof(ModelAction));
            _typesToScan.Add(typeof(AnalyticsEventBase));
            _typesToScan.Add(typeof(AnalyticsContextBase));
        }

        protected override IEnumerable<Type> AllTypes(Assembly[] relevantAssemblies)
        {
            return _typesToScan;
        }
    }
}
