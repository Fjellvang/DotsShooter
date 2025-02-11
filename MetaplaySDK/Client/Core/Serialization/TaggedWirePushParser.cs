// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.IO;
using System;

namespace Metaplay.Core.Serialization
{
    /// <summary>
    /// Push parser for tagged wire serialization data. Parser parses the input in one go, pushing the parse
    /// events to callbacks as it parses. This is essentially a SAX parser but for Tagged Serialization format.
    /// </summary>
    public abstract class TaggedWirePushParser
    {
        public class ParseError : Exception
        {
            public ParseError(string message) : base(message)
            {
            }
        }

        /// <summary>
        /// Parses the data with the reader
        /// </summary>
        protected void Parse(IOReader reader)
        {
            try
            {
                ParseTopLevel(reader);
            }
            catch (Exception ex)
            {
                if (!OnError(ex))
                    throw;
            }
        }

        protected virtual void OnEnd(IOReader reader) { }

        /// <summary>
        /// Returns true if error was handled. If this returns false, the exception is thrown from <see cref="Parse"/>.
        /// </summary>
        protected virtual bool OnError(Exception ex) => false;
        protected virtual void OnBeginParsePrimitive(IOReader reader, WireDataType wireType) { }
        protected virtual void OnEndParsePrimitive(IOReader reader, WireDataType wireType, object obj) { }
        protected virtual void OnBeginParseAbstractStruct(IOReader reader, int typecode) { }
        protected virtual void OnBeginParseNullableStruct(IOReader reader, bool isNotNull) { }
        protected virtual void OnBeginParseStruct(IOReader reader) { }
        protected virtual void OnEndParseStruct(IOReader reader) { }
        protected virtual void OnBeginParseValueCollection(IOReader reader, int numElements, WireDataType valueType) { }
        protected virtual void OnEndParseValueCollection(IOReader reader) { }
        protected virtual void OnBeginParseKeyValueCollection(IOReader reader, int numElements, WireDataType keyType, WireDataType valueType) { }
        protected virtual void OnEndParseKeyValueCollection(IOReader reader) { }
        protected virtual void OnBeginParseStructMember(IOReader reader, WireDataType wireType, int tagId) { }
        protected virtual void OnEndParseStructMember(IOReader reader) { }
        protected virtual void OnBeginParseCollectionElement(IOReader reader, int ndx) { }
        protected virtual void OnEndParseCollectionElement(IOReader reader) { }
        protected virtual void OnBeginParseCollectionKey(IOReader reader, int ndx) { }
        protected virtual void OnEndParseCollectionKey(IOReader reader) { }
        protected virtual void OnBeginParseCollectionValue(IOReader reader, int ndx) { }
        protected virtual void OnEndParseCollectionValue(IOReader reader) { }
        protected virtual void OnBeginParseNullablePrimitive(IOReader reader, WireDataType dataType, bool isNotNull) { }
        protected virtual void OnEndParseNullablePrimitive(IOReader reader) { }

        void ParseTopLevel(IOReader reader)
        {
            WireDataType topLevelWireType = TaggedWireSerializer.ReadWireType(reader);
            ParseWireObjectContents(reader, topLevelWireType);
            OnEnd(reader);
        }

        void ParseWireObjectContents(IOReader reader, WireDataType wireType)
        {
            switch (wireType)
            {
                case WireDataType.Null:         OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, null);                               break;
                case WireDataType.VarInt:       OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadVarLong());               break;
                case WireDataType.VarInt128:    OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadVarUInt128());            break;
                case WireDataType.F32:          OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadF32());                   break;
                case WireDataType.F32Vec2:      OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadF32Vec2());               break;
                case WireDataType.F32Vec3:      OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadF32Vec3());               break;
                case WireDataType.F64:          OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadF64());                   break;
                case WireDataType.F64Vec2:      OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadF64Vec2());               break;
                case WireDataType.F64Vec3:      OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadF64Vec3());               break;
                case WireDataType.Float32:      OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadFloat());                 break;
                case WireDataType.Float64:      OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadDouble());                break;
                case WireDataType.String:       OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadString(64 * 1024 * 1024));        break;
                case WireDataType.Bytes:        OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, reader.ReadByteString(64 * 1024 * 1024));    break;
                case WireDataType.MetaGuid:     OnBeginParsePrimitive(reader, wireType); OnEndParsePrimitive(reader, wireType, new MetaGuid(reader.ReadUInt128())); break;

                case WireDataType.AbstractStruct:
                {
                    int typeCode = reader.ReadVarInt();
                    OnBeginParseAbstractStruct(reader, typeCode);
                    if (typeCode == 0)
                    {
                        // null
                    }
                    else if (typeCode > 0)
                        ParseStructContents(reader);
                    else
                        throw new ParseError($"Invalid AbstractStruct typecode. Must be non-negative, got {typeCode}");
                    OnEndParseStruct(reader);
                    break;
                }

                case WireDataType.NullableStruct:
                {
                    bool isNotNull = ParseIsNotNullFlag(reader);
                    OnBeginParseNullableStruct(reader, isNotNull);
                    if (isNotNull)
                        ParseStructContents(reader);
                    OnEndParseStruct(reader);
                    break;
                }
                case WireDataType.Struct:
                {
                    OnBeginParseStruct(reader);
                    ParseStructContents(reader);
                    OnEndParseStruct(reader);
                    break;
                }

                case WireDataType.ValueCollection:
                {
                    int numElements = reader.ReadVarInt();
                    if (numElements == -1)
                    {
                        OnBeginParseValueCollection(reader, numElements, WireDataType.Invalid);
                    }
                    else if (numElements >= 0)
                    {
                        WireDataType elementType = TaggedWireSerializer.ReadWireType(reader);
                        OnBeginParseValueCollection(reader, numElements, elementType);
                        for (int ndx = 0; ndx < numElements; ndx++)
                        {
                            OnBeginParseCollectionElement(reader, ndx);
                            ParseWireObjectContents(reader, elementType);
                            OnEndParseCollectionElement(reader);
                        }
                    }
                    else
                        throw new ParseError($"Invalid ValueCollection count. Must be non-negative, got {numElements}");
                    OnEndParseValueCollection(reader);
                    break;
                }

                case WireDataType.KeyValueCollection:
                {
                    int numElements = reader.ReadVarInt();
                    if (numElements == -1)
                    {
                        OnBeginParseKeyValueCollection(reader, numElements, WireDataType.Invalid, WireDataType.Invalid);
                    }
                    else if (numElements >= 0)
                    {
                        WireDataType keyType = TaggedWireSerializer.ReadWireType(reader);
                        WireDataType valueType = TaggedWireSerializer.ReadWireType(reader);
                        OnBeginParseKeyValueCollection(reader, numElements, keyType, valueType);
                        for (int ndx = 0; ndx < numElements; ndx++)
                        {
                            OnBeginParseCollectionKey(reader, ndx);
                            ParseWireObjectContents(reader, keyType);
                            OnEndParseCollectionKey(reader);

                            OnBeginParseCollectionValue(reader, ndx);
                            ParseWireObjectContents(reader, valueType);
                            OnEndParseCollectionValue(reader);
                        }
                    }
                    else
                        throw new ParseError($"Invalid KeyValueCollection count. Must be non-negative, got {numElements}");
                    OnEndParseKeyValueCollection(reader);
                    break;
                }

                case WireDataType.NullableVarInt:
                case WireDataType.NullableVarInt128:
                case WireDataType.NullableF32:
                case WireDataType.NullableF32Vec2:
                case WireDataType.NullableF32Vec3:
                case WireDataType.NullableF64:
                case WireDataType.NullableF64Vec2:
                case WireDataType.NullableF64Vec3:
                case WireDataType.NullableFloat32:
                case WireDataType.NullableFloat64:
                case WireDataType.NullableMetaGuid:
                {
                    WireDataType type = TaggedWireSerializer.NullablePrimitiveWireTypeUnwrap(wireType);
                    bool isNotNull = ParseIsNotNullFlag(reader);

                    OnBeginParseNullablePrimitive(reader, wireType, isNotNull);
                    if (isNotNull)
                        ParseWireObjectContents(reader, type);
                    else
                    {
                        OnBeginParsePrimitive(reader, wireType);
                        OnEndParsePrimitive(reader, wireType, null);
                    }
                    OnEndParseNullablePrimitive(reader);
                    break;
                }

                default:
                    throw new ParseError($"invalid wireformat: got {wireType}");
            }
        }

        void ParseStructContents(IOReader reader)
        {
            for (;;)
            {
                WireDataType memberType = TaggedWireSerializer.ReadWireType(reader);
                if (memberType == WireDataType.EndStruct)
                    break;

                int tagId = reader.ReadVarInt();
                if (tagId < 0)
                    throw new ParseError($"Invalid struct member tagId. Must be non-negative, got {tagId}");

                OnBeginParseStructMember(reader, memberType, tagId);
                ParseWireObjectContents(reader, memberType);
                OnEndParseStructMember(reader);
            }
        }

        bool ParseIsNotNullFlag(IOReader reader)
        {
            int isNotNull = reader.ReadByte();
            if (isNotNull == 0)
                return false;
            else if (isNotNull == 2) // true == -1 => swizzled into uint => 2
                return true;
            else
                throw new ParseError($"Invalid null-flag. Must be bool, got {isNotNull}");
        }
    }
}
