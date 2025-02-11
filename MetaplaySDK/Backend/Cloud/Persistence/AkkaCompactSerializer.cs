// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.IO;
using Metaplay.Core.Serialization;
using System;

namespace Metaplay.Cloud.Persistence
{
    class AkkaCompactSerializer : Akka.Serialization.Serializer
    {
        readonly MetaSerialization _serializer;

        public AkkaCompactSerializer(Akka.Actor.ExtendedActorSystem system) : base(system)
        {
            _serializer = MetaSerialization.Instance;
        }

        public override bool IncludeManifest => true;

        public override int Identifier => 14480;

        public override byte[] ToBinary(object obj)
        {
            MetaSerializationContext context = _serializer.CreateContext(MetaSerializationFlags.IncludeAll, resolver: null, logicVersion: null, debugStream: null, settings: null, actorSystem: system);

            using (MetaSerialization.BorrowedIOBuffer buffer = _serializer.BorrowIOBuffer())
            {
                using (IOWriter writer = new IOWriter(buffer.Buffer))
                    _serializer.Serializer.Serialize(ref context, writer, obj.GetType(), obj);
                return buffer.Buffer.ToArray();
            }
        }

        public override object FromBinary(byte[] bytes, Type type)
        {
            MetaSerializationContext context = _serializer.CreateContext(MetaSerializationFlags.IncludeAll, resolver: null, logicVersion: null, debugStream: null, settings: null, actorSystem: system);
            using (IOReader reader = new IOReader(bytes))
            {
                object result = _serializer.Serializer.Deserialize(ref context, reader, type);
                return result;
            }
        }
    }
}
