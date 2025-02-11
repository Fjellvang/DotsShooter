// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Metaplay.Core.Serialization
{
    /// <summary>
    /// MetaSerializerTypeInfo provider based on a pre-baked, compiled registry. Used by Unity Client.
    /// </summary>
    public class RuntimeTypeInfoProvider
    {
        readonly TaggedSerializerRoslyn _generatedSerializer;

        public RuntimeTypeInfoProvider(TaggedSerializerRoslyn generatedSerializer)
        {
            if (!generatedSerializer.HasTypeInfoMethod)
                throw new InvalidOperationException("Serializer has been generated without runtime type info!");
            _generatedSerializer = generatedSerializer;
        }

        public MetaSerializerTypeInfo GetTypeInfo()
        {
            return _generatedSerializer.GetTypeInfo();
        }
    }
}
