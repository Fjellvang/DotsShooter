// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Metaplay.Core.Model
{
    /// <summary>
    /// Mark a type as migratable. The type must then define its supported schema versions using <see cref="SupportedSchemaVersionsAttribute"/>.
    /// Individual migrations from one version to the next are declared as member functions on the type and decorated with <see cref="MigrationFromVersionAttribute"/>.
    /// </summary>
    public interface ISchemaMigratable
    {
    }

    /// <summary>
    /// Mark the migratable type to support specific schema versions. Older versions are no longer migrated (the contents are dropped).
    /// When the type is persisted, the current schema version is used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false)]
    public class SupportedSchemaVersionsAttribute : Attribute
    {
        public readonly MetaVersionRange SupportedSchemaVersions;

        public SupportedSchemaVersionsAttribute(int oldestSupportedVersion, int currentVersion)
        {
            SupportedSchemaVersions = new MetaVersionRange(oldestSupportedVersion, currentVersion);
        }
    }

    /// <summary>
    /// Mark a method as a migration step from the specific schema version to the next. The method must be non-static and take no arguments.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class MigrationFromVersionAttribute : Attribute
    {
        public readonly int FromVersion;

        public MigrationFromVersionAttribute(int fromVersion) => FromVersion = fromVersion;
    }

    /// <summary>
    /// As an alternative to individual MigrateFromVersion functions the migratable class can provide a single RegisterMigrationsFunction.
    /// The expected function signature is:
    /// <code>
    /// Action&lt;TMigratableType&gt; RegisterMigrations(int)
    /// </code>
    /// Upon initialization this function will be called with each of the supported versions, and the implementation can choose to provide
    /// a migration step for the corresponding version by returning the migration step as an Action accepting the migratable instance
    /// as its single argument.
    /// Note that a migration step must exist for each of the supported versions, so if the RegisterMigrations function returns null for
    /// a particular version then a migration for that version must be declared via MigrationFromVersionAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RegisterMigrationsFunctionAttribute : Attribute
    {
    }

    /// <summary>
    /// Registry of <see cref="SchemaMigrator"/>s for all migratable types.
    /// </summary>
    public class SchemaMigrationRegistry
    {
        public static SchemaMigrationRegistry Instance => MetaplayServices.Get<SchemaMigrationRegistry>();

        Dictionary<Type, SchemaMigrator> _migrators = new Dictionary<Type, SchemaMigrator>();

        public SchemaMigrationRegistry()
        {
            // Register all concrete classes implementing ISchemaMigratable
            ICollection<Type> migratableInterfaces = TypeScanner.GetInterfaceImplementations<ISchemaMigratable>();
            _migrators.EnsureCapacity(migratableInterfaces.Count);
            foreach (Type migratableType in migratableInterfaces)
            {
                if (migratableType.IsAbstract)
                    continue;
                _migrators.Add(migratableType, SchemaMigrator.CreateForType(migratableType));
            }
        }

        public SchemaMigrator GetSchemaMigrator(Type migratableType)
        {
            if (migratableType == null)
                throw new ArgumentNullException(nameof(migratableType));

            if (_migrators.TryGetValue(migratableType, out SchemaMigrator migrator))
                return migrator;
            else
                throw new ArgumentException($"No schema migrations registered for type {migratableType.ToGenericTypeString()}. The type must implement {nameof(ISchemaMigratable)} and declare the [SupportedSchemaVersions] attribute.");
        }

        public SchemaMigrator GetSchemaMigrator<TMigratable>() where TMigratable : ISchemaMigratable
        {
            return GetSchemaMigrator(typeof(TMigratable));
        }
    }
}
