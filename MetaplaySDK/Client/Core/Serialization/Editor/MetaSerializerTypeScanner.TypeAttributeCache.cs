// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Metaplay.Core.Serialization
{
    public class TypeAttributeCache
    {
        class TypeCacheEntry
        {
            readonly Type _type;
            object[] _attributesOnType = null; // null if not resolved

            public TypeCacheEntry(Type type)
            {
                _type = type;
            }

            public object[] GetAttributesOnType()
            {
                if (_attributesOnType == null)
                    _attributesOnType = _type.GetCustomAttributes(inherit: false);
                return _attributesOnType;
            }
        }
        class MemberCacheEntry
        {
            readonly MemberInfo _member;
            object[] _attributesOnMember = null; // null if not resolved

            public MemberCacheEntry(MemberInfo member)
            {
                _member = member;
            }

            public object[] GetAttributesOnMember()
            {
                if (_attributesOnMember == null)
                    _attributesOnMember = Attribute.GetCustomAttributes(_member);
                return _attributesOnMember;
            }
        }
        class MethodCacheEntry
        {
            public Type[] AttrTypes;

            public MethodCacheEntry(MethodInfo method)
            {
                IList<CustomAttributeData> data = method.GetCustomAttributesData();
                if (data.Count == 0)
                    AttrTypes = Array.Empty<Type>();
                else
                {
                    AttrTypes = new Type[data.Count];
                    for (int ndx = 0; ndx < data.Count; ++ndx)
                        AttrTypes[ndx] = data[ndx].AttributeType;
                }
            }
        }
        class AssemblyCacheEntry
        {
            readonly Assembly _assembly;
            MetaplayFeatureEnabledConditionAttributeUtil.InspectResult? _inspectResult = null; // null if not resolved

            public AssemblyCacheEntry(Assembly assembly)
            {
                _assembly = assembly;
            }

            public MetaplayFeatureEnabledConditionAttributeUtil.InspectResult GetAssemblyMetaFeatureInspectResult()
            {
                if (_inspectResult == null)
                    _inspectResult = MetaplayFeatureEnabledConditionAttributeUtil.InspectFeatureAttributes(_assembly.GetCustomAttributes<MetaplayFeatureEnabledConditionAttribute>());
                return _inspectResult.Value;
            }
        }

        Dictionary<Type, TypeCacheEntry> _typeCache = new Dictionary<Type, TypeCacheEntry>();
        Dictionary<MemberInfo, MemberCacheEntry> _memberCache = new Dictionary<MemberInfo, MemberCacheEntry>();
        Dictionary<MethodInfo, MethodCacheEntry> _methodCache = new Dictionary<MethodInfo, MethodCacheEntry>();
        Dictionary<Assembly, AssemblyCacheEntry> _assemblyCache = new Dictionary<Assembly, AssemblyCacheEntry>();

        TypeCacheEntry GetTypeCacheEntry(Type type)
        {
            if (_typeCache.TryGetValue(type, out TypeCacheEntry cachedEntry))
                return cachedEntry;
            TypeCacheEntry entry = new TypeCacheEntry(type);
            _typeCache[type] = entry;
            return entry;
        }
        MemberCacheEntry GetMemberCacheEntry(MemberInfo member)
        {
            if (_memberCache.TryGetValue(member, out MemberCacheEntry cachedEntry))
                return cachedEntry;
            MemberCacheEntry entry = new MemberCacheEntry(member);
            _memberCache[member] = entry;
            return entry;
        }
        MethodCacheEntry GetMethodCacheEntry(MethodInfo method)
        {
            if (_methodCache.TryGetValue(method, out MethodCacheEntry cachedEntry))
                return cachedEntry;
            MethodCacheEntry entry = new MethodCacheEntry(method);
            _methodCache[method] = entry;
            return entry;
        }
        AssemblyCacheEntry GetAssemblyCacheEntry(Assembly assembly)
        {
            if (_assemblyCache.TryGetValue(assembly, out AssemblyCacheEntry cachedEntry))
                return cachedEntry;
            AssemblyCacheEntry entry = new AssemblyCacheEntry(assembly);
            _assemblyCache[assembly] = entry;
            return entry;
        }

        public TAttr TryGetSealedAttributeOnExactType<TAttr>(Type type) where TAttr : Attribute
        {
            if (!typeof(TAttr).IsSealed)
                throw new ArgumentException("attribute must be sealed");
            TypeCacheEntry entry = GetTypeCacheEntry(type);
            TAttr found = null;
            foreach (object attr in entry.GetAttributesOnType())
            {
                if (attr.GetType() != typeof(TAttr))
                    continue;
                if (found != null)
                    throw new AmbiguousMatchException();
                found = (TAttr)attr;
            }
            return found;
        }

        public IEnumerable<TAttr> GetDerivedAttributesOnExactType<TAttr>(Type type)
        {
            List<TAttr> attrs = null;
            TypeCacheEntry entry = GetTypeCacheEntry(type);
            foreach (object attr in entry.GetAttributesOnType())
            {
                if (attr is TAttr derivedOrExact)
                {
                    if (attrs == null)
                        attrs = new List<TAttr>();
                    attrs.Add(derivedOrExact);
                }
            }
            if (attrs == null)
                return Array.Empty<TAttr>();
            return attrs;
        }

        public IEnumerable<TAttr> GetSealedAttributesOnExactType<TAttr>(Type type)
        {
            if (!typeof(TAttr).IsSealed)
                throw new ArgumentException("attribute must be sealed");
            List<TAttr> attrs = null;
            TypeCacheEntry entry = GetTypeCacheEntry(type);
            foreach (object attr in entry.GetAttributesOnType())
            {
                if (attr.GetType() == typeof(TAttr))
                {
                    if (attrs == null)
                        attrs = new List<TAttr>();
                    attrs.Add((TAttr)attr);
                }
            }
            if (attrs == null)
                return Array.Empty<TAttr>();
            return attrs;
        }

        public IEnumerable<TAttr> GetDerivedAttributesOnTypeOrAncestor<TAttr>(Type type)
        {
            List<TAttr> attrs = null;
            for (Type cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                TypeCacheEntry entry = GetTypeCacheEntry(cursor);
                foreach (object attr in entry.GetAttributesOnType())
                {
                    if (attr is TAttr derivedOrExact)
                    {
                        if (attrs == null)
                            attrs = new List<TAttr>();
                        attrs.Add(derivedOrExact);
                    }
                }
            }
            if (attrs == null)
                return Array.Empty<TAttr>();
            return attrs;
        }

        public TAttr TryGetSealedAttributeOnTypeOrAncestor<TAttr>(Type type) where TAttr : Attribute
        {
            if (!typeof(TAttr).IsSealed)
                throw new ArgumentException("attribute must be sealed");
            TAttr found = null;
            for (Type cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                TypeCacheEntry entry = GetTypeCacheEntry(cursor);
                foreach (object attr in entry.GetAttributesOnType())
                {
                    if (attr.GetType() != typeof(TAttr))
                        continue;
                    if (found != null)
                        throw new AmbiguousMatchException();
                    found = (TAttr)attr;
                }
            }

            return found;
        }

        public bool HasSealedAttributeOnExactType<TAttr>(Type type) where TAttr : Attribute
        {
            return TryGetSealedAttributeOnExactType<TAttr>(type) != null;
        }

        public bool HasSealedAttributeOnMember<TAttr>(MemberInfo member)
        {
            if (!typeof(TAttr).IsSealed)
                throw new ArgumentException("attribute must be sealed");
            MemberCacheEntry entry = GetMemberCacheEntry(member);
            bool found = false;
            foreach (object attr in entry.GetAttributesOnMember())
            {
                if (attr.GetType() != typeof(TAttr))
                    continue;
                if (found)
                    throw new AmbiguousMatchException();
                found = true;
            }
            return found;
        }

        public TAttr TryGetSealedAttributeOnMember<TAttr>(MemberInfo member) where TAttr : Attribute
        {
            if (!typeof(TAttr).IsSealed)
                throw new ArgumentException("attribute must be sealed");
            MemberCacheEntry entry = GetMemberCacheEntry(member);
            TAttr found = null;
            foreach (object attr in entry.GetAttributesOnMember())
            {
                if (attr.GetType() != typeof(TAttr))
                    continue;
                if (found != null)
                    throw new AmbiguousMatchException();
                found = (TAttr)attr;
            }
            return found;
        }

        public IEnumerable<TAttr> GetDerivedAttributesOnMember<TAttr>(MemberInfo member)
        {
            List<TAttr> attrs = null;
            MemberCacheEntry entry = GetMemberCacheEntry(member);
            foreach (object attr in entry.GetAttributesOnMember())
            {
                if (attr is TAttr derivedOrExact)
                {
                    if (attrs == null)
                        attrs = new List<TAttr>();
                    attrs.Add(derivedOrExact);
                }
            }
            if (attrs == null)
                return Array.Empty<TAttr>();
            return attrs;
        }

        public bool HasSealedAttributeOnMethod<TAttr>(MethodInfo method) where TAttr : Attribute
        {
            if (!typeof(TAttr).IsSealed)
                throw new ArgumentException("attribute must be sealed");
            MethodCacheEntry entry = GetMethodCacheEntry(method);
            foreach (Type type in entry.AttrTypes)
            {
                if (type == typeof(TAttr))
                    return true;
            }
            return false;
        }

        public bool IsMetaFeatureEnabled(Type type)
        {
            // A type is MetaFeature-enabled if it has either no MetaFeatureEnabledCondition attributes, or any of the attributes is true.
            // The attributes are searched for the type and all its parents and each types all assemblies.

            List<MetaplayFeatureEnabledConditionAttribute> attrs = null;
            bool hasSomeAttributes = false;

            for (Type cursor = type; cursor != null; cursor = cursor.BaseType)
            {
                attrs?.Clear();

                TypeCacheEntry entry = GetTypeCacheEntry(cursor);
                foreach (object attr in entry.GetAttributesOnType())
                {
                    if (attr is MetaplayFeatureEnabledConditionAttribute derivedOrExact)
                    {
                        if (attrs == null)
                            attrs = new List<MetaplayFeatureEnabledConditionAttribute>();
                        attrs.Add(derivedOrExact);
                    }
                }

                MetaplayFeatureEnabledConditionAttributeUtil.InspectResult typeResult = MetaplayFeatureEnabledConditionAttributeUtil.InspectFeatureAttributes(attrs);
                if (typeResult == MetaplayFeatureEnabledConditionAttributeUtil.InspectResult.SomeAttributeEnabled)
                    return true;
                if (typeResult == MetaplayFeatureEnabledConditionAttributeUtil.InspectResult.AllAttributesDisabled)
                    hasSomeAttributes = true;

                MetaplayFeatureEnabledConditionAttributeUtil.InspectResult assemblyResult = GetAssemblyCacheEntry(type.Assembly).GetAssemblyMetaFeatureInspectResult();
                if (assemblyResult == MetaplayFeatureEnabledConditionAttributeUtil.InspectResult.SomeAttributeEnabled)
                    return true;
                if (assemblyResult == MetaplayFeatureEnabledConditionAttributeUtil.InspectResult.AllAttributesDisabled)
                    hasSomeAttributes = true;
            }

            // If we had some attributes, and they were all false (we short circuit on true), the feature is not enabled.
            return !hasSomeAttributes;
        }
    }
}
