// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Cloud.RuntimeOptions
{
    /// <summary>
    /// Mark a class as containing RuntimeOptions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class RuntimeOptionsAttribute : Attribute
    {
        /// <summary>
        /// Name of the section in the config files
        /// </summary>
        public readonly string  SectionName;
        /// <summary>
        /// Description of the section
        /// </summary>
        public readonly string  SectionDescription;
        /// <summary>
        /// Is the options block static (i.e., cannot be updated during runtime).
        /// </summary>
        public readonly bool    IsStatic;

        /// <param name="sectionName">Name of the section in the config files (required).</param>
        /// <param name="isStatic">Is the options block static (i.e., cannot be updated during runtime).</param>
        /// <param name="sectionDescription">Description of the section.</param>
        public RuntimeOptionsAttribute(string sectionName, bool isStatic, string sectionDescription = null)
        {
            SectionName        = sectionName ?? throw new ArgumentNullException(nameof(sectionName));
            SectionDescription = sectionDescription;
            IsStatic           = isStatic;
        }
    }

    /// <summary>
    /// Define a command line alias for the given RuntimeOption.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CommandLineAliasAttribute : Attribute
    {
        public readonly string Alias;

        public CommandLineAliasAttribute(string alias) { Alias = alias; }
    }

    /// <summary>
    /// Define an environment variable for the given RuntimeOption. This environment variable is used
    /// in addition to the normal environment variable (Metaplay_FooOptions__BarField). It is an error
    /// to have multiple environment variables set for a same backing field at the same time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EnvironmentVariableAttribute : Attribute
    {
        public readonly string Alias;

        public EnvironmentVariableAttribute(string environmentVariable) { Alias = environmentVariable; }
    }

    /// <summary>
    /// Define the RuntimeOption as Computed. Computed values cannot be assigned
    /// from runtime options and instead are computed from other runtime options.
    /// Properties with no setter are automatically interpreted as Computed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ComputedValueAttribute : Attribute
    {
    }
}
