// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Analytics;
using Metaplay.Core.IO;
using Metaplay.Core.Json;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using static System.FormattableString;

namespace Metaplay.Core.EventLog
{
    /// <summary>
    /// Entry type specific for an entity's event log.
    /// </summary>
    [MetaSerializable]
    public abstract class EntityEventLogEntry<TPayload, TPayloadDeserializationFailureSubstitute> : MetaEventLogEntry
        where TPayload : EntityEventBase
        where TPayloadDeserializationFailureSubstitute : TPayload, IEntityEventPayloadDeserializationFailureSubstitute, new()
    {
        /// <summary> Model's CurrentTime at the time of the event. </summary>
        [MetaMember(1)] public MetaTime ModelTime               { get; private set; }
        /// <summary> Schema version for the payload type. </summary>
        [MetaMember(3)] public int      PayloadSchemaVersion    { get; private set; }
        /// <summary> Event-specific payload. </summary>
        [MetaOnMemberDeserializationFailure(nameof(CreatePayloadDeserializationFailureSubstitute))]
        [MetaMember(2)] public TPayload Payload                 { get; private set; }
        [MetaOnMemberDeserializationFailure(nameof(CreateContextDeserializationFailureSubstitute))]
        [MetaMember(4)] public AnalyticsContextBase Context { get; private set; }
        // \note Label ids are stored as `int`s instead of `AnalyticsLabel`s here,
        //       so that deserialization does not fail if an `AnalyticsLabel` is removed/changed.
        //       We convert to human-facing strings in LabelsForDashboard below (and tolerate missing labels).
        [JsonIgnore] [MetaMember(5)] public MetaDictionary<int, string> LabelsAsInts { get; private set; }

        [JsonProperty("labels")]
        public MetaDictionary<string, string> LabelsForDashboard
        {
            get
            {
                if (LabelsAsInts == null)
                    return null;

                MetaDictionary<string, string> result = new(capacity: LabelsAsInts.Count);
                foreach ((int labelInt, string value) in LabelsAsInts)
                {
                    string labelString;

                    if (AnalyticsLabel.TryFromId(labelInt, out AnalyticsLabel label))
                        labelString = label.Name;
                    else if (labelInt == -1)
                        labelString = null;
                    else
                        labelString = Invariant($"UnknownLabel#{labelInt}");

                    result.Add(labelString, value);
                }

                return result;
            }
        }

        protected EntityEventLogEntry(){ }
        public EntityEventLogEntry(BaseParams baseParams, MetaTime modelTime, int payloadSchemaVersion, TPayload payload, AnalyticsContextBase context, MetaDictionary<int, string> labelsAsInts) : base(baseParams)
        {
            ModelTime               = modelTime;
            PayloadSchemaVersion    = payloadSchemaVersion;
            Payload                 = payload;
            Context                 = context;
            LabelsAsInts            = labelsAsInts;
        }

        public static TPayloadDeserializationFailureSubstitute CreatePayloadDeserializationFailureSubstitute(MetaMemberDeserializationFailureParams failureParams)
        {
            TPayloadDeserializationFailureSubstitute substitute = new TPayloadDeserializationFailureSubstitute();
            substitute.Initialize(failureParams);
            return substitute;
        }

        public static AnalyticsContextDeserializationFailureSubstitute CreateContextDeserializationFailureSubstitute(MetaMemberDeserializationFailureParams failureParams)
        {
            return new AnalyticsContextDeserializationFailureSubstitute(new AbstractTypeDeserializationFailureInfo<AnalyticsContextBase>(failureParams));
        }
    }

    [MetaSerializableDerived(10)]
    public class AnalyticsContextDeserializationFailureSubstitute : AnalyticsContextBase
    {
        [MetaMember(1)] public AbstractTypeDeserializationFailureInfo<AnalyticsContextBase> Info { get; }

        [MetaDeserializationConstructor]
        public AnalyticsContextDeserializationFailureSubstitute(AbstractTypeDeserializationFailureInfo<AnalyticsContextBase> info)
        {
            Info = info;
        }
    }

    public interface IEntityEventPayloadDeserializationFailureSubstitute
    {
        void Initialize(MetaMemberDeserializationFailureParams failureParams);
    }

    /// <summary>
    /// Base class for entity-specific analytics events, both Metaplay core and
    /// game-specific event types.
    /// </summary>
    [MetaAllowNoSerializedMembers]
    [MetaSerializable]
    public abstract class EntityEventBase : AnalyticsEventBase
    {
        /// <summary> Human-readable description of the event, shown in the dashboard. </summary>
        [JsonIgnore]
        public abstract string EventDescription { get; }

        [JsonProperty("eventDescription")]
        [IncludeOnlyInJsonSerializationMode(JsonSerializationMode.AdminApi)]
        public string EventDescriptionErrorWrapper
        {
            get
            {
                try
                {
                    return EventDescription;
                }
                catch (Exception ex)
                {
                    return $"Failed to get description string: {ex.Message}";
                }
            }
        }
    }

    [MetaSerializable]
    public abstract class EntityEventLog<TPayload, TPayloadDeserializationFailureSubstitute, TEntry> : MetaEventLog<TEntry>
        where TPayload : EntityEventBase
        where TPayloadDeserializationFailureSubstitute : TPayload, IEntityEventPayloadDeserializationFailureSubstitute, new()
        where TEntry : EntityEventLogEntry<TPayload, TPayloadDeserializationFailureSubstitute>
    {
    }

    /// <summary>
    /// Serializable class storing information about a failure of deserializing
    /// an object via base class <typeparamref name="TPayloadBase"/>.
    /// Constructed from <see cref="MetaMemberDeserializationFailureParams"/>,
    /// this will extract information from the params and store that as serializable data.
    /// </summary>
    [MetaSerializable]
    public class AbstractTypeDeserializationFailureInfo<TPayloadBase>
    {
        const int MaxPayloadBytesRetained = 10 * 1024; // Limit number of bytes stored in the substitute, to avoid unbounded bloating in hypothetical case of recurring deserialization failure of substitute itself

        [MetaMember(1)] public int              PayloadBytesLength          { get; private set; }
        [MetaMember(2)] public byte[]           PayloadBytesTruncated       { get; private set; }
        [MetaMember(8)] public string           PayloadTypeName             { get; private set; } = null;
        [MetaMember(3)] public string           ExceptionType               { get; private set; }
        [MetaMember(4)] public string           ExceptionMessage            { get; private set; }
        [MetaMember(5)] public int?             UnknownClassTypeCode        { get; private set; }
        [MetaMember(6)] public int?             UnexpectedWireDataTypeValue { get; private set; }
        [MetaMember(7)] public string           UnexpectedWireDataTypeName  { get; private set; }

        [IgnoreDataMember] public string Description => $"{ExceptionType} occurred while deserializing type {PayloadTypeName}: {ExceptionMessage}";

        AbstractTypeDeserializationFailureInfo(){ }
        public AbstractTypeDeserializationFailureInfo(MetaMemberDeserializationFailureParams failureParams)
        {
            PayloadBytesLength          = failureParams.MemberPayload.Length;
            PayloadBytesTruncated       = failureParams.MemberPayload.Take(MaxPayloadBytesRetained).ToArray();
            PayloadTypeName             = PeekPayloadTypeName(PayloadBytesTruncated, failureParams.TypeInfo);

            ExceptionType               = failureParams.Exception?.GetType().Name;
            ExceptionMessage            = failureParams.Exception?.Message;

            UnknownClassTypeCode        = (failureParams.Exception as MetaUnknownDerivedTypeDeserializationException)?.EncounteredTypeCode;

            WireDataType? unexpectedWireDataType = (failureParams.Exception as MetaWireDataTypeMismatchDeserializationException)?.EncounteredWireDataType;
            UnexpectedWireDataTypeValue = (int?)unexpectedWireDataType;
            UnexpectedWireDataTypeName  = unexpectedWireDataType?.ToString();
        }

        [MetaOnDeserialized]
        void EnsurePayloadTypeNameIsSet(MetaOnDeserializedParams p)
        {
            if (PayloadTypeName == null)
                PayloadTypeName = PeekPayloadTypeName(PayloadBytesTruncated, p.TypeInfo);
        }

        static string PeekPayloadTypeName(byte[] payloadBytesTruncated, MetaSerializerTypeRegistry typeInfo)
        {
            try
            {
                int typeCode;
                using (IOReader reader = new IOReader(payloadBytesTruncated))
                    typeCode = reader.ReadVarInt();

                MetaSerializableType baseTypeSpec = typeInfo.GetSerializableType(typeof(TPayloadBase));
                Type concreteType = baseTypeSpec.DerivedTypes[typeCode];
                return concreteType.Name;
            }
            catch
            {
                return "<unknown>";
            }
        }
    }
}
