// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static System.FormattableString;

namespace Metaplay.Core
{
    public class EntityKindRegistry
    {
        public static EntityKindRegistry Instance => MetaplayServices.Get<EntityKindRegistry>();

        public readonly Dictionary<string, EntityKind>  ByName  = new Dictionary<string, EntityKind>();
        public readonly Dictionary<EntityKind, string>  ByValue = new Dictionary<EntityKind, string>();

        public EntityKindRegistry()
        {
            #if NETCOREAPP || UNITY_EDITOR // Validate on server and editor -- skip devices
            Dictionary<Type, EntityKindRegistryAttribute[]> typeToAttribs = new Dictionary<Type, EntityKindRegistryAttribute[]>();
            #endif

            foreach (Type type in TypeScanner.GetClassesWithAttribute<EntityKindRegistryAttribute>())
            {
                #if NETCOREAPP || UNITY_EDITOR
                // Check for range conflicts
                EntityKindRegistryAttribute[] attribs = type.GetCustomAttributes<EntityKindRegistryAttribute>().ToArray();
                for (int pairA = 0; pairA < attribs.Length; ++pairA)
                {
                    for (int pairB = pairA+1; pairB < attribs.Length; ++pairB)
                    {
                        if (DoesRangeOverlap(attribs[pairA], attribs[pairB]))
                            throw new InvalidOperationException($"Ranges for EntityKind registry {type.ToGenericTypeString()} ({attribs[pairA].StartIndex}..{attribs[pairA].EndIndex}) ({attribs[pairB].StartIndex}..{attribs[pairB].EndIndex}) overlap!");
                    }
                }
                foreach (EntityKindRegistryAttribute attrib in attribs)
                {
                    foreach ((Type otherType, EntityKindRegistryAttribute[] others) in typeToAttribs)
                    {
                        foreach (EntityKindRegistryAttribute other in others)
                        {
                            if (DoesRangeOverlap(attrib, other))
                                throw new InvalidOperationException($"Ranges for EntityKind registries {type.ToGenericTypeString()} ({attrib.StartIndex}..{attrib.EndIndex}) and {otherType.ToGenericTypeString()} ({other.StartIndex}..{other.EndIndex}) overlap!");
                        }
                    }
                }
                typeToAttribs.Add(type, attribs);
                #endif

                // Iterate over all EntityKind registrations
                foreach (FieldInfo fi in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (fi.FieldType != typeof(EntityKind))
                        continue;

                    #if NETCOREAPP || UNITY_EDITOR
                    if (!fi.IsInitOnly)
                        throw new InvalidOperationException($"{type.ToGenericTypeString()}.{fi.Name} must be 'static readonly'");
                    #endif

                    EntityKind kind = (EntityKind)fi.GetValue(null);

                    #if NETCOREAPP || UNITY_EDITOR
                    // Check that value is in range
                    bool inRange = false;
                    foreach (EntityKindRegistryAttribute attrib in attribs)
                    {
                        if (kind.Value >= attrib.StartIndex && kind.Value < attrib.EndIndex)
                            inRange = true;
                    }
                    if (!inRange)
                        throw new InvalidOperationException($"{type.ToGenericTypeString()}.{fi.Name} value ({kind.Value}) is not in any range specified with [EntityKindRegistry] attributes: {string.Join(", ", attribs.Select(attrib => $"[{attrib.StartIndex}..{attrib.EndIndex})"))}.");

                    // Check for name conflicts
                    if (ByName.TryGetValue(fi.Name, out EntityKind _))
                        throw new InvalidOperationException($"Duplicate EntityKinds with name {fi.Name}");

                    // Check for value conflicts
                    if (ByValue.TryGetValue(kind, out string existingName))
                        throw new InvalidOperationException($"EntityKinds {fi.Name} and {existingName} have the same value {kind.Value}");
                    #endif

                    ByName.Add(fi.Name, kind);
                    ByValue.Add(kind, fi.Name);
                }
            }
        }

        static bool DoesRangeOverlap(EntityKindRegistryAttribute a, EntityKindRegistryAttribute b)
        {
            int start0  = a.StartIndex;
            int end0    = a.EndIndex;
            int start1  = b.StartIndex;
            int end1    = b.EndIndex;
            int w0 = end0 - start0;
            int w1 = end1 - start1;
            int mn = System.Math.Min(start0, start1);
            int mx = System.Math.Max(end0, end1);
            return (mx - mn) < (w0 + w1);
        }

        public static bool TryFromName(string str, out EntityKind kind) =>
            Instance.ByName.TryGetValue(str, out kind);

        public static EntityKind FromName(string str)
        {
            if (TryFromName(str, out EntityKind kind))
                return kind;
            else
                throw new InvalidOperationException($"No such EntityKind '{str}'");
        }

        public static string GetName(EntityKind kind)
        {
            if (Instance.ByValue.TryGetValue(kind, out string name))
                return name;
            else
                return Invariant($"Invalid#{kind.Value}");
        }

        /// <summary>
        /// Check whether the given EntityKind is a valid value found in the registry.
        /// <c>EntityKind.None</c> returns false.
        /// </summary>
        public static bool IsValid(EntityKind kind) =>
            Instance.ByValue.ContainsKey(kind);

        public static IEnumerable<EntityKind> AllValues =>
            Instance.ByValue.Keys;
    }
}
