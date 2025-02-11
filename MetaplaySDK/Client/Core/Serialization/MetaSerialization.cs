// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using Metaplay.Core.IO;
using Metaplay.Core.Memory;
using Metaplay.Core.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using static System.FormattableString;
#if NETCOREAPP
using Akka.Actor;
#endif

namespace Metaplay.Core.Serialization
{
    /// <summary>
    /// Represents an error during serialization or deserialization.
    /// </summary>
    public class MetaSerializationException : Exception
    {
        public MetaSerializationException() { }
        public MetaSerializationException(string message) : base(message) { }
        public MetaSerializationException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Represents an error during serialization or deserialization.
    /// </summary>
    public class MetaTypeMissingInLogicVersionSerializationException : MetaSerializationException
    {
        public string DerivedTypeName     { get; }
        public int    MinimumLogicVersion { get; }
        public MetaTypeMissingInLogicVersionSerializationException(string derivedTypeName, int? currentLogicVersion, int minimumLogicVersion) : base(Invariant($"Type '{derivedTypeName}' cannot be serialized as it is not available in logic version {currentLogicVersion}, at minimum version {minimumLogicVersion} is required"))
        {
            DerivedTypeName          = derivedTypeName;
            MinimumLogicVersion      = minimumLogicVersion;
        }
    }

    /// <summary>
    /// A deserialization error caused by an unknown derived type code.
    /// </summary>
    public class MetaUnknownDerivedTypeDeserializationException : MetaSerializationException
    {
        public readonly Type AttemptedType;
        public readonly int  EncounteredTypeCode;

        public MetaUnknownDerivedTypeDeserializationException(string message, Type attemptedType, int encounteredTypeCode)
            : base(message)
        {
            AttemptedType       = attemptedType;
            EncounteredTypeCode = encounteredTypeCode;
        }
    }

    /// <summary>
    /// A deserialization error caused by mismatching wire data types.
    /// </summary>
    public class MetaWireDataTypeMismatchDeserializationException : MetaSerializationException
    {
        public readonly string       MemberName;
        public readonly Type         AttemptedType;
        public readonly WireDataType ExpectedWireDataType;
        public readonly WireDataType EncounteredWireDataType;

        public MetaWireDataTypeMismatchDeserializationException(
            string message,
            string memberName,
            Type attemptedType,
            WireDataType expectedWireDataType,
            WireDataType encounteredWireDataType)
            : base(message)
        {
            MemberName              = memberName;
            AttemptedType           = attemptedType;
            ExpectedWireDataType    = expectedWireDataType;
            EncounteredWireDataType = encounteredWireDataType;
        }
    }

    public class MetaSerializationDepthExceededException : MetaSerializationException
    {
        public readonly int MaxDepth;

        public MetaSerializationDepthExceededException(string message, int maxDepth)
            : base(message)
        {
            MaxDepth = maxDepth;
        }
    }

    /// <summary>
    /// Flags to specify how serialization operations should behave. These essentially exclude members
    /// based on <see cref="MetaMemberFlags"/>.
    /// </summary>
    [MetaSerializable]
    public enum MetaSerializationFlags
    {
        IncludeAll      = 0,                          // Include all fields (local cloning etc.)
        SendOverNetwork = MetaMemberFlags.Hidden,     // All but hidden fields are transmitted over network
        ComputeChecksum = MetaMemberFlags.NoChecksum, // Checksum computation ignores NoChecksum members
        Persisted       = MetaMemberFlags.Transient,  // Include all persisted members
        EntityEventLog  = MetaMemberFlags.ExcludeFromEventLog,
    }

    public ref struct MetaSerializationMemberContext
    {
        public string MemberName        { get; private set; }
        public int    MaxCollectionSize { get; private set; }

        public void UpdateMemberName(string memberName)
        {
            MemberName = memberName;
        }

        public void UpdateCollectionSize(int maxCollectionSize)
        {
            MaxCollectionSize = maxCollectionSize;
        }

        public void Update(string memberName, int maxCollectionSize)
        {
            UpdateMemberName(memberName);
            UpdateCollectionSize(maxCollectionSize);
        }
    }

    /// <summary>
    /// Represents state passed into serialization functions.
    /// </summary>
    public ref struct MetaSerializationContext
    {
        public readonly MetaMemberFlags            ExcludeFlags; // Mask of member flags to exclude when serializing.
        public readonly IGameConfigDataResolver    Resolver;     // Resolver for GameConfigData
        public readonly int?                       LogicVersion; // LogicVersion of data to serialize (null means ignore any version attributes)
        public readonly StringBuilder              DebugStream;  // Output stream for all debug data
        public readonly MetaSerializerTypeRegistry TypeInfo;

        public MetaSerializationMetaRefTraversalParams MetaRefTraversal;

        #if NETCOREAPP
        public readonly ExtendedActorSystem  ActorSystem;    // Akka.net actor system (for deserializing IActorRefs)
        #endif

        // \todo [petri] make configurable?
        public int MaxStringSize    => 64 * 1024 * 1024; // Maximum size of encoded string (in bytes)
        public int MaxByteArraySize => 64 * 1024 * 1024; // Maximum size of byte array

        public const int DefaultMaxCollectionSize = 16384;

        public MetaSerializationMemberContext MemberContext;

        public readonly int MaxDepth;
        /// <summary>
        /// Depth tracked during (de)serialization.
        /// Caveat: this is not the exact depth, because for performance reasons depth is not tracked for all types.
        /// </summary>
        public int Depth;

        public MetaSerializationContext(
            MetaSerializationFlags flags,
            IGameConfigDataResolver resolver,
            int? logicVersion,
            StringBuilder debugStream,
            MetaSerializationMetaRefTraversalParams metaRefTraversal,
            MetaSerializationSettings settings,
            MetaSerializerTypeRegistry typeInfo
            #if NETCOREAPP
            ,
            ExtendedActorSystem actorSystem
            #endif
        )
        {
            // \note MetaSerializationFlags is already an exclude mask, so we just cast here
            ExcludeFlags = (MetaMemberFlags)flags;
            Resolver     = resolver;
            LogicVersion = logicVersion;
            DebugStream  = debugStream;
            TypeInfo     = typeInfo;

            MetaRefTraversal = metaRefTraversal;

            #if NETCOREAPP
            ActorSystem     = actorSystem;
            #endif
            MemberContext = new MetaSerializationMemberContext();
            MemberContext.UpdateCollectionSize(DefaultMaxCollectionSize);

            if (settings.MaxDepth < 0)
                throw new ArgumentException($"Max depth must be non-negative, got {settings.MaxDepth}", nameof(settings));

            MaxDepth = settings.MaxDepth;
            Depth    = 0;
        }

        public void VisitMetaRef<TItem>(MetaRef<TItem> metaRef)
            where TItem : class, IGameConfigData
        {
            if (MetaRefTraversal.VisitMetaRef == null)
                return;

            MetaRefTraversal.VisitMetaRef.Invoke(ref this, metaRef);
        }

        public void VisitMetaConfigId<TItem>(MetaConfigId<TItem> metaConfigId)
            where TItem : class, IGameConfigData
        {
            if (MetaRefTraversal.VisitMetaConfigId == null)
                return;

            MetaRefTraversal.VisitMetaConfigId.Invoke(ref this, metaConfigId);
        }

        // \note Throw helper for performance reasons. See https://learn.microsoft.com/en-us/dotnet/communitytoolkit/diagnostics/throwhelper#technical-details
        //       for possible explanations.
        //       Regardless the exact reason, it did actually seem to make a measurable perf difference when i tested.
        void ThrowDepthExceededException()
        {
            throw new MetaSerializationDepthExceededException($"Depth limit exceeded. Depth is at least {MaxDepth}.", MaxDepth);
        }

        public void IncrementDepth()
        {
            if (Depth == MaxDepth)
                ThrowDepthExceededException();

            Depth++;
        }

        public void DecrementDepth()
        {
            Depth--;
        }

        [Conditional("DEBUG")]
        public void DebugAssertZeroDepth()
        {
            if (Depth != 0)
                throw new MetaAssertException($"Expected depth to be 0, but is {Depth}");
        }
    }

    public struct MetaSerializationSettings
    {
        public const int DefaultMaxDepth = 256;

        public readonly int MaxDepth;

        public MetaSerializationSettings(int maxDepth)
        {
            MaxDepth = maxDepth;
        }

        public static MetaSerializationSettings CreateDefault()
        {
            return new MetaSerializationSettings(maxDepth: DefaultMaxDepth);
        }
    }

    public struct MetaSerializationMetaRefTraversalParams
    {
        public delegate void VisitTableTopLevelConfigItemDelegate(ref MetaSerializationContext context, IGameConfigData item);

        public delegate void VisitMetaRefDelegate(ref MetaSerializationContext context, IMetaRef metaRef);

        public delegate void VisitMetaConfigIdDelegate(ref MetaSerializationContext context, IMetaConfigId metaConfigId);

        public readonly VisitTableTopLevelConfigItemDelegate VisitTableTopLevelConfigItem;
        public readonly VisitMetaRefDelegate                 VisitMetaRef;
        public readonly VisitMetaConfigIdDelegate            VisitMetaConfigId;

        public MetaSerializationMetaRefTraversalParams(VisitTableTopLevelConfigItemDelegate visitTableTopLevelConfigItem, VisitMetaRefDelegate visitMetaRef, VisitMetaConfigIdDelegate visitMetaConfigId)
        {
            VisitTableTopLevelConfigItem = visitTableTopLevelConfigItem;
            VisitMetaRef                 = visitMetaRef;
            VisitMetaConfigId            = visitMetaConfigId;
        }

        public static MetaSerializationMetaRefTraversalParams CreateDefault()
        {
            return new MetaSerializationMetaRefTraversalParams(DefaultVisitTableTopLevelConfigItem, DefaultVisitMetaRef, _defaultVisitMetaConfigId);
        }

        // \note This is a static delegate instead of a method in order to avoid allocating in CreateDefault.
        static readonly VisitTableTopLevelConfigItemDelegate DefaultVisitTableTopLevelConfigItem = (ref MetaSerializationContext context, IGameConfigData item) => { };

        // \note This is a static delegate instead of a method in order to avoid allocating in CreateDefault.
        static readonly VisitMetaRefDelegate DefaultVisitMetaRef = (ref MetaSerializationContext context, IMetaRef metaRef) => { metaRef.ResolveInPlace(context.Resolver); };

        // \note This is a static delegate instead of a method in order to avoid allocating in CreateDefault.
        static readonly VisitMetaConfigIdDelegate _defaultVisitMetaConfigId = (ref MetaSerializationContext context, IMetaConfigId metaConfigId) => { }; // \note: We specifically do not want to throw for invalid MetaConfigIds here, as they are tolerated on deserialization as well.
    }

    /// <summary>
    /// Describes a failure that occurred when deserializing a member of a struct/class.
    /// This is passed to a handler registered with the <see cref="MetaOnMemberDeserializationFailureAttribute"/>.
    /// </summary>
    public struct MetaMemberDeserializationFailureParams
    {
        /// <summary>
        /// The serialized payload of the member.
        /// </summary>
        /// <remarks>
        /// This is _not_ prefixed with the wire data type tag for the member,
        /// nor with the member tag id. Those are known statically from the
        /// member's static type and the MetaMember tag id, respectively.
        /// </remarks>
        public readonly byte[]                      MemberPayload;
        /// <summary>
        /// The exception that caused the failure.
        /// </summary>
        public readonly Exception                   Exception;
        public readonly MetaSerializerTypeRegistry  TypeInfo;

        // \todo [nuutti] Include MetaSerializationContext. It's a ref struct, so cannot include as is.
        //                MetaMemberDeserializationFailureParams itself cannot be ref struct at the moment,
        //                since MemberAccessGenerator boxes it.

        public MetaMemberDeserializationFailureParams(byte[] memberPayload, Exception exception, ref MetaSerializationContext context)
        {
            MemberPayload = memberPayload;
            Exception     = exception;
            TypeInfo      = context.TypeInfo;
        }
    }

    /// <summary>
    /// Parameters (optionally) passed to a custom on-deserialized handler registered
    /// with the <see cref="MetaOnDeserializedAttribute"/>.
    /// </summary>
    public struct MetaOnDeserializedParams
    {
        public readonly IGameConfigDataResolver    Resolver;
        public readonly int?                       LogicVersion;
        public readonly MetaSerializerTypeRegistry TypeInfo;

        public MetaOnDeserializedParams(ref MetaSerializationContext context)
        {
            Resolver     = context.Resolver;
            LogicVersion = context.LogicVersion;
            TypeInfo     = context.TypeInfo;
        }
    }

    /// <summary>
    /// High-level logic object serialization API.
    ///
    /// Provides functions for serialization and deserialization using a tagged format for MetaSerializable types.
    /// This is used for most serialization needs of the SDK, such as server-client over-the-wire and database persisting.
    /// </summary>
    public class MetaSerialization
    {
        /// <summary>
        /// Simple wrapper to allow the use of 'using' keyword for borrowing a thread-local buffer from
        /// _recycleBuffers with added safety mechanism in Clear() to avoid an IOBuffer in broken state
        /// to fail all future serializations on the thread.
        /// </summary>
        public struct BorrowedIOBuffer : IDisposable
        {
            public IOBuffer Buffer => _recycleBuffers.Value;
            readonly ThreadLocal<IOBuffer> _recycleBuffers;

            public BorrowedIOBuffer(ThreadLocal<IOBuffer> recycleBuffers)
            {
                _recycleBuffers = recycleBuffers;
            }

            public void Dispose()
            {
                // Extra-safe buffer releases: in case the buffer Clear() fails, we re-create the buffer
                // as otherwise all future serialization on this thread may end up failing.
                try
                {
                    _recycleBuffers.Value.Clear();
                }
                catch (Exception ex)
                {
                    DebugLog.Error("Failed to release serializer recycled buffer, reallocating buffer: {Error}", ex);
                    _recycleBuffers.Value = new SegmentedIOBuffer();
                }
            }
        }

        #if NETCOREAPP
        ExtendedActorSystem _actorSystem = null;

        public void RegisterActorSystem(ExtendedActorSystem actorSystem)
        {
            _actorSystem = actorSystem;
        }

        #endif

        readonly MetaSerializerTypeRegistry _typeRegistry;

        // The generated serializer
        readonly TaggedSerializerRoslyn _taggedSerializerRoslyn;
        public   TaggedSerializerRoslyn Serializer => _taggedSerializerRoslyn;

        // Recycled SegmentedIOBuffers for each thread to enable per-segment memory re-use
        readonly ThreadLocal<IOBuffer> _recycleBuffers = new ThreadLocal<IOBuffer>(() => new SegmentedIOBuffer());

        public MetaSerialization(MetaSerializerTypeRegistry typeRegistry, TaggedSerializerRoslyn roslynGeneratedSerializer)
        {
            _typeRegistry = typeRegistry;
            _taggedSerializerRoslyn = roslynGeneratedSerializer;

            #if !NETCOREAPP
            WarmUpSerializerForUnity();
            #endif
        }

        #if !NETCOREAPP
        void WarmUpSerializerForUnity()
        {
            // It seems that:
            //
            // Unity on Android uses stop-the-world Garbage collection that forcibly interrupts all threads, including background threads,
            // regardless of what they are processing at the time. If GC is triggered while a background thread is initializing lazily initialized IL2CPP
            // object metadata (i.e. accessing a type for the first time in the process instance), it still holds the the internal lock IL2CPP metadata
            // lock while it's parked for GC. Now, if UnityMain thread decides to access non-initialized lazily initialized metadata record for the GC sweep,
            // it first tries to take the lock, notices it's not available, and chooses to wait for it. And this wait never completes.
            //
            // Avoid this by initializing lazily-init static class initializers immediately with a dummy serialization operation.
            //
            // \todo [jarkko] make a reliable repro and report to unity.

            MetaSerializationContext context = CreateContext(MetaSerializationFlags.IncludeAll, resolver: null, logicVersion: null, debugStream: null, settings: null);
            using (FlatIOBuffer buffer = new FlatIOBuffer())
            using (IOWriter writer = new IOWriter(buffer))
                _taggedSerializerRoslyn.Serialize<MetaMessage>(ref context, writer, null);
        }
        #endif

        public static MetaSerialization Instance => MetaplayServices.Get<MetaSerialization>();

        // TAGGED SERIALIZATION

        public MetaSerializationContext CreateContext(
            MetaSerializationFlags flags,
            IGameConfigDataResolver resolver,
            int? logicVersion,
            StringBuilder debugStream,
            MetaSerializationSettings? settings,
            MetaSerializationMetaRefTraversalParams? metaRefTraversalParams = null
            #if NETCOREAPP
            , ExtendedActorSystem actorSystem = null
            #endif
            )
        {
            return new MetaSerializationContext(
                flags,
                resolver,
                logicVersion,
                debugStream,
                metaRefTraversalParams ?? MetaSerializationMetaRefTraversalParams.CreateDefault(),
                settings ?? MetaSerializationSettings.CreateDefault(),
                _typeRegistry
                #if NETCOREAPP
                , actorSystem ?? _actorSystem
                #endif
            );
        }

        public BorrowedIOBuffer BorrowIOBuffer()
        {
            return new BorrowedIOBuffer(_recycleBuffers);
        }

        public static byte[] SerializeTagged<T>(
            T value,
            MetaSerializationFlags flags,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver: null, logicVersion, debugStream, settings);
            using (BorrowedIOBuffer buffer = instance.BorrowIOBuffer())
            {
                using (IOWriter writer = new IOWriter(buffer.Buffer))
                    instance._taggedSerializerRoslyn.Serialize<T>(ref context, writer, value);
                return buffer.Buffer.ToArray();
            }
        }

        public static void SerializeTagged<T>(
            IOWriter writer,
            T value,
            MetaSerializationFlags flags,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver: null, logicVersion, debugStream, settings);
            instance._taggedSerializerRoslyn.Serialize<T>(ref context, writer, value);
        }

        public static byte[] SerializeTagged(
            Type type,
            object obj,
            MetaSerializationFlags flags,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver: null, logicVersion, debugStream, settings);
            using (BorrowedIOBuffer buffer = instance.BorrowIOBuffer())
            {
                using (IOWriter writer = new IOWriter(buffer.Buffer))
                    instance._taggedSerializerRoslyn.Serialize(ref context, writer, type, obj);
                return buffer.Buffer.ToArray();
            }
        }

        public static void SerializeTagged(
            IOWriter writer,
            Type type,
            object obj,
            MetaSerializationFlags flags,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver: null, logicVersion, debugStream, settings);
            instance._taggedSerializerRoslyn.Serialize(ref context, writer, type, obj);
        }

        public static MetaSerialized<T> ToMetaSerialized<T>(
            T value,
            MetaSerializationFlags flags,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null)
        {
            byte[] bytes = SerializeTagged(value, flags, logicVersion, debugStream, settings);
            return new MetaSerialized<T>(bytes, flags);
        }

        public static byte[] SerializeTableTagged<T>(
            IReadOnlyList<T> items,
            MetaSerializationFlags flags,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null,
            int maxCollectionSizeOverride = MetaSerializationContext.DefaultMaxCollectionSize)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context  = instance.CreateContext(flags, resolver: null, logicVersion, debugStream, settings);
            using (BorrowedIOBuffer buffer = instance.BorrowIOBuffer())
            {
                using (IOWriter writer = new IOWriter(buffer.Buffer))
                    instance._taggedSerializerRoslyn.SerializeTable<T>(ref context, writer, items, maxCollectionSizeOverride);
                return buffer.Buffer.ToArray();
            }
        }

        public static void SerializeTableTagged<T>(
            IOWriter writer,
            IReadOnlyList<T> items,
            MetaSerializationFlags flags,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null,
            int maxCollectionSizeOverride = MetaSerializationContext.DefaultMaxCollectionSize)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver: null, logicVersion, debugStream, settings);
            instance._taggedSerializerRoslyn.SerializeTable<T>(ref context, writer, items, maxCollectionSizeOverride);
        }

        public static T DeserializeTagged<T>(
            byte[] serialized,
            MetaSerializationFlags flags,
            IGameConfigDataResolver resolver,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver, logicVersion, debugStream, settings);
            using (IOReader reader = new IOReader(serialized))
            {
                T result = instance._taggedSerializerRoslyn.Deserialize<T>(ref context, reader);
                return result;
            }
        }

        public static object DeserializeTagged(
            byte[] serialized,
            Type type,
            MetaSerializationFlags flags,
            IGameConfigDataResolver resolver,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver, logicVersion, debugStream, settings);
            using (IOReader reader = new IOReader(serialized))
            {
                object result = instance._taggedSerializerRoslyn.Deserialize(ref context, reader, type);
                return result;
            }
        }

        public static T DeserializeTagged<T>(
            IOReader reader,
            MetaSerializationFlags flags,
            IGameConfigDataResolver resolver,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver, logicVersion, debugStream, settings);
            return instance._taggedSerializerRoslyn.Deserialize<T>(ref context, reader);
        }

        public static object DeserializeTagged(
            IOReader reader,
            Type type,
            MetaSerializationFlags flags,
            IGameConfigDataResolver resolver,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver, logicVersion, debugStream, settings);
            return instance._taggedSerializerRoslyn.Deserialize(ref context, reader, type);
        }

        public static IReadOnlyList<T> DeserializeTableTagged<T>(
            byte[] serialized,
            MetaSerializationFlags flags,
            IGameConfigDataResolver resolver,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null,
            int maxCollectionSizeOverride = MetaSerializationContext.DefaultMaxCollectionSize)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver, logicVersion, debugStream, settings);
            using (IOReader reader = new IOReader(serialized))
            {
                return instance._taggedSerializerRoslyn.DeserializeTable<T>(ref context, reader, maxCollectionSizeOverride, typeof(T));
            }
        }

        public static IReadOnlyList<T> DeserializeTableTagged<T>(
            IOReader reader,
            MetaSerializationFlags flags,
            IGameConfigDataResolver resolver,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null,
            int maxCollectionSizeOverride = MetaSerializationContext.DefaultMaxCollectionSize)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver, logicVersion, debugStream, settings);
            return instance._taggedSerializerRoslyn.DeserializeTable<T>(ref context, reader, maxCollectionSizeOverride, typeof(T));
        }

        public static IReadOnlyList<T> DeserializeTableTagged<T>(
            IOReader reader,
            Type type,
            MetaSerializationFlags flags,
            IGameConfigDataResolver resolver,
            int? logicVersion,
            StringBuilder debugStream = null,
            MetaSerializationSettings? settings = null,
            int maxCollectionSizeOverride = MetaSerializationContext.DefaultMaxCollectionSize)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(flags, resolver, logicVersion, debugStream, settings);
            return instance._taggedSerializerRoslyn.DeserializeTable<T>(ref context, reader, maxCollectionSizeOverride, type);
        }

        public static T CloneTagged<T>(
            T value,
            MetaSerializationFlags flags,
            int? logicVersion,
            IGameConfigDataResolver resolver,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            using (BorrowedIOBuffer buffer = instance.BorrowIOBuffer())
            {
                using (IOWriter writer = new IOWriter(buffer.Buffer))
                    SerializeTagged(writer, value, flags, logicVersion, settings: settings);
                using (IOReader reader = new IOReader(buffer.Buffer))
                    return DeserializeTagged<T>(reader, flags, resolver, logicVersion, settings: settings);
            }
        }

        public static object CloneTagged(
            Type type,
            object obj,
            MetaSerializationFlags flags,
            int? logicVersion,
            IGameConfigDataResolver resolver,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            using (BorrowedIOBuffer buffer = instance.BorrowIOBuffer())
            {
                using (IOWriter writer = new IOWriter(buffer.Buffer))
                    SerializeTagged(
                        writer,
                        type,
                        obj,
                        flags,
                        logicVersion,
                        settings: settings);
                using (IOReader reader = new IOReader(buffer.Buffer))
                    return DeserializeTagged(
                        reader,
                        type,
                        flags,
                        resolver,
                        logicVersion,
                        settings: settings);
            }
        }

        public static IReadOnlyList<T> CloneTableTagged<T>(
            List<T> items,
            MetaSerializationFlags flags,
            int? logicVersion,
            IGameConfigDataResolver resolver,
            MetaSerializationSettings? settings = null,
            int maxCollectionSizeOverride = MetaSerializationContext.DefaultMaxCollectionSize)
        {
            MetaSerialization instance = Instance;
            using (BorrowedIOBuffer buffer = instance.BorrowIOBuffer())
            {
                using (IOWriter writer = new IOWriter(buffer.Buffer))
                    SerializeTableTagged(
                        writer,
                        items,
                        flags,
                        logicVersion,
                        settings: settings,
                        maxCollectionSizeOverride: maxCollectionSizeOverride);
                using (IOReader reader = new IOReader(buffer.Buffer))
                    return DeserializeTableTagged<T>(
                        reader,
                        flags,
                        resolver,
                        logicVersion,
                        settings: settings,
                        maxCollectionSizeOverride: maxCollectionSizeOverride);
            }
        }

        /// <summary>
        /// Resolve MetaRefs that are contained in the object tree rooted at <paramref name="obj"/>.
        /// <para>
        /// The MetaRefs are resolved in-place, and therefore their equalities and hash codes
        /// might change (due to config aliases). For this reason, you should not store MetaRefs
        /// as keys in hash-based collections (e.g. OrderedSet/HashSet or MetaDictionary/Dictionary)
        /// in unresolved form as they are going to get resolved later.
        /// </para>
        /// </summary>
        public static void ResolveMetaRefs(Type type, object obj, IGameConfigDataResolver resolver, MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(MetaSerializationFlags.IncludeAll, resolver, logicVersion: null, debugStream: null, settings);
            instance._taggedSerializerRoslyn.TraverseMetaRefs(ref context, type, obj);
        }

        /// <summary>
        /// Static typing helper for <see cref="ResolveMetaRefs(Type, object, IGameConfigDataResolver, MetaSerializationSettings?)"/>.
        /// </summary>
        public static void ResolveMetaRefs<T>(T value, IGameConfigDataResolver resolver, MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(MetaSerializationFlags.IncludeAll, resolver, logicVersion: null, debugStream: null, settings);
            instance._taggedSerializerRoslyn.TraverseMetaRefs<T>(ref context, value);
        }


        public static void TraverseMetaRefs(
            Type type,
            ref object obj,
            IGameConfigDataResolver resolver,
            MetaSerializationMetaRefTraversalParams metaRefTraversal,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(
                MetaSerializationFlags.IncludeAll,
                resolver,
                logicVersion: null,
                debugStream: null,
                settings,
                metaRefTraversal);
            instance._taggedSerializerRoslyn.TraverseMetaRefs(ref context, type, obj);
        }

        public static void TraverseMetaRefs<T>(ref T value, IGameConfigDataResolver resolver, MetaSerializationMetaRefTraversalParams metaRefTraversal, MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(
                MetaSerializationFlags.IncludeAll,
                resolver,
                logicVersion: null,
                debugStream: null,
                settings,
                metaRefTraversal);
            instance._taggedSerializerRoslyn.TraverseMetaRefs<T>(ref context, value);
        }

        /// <summary>
        /// Like <see cref="ResolveMetaRefs{T}(T, IGameConfigDataResolver, MetaSerializationSettings?)"/> but operates on
        /// a list of config data items that will be traversed into (instead of the default behavior of treating
        /// config data items as config data references).
        /// </summary>
        public static void ResolveMetaRefsInTable<T>(List<T> items, IGameConfigDataResolver resolver, MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(MetaSerializationFlags.IncludeAll, resolver, logicVersion: null, debugStream: null, settings);
            instance._taggedSerializerRoslyn.TraverseMetaRefsInTable(ref context, items);
        }

        /// <param name="itemsList">
        /// A <c>List&lt;TInfo&gt;</c> where <c>typeof(TInfo)</c> is <paramref name="itemType"/>.
        /// </param>
        public static void TraverseMetaRefsInTable(
            Type itemType,
            object itemsList,
            IGameConfigDataResolver resolver,
            MetaSerializationMetaRefTraversalParams metaRefTraversal,
            MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(
                MetaSerializationFlags.IncludeAll,
                resolver,
                logicVersion: null,
                debugStream: null,
                settings,
                metaRefTraversal);
            instance._taggedSerializerRoslyn.TraverseMetaRefsInTable(ref context, itemType, itemsList);
        }

        public static void TraverseMetaRefsInTable<T>(List<T> items, IGameConfigDataResolver resolver, MetaSerializationMetaRefTraversalParams metaRefTraversal, MetaSerializationSettings? settings = null)
        {
            MetaSerialization instance = Instance;
            MetaSerializationContext context = instance.CreateContext(
                MetaSerializationFlags.IncludeAll,
                resolver,
                logicVersion: null,
                debugStream: null,
                settings,
                metaRefTraversal);
            instance._taggedSerializerRoslyn.TraverseMetaRefsInTable(ref context, items);
        }
    }
}
