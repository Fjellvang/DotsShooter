// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Metaplay.Core.Model
{
    // ModelActionSpec

    public class ModelActionSpec
    {
        public int                                  TypeCode;
        public Type                                 Type;
        public ModelActionExecuteFlags              ExecuteFlags;
        public MetaDictionary<Type, Attribute>   CustomAttributes;

        public ModelActionSpec(int typeCode, Type type, ModelActionExecuteFlags execFlags, IEnumerable<Attribute> customAttributes)
        {
            TypeCode            = typeCode;
            Type                = type;
            ExecuteFlags        = execFlags;
            CustomAttributes    = new MetaDictionary<Type, Attribute>();

            foreach (Attribute customAttribute in customAttributes)
                CustomAttributes.AddIfAbsent(customAttribute.GetType(), customAttribute);
        }

        public T TryGetCustomAttribute<T>() where T : Attribute
        {
            if (CustomAttributes.TryGetValue(typeof(T), out Attribute attrib))
                return (T)attrib;
            return null;
        }

        public bool HasCustomAttribute<T>() where T : Attribute
        {
            return CustomAttributes.ContainsKey(typeof(T));
        }
    }

    /// <summary>
    /// Repository for keeping track of all <see cref="ModelAction"/>s.
    /// </summary>
    public class ModelActionRepository
    {
        public static ModelActionRepository Instance => MetaplayServices.Get<ModelActionRepository>();

        public Dictionary<int, ModelActionSpec>     SpecFromCode    { get; } = new Dictionary<int, ModelActionSpec>();
        public Dictionary<Type, ModelActionSpec>    SpecFromType    { get; } = new Dictionary<Type, ModelActionSpec>();
        public Dictionary<string, ModelActionSpec>  SpecFromName    { get; } = new Dictionary<string, ModelActionSpec>();

        public ModelActionRepository()
        {
            RegisterActionTypes();
        }

        private void RegisterActionTypes()
        {
            foreach (Type actionType in MetaSerializerTypeRegistry.GetConcreteDerivedTypes<ModelAction>())
            {
                ModelActionAttribute attrib = actionType.GetCustomAttribute<ModelActionAttribute>(inherit: false);
                if (attrib == null)
                    MetaDebug.AssertFail("Missing ModelActionAttribute from {0}", actionType.ToGenericTypeString());

                int typeCode = attrib.TypeCode;
                if (typeCode <= 0)
                    MetaDebug.AssertFail($"Action type code for {actionType.ToGenericTypeString()} must be a positive integer");
                if (SpecFromCode.TryGetValue(typeCode, out ModelActionSpec typeCodeConflictingSpec))
                    MetaDebug.AssertFail("Duplicate Actions with typeCode #{0}: {1}, {2}", typeCode, actionType.ToGenericTypeString(), typeCodeConflictingSpec.Type.ToGenericTypeString());

                string actionName = actionType.ToGenericTypeString();
                if (SpecFromName.TryGetValue(actionName, out ModelActionSpec nameConflictingSpec))
                    MetaDebug.AssertFail("Duplicate Actions with name {0}: {1}, {2}", actionName, actionType.ToNamespaceQualifiedTypeString(), nameConflictingSpec.Type.ToNamespaceQualifiedTypeString());

                ModelActionExecuteFlags execFlags = ResolveActionExecuteFlags(actionType);
                if (execFlags == ModelActionExecuteFlags.None)
                    MetaDebug.AssertFail("In {0} action, action has no declared execution modes. Declare allowed execution mode with [ModelActionExecuteFlags] attribute in the Action, or in any of its baseclasses.", actionType.ToGenericTypeString());

                ModelActionSpec spec = new ModelActionSpec(typeCode, actionType, execFlags, actionType.GetCustomAttributes());
                SpecFromCode.Add(typeCode, spec);
                SpecFromType.Add(actionType, spec);
                SpecFromName.Add(actionName, spec);
            }
        }

        static ModelActionExecuteFlags ResolveActionExecuteFlags(Type rootType)
        {
            Type typeCursor = rootType;
            for (;;)
            {
                if (typeCursor == null)
                    return ModelActionExecuteFlags.None;

                ModelActionExecuteFlagsAttribute modeAttr = typeCursor.GetCustomAttribute<ModelActionExecuteFlagsAttribute>(inherit: false);
                if (modeAttr != null)
                    return modeAttr.Modes;

                typeCursor = typeCursor.BaseType;
            }
        }

        public void PrintActionTypes()
        {
            // Print in sorted order
            Console.WriteLine("Actions:");
            var sorted = SpecFromCode.ToList();
            sorted.Sort((a, b) => a.Key - b.Key);
            foreach (var entry in sorted)
            {
                ModelActionSpec spec = entry.Value;
                Console.WriteLine("  #{0}: {1}", spec.TypeCode, spec.Type);
            }
            Console.WriteLine();
        }
    }
}
