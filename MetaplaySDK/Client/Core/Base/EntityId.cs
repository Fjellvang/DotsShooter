// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using System;
using System.IO;
using static System.FormattableString;

namespace Metaplay.Core
{
    /// <summary>
    /// Uniquely identifies an Entity in the system (eg, Player, Session, or Guild).
    /// Used for routing messages within the server cluster.
    ///
    /// Consists of a 10-bit EntityKind and a 58-bit Value.
    /// </summary>
    [MetaSerializable]
    public struct EntityId : IComparable<EntityId>, IEquatable<EntityId>, IComparable
    {
        /// <summary> Number of bits used in Value </summary>
        public const int    NumValueBits    = 58;
        /// <summary> Maximum value for an EntityId (exclusive) </summary>
        public const ulong  MaxValue        = (1ul << NumValueBits);
        /// <summary> Mask of bits valid in a Value </summary>
        public const ulong  ValueMask       = MaxValue - 1;

        [MetaMember(1)] ulong   _rawValue;  // 58-bit value (legacy serialized values can have the EntityKind in the top-6 bits)
        [MetaMember(2)] ushort  _rawKind;   // 10-bit EntityKind

        public readonly EntityKind Kind  => EntityKind.FromValue(_rawKind);
        public readonly ulong      Value => _rawValue;

        private EntityId(ushort kind, ulong value)
        {
            if (kind >= EntityKind.MaxValue)
                throw new ArgumentOutOfRangeException($"EntityKind value {kind} must be smaller than {EntityKind.MaxValue}");
            if (value >= MaxValue)
                throw new ArgumentOutOfRangeException($"Value {value} must be smaller than {MaxValue}");

            _rawKind = kind;
            _rawValue = value;
        }

        [MetaOnDeserialized]
        void OnDeserialized()
        {
            // If _rawValue is any of its EntityKind bits set, we know it's a legacy value that needs to be converted.
            // \note This does not trigger for legacy values with EntityKind==0 but those values don't need conversion.
            if (_rawValue >= MaxValue)
            {
                _rawKind = (ushort)(_rawValue >> NumValueBits);
                _rawValue &= ValueMask;
            }

            // Check that we didn't deserialize invalid values
            if (_rawKind >= EntityKind.MaxValue)
                throw new InvalidDataException($"Deserialized an invalid EntityKind value ({_rawKind}), maximum value is {EntityKind.MaxValue}");
            if (_rawValue >= MaxValue)
                throw new InvalidDataException($"Deserialized an invalid EntityId.Value ({_rawValue}), maximum value is {MaxValue}");
        }

        public static EntityId None => Create(EntityKind.None, 0);

        public static EntityId Create(EntityKind kind, ulong value)
        {
            if (kind == EntityKind.None && value != 0)
                throw new ArgumentException("Value must be zero for EntityKind.None", nameof(value));
            if (kind.Value < 0 || kind.Value >= EntityKind.MaxValue)
                throw new ArgumentException($"Invalid EntityKind value {kind.Value}", nameof(kind));
            if (value < 0 || value >= MaxValue)
                throw new ArgumentException($"Invalid EntityId value {kind}:{value} (value must be smaller than {MaxValue})", nameof(value));

            return new EntityId((ushort)kind.Value, value);
        }

        /// <summary>
        /// Only intended for testing purposes, don't use otherwise!
        /// </summary>
        internal static EntityId CreateUncheckedTestingOnly(EntityKind kind, ulong value) =>
            new EntityId((ushort)kind.Value, value);

        public static EntityId CreateRandom(EntityKind kind)
        {
            // \note not guaranteed to be unique, must check uniqueness after creating
            // \note guid provides much better randomness than simple Random.Next()
            ulong value = ((ulong)Guid.NewGuid().GetHashCode() << 32) + (ulong)Guid.NewGuid().GetHashCode();
            return new EntityId((ushort)kind.Value, value & ValueMask);
        }

        public static EntityId ParseFromString(string str)
        {
            if (str is null)
                throw new ArgumentNullException(nameof(str), "Cannot parse null string into EntityId");

            if (!TryParseFromString(str, out EntityId entityId, out string errorStr))
                throw new FormatException(errorStr);

            return entityId;
        }

        /// <summary>
        /// Tries to parse an <see cref="EntityId"/> out of an entity id string. Returns false if unsuccessful. Guaranteed to not throw.
        /// </summary>
        /// <param name="str">The entity id string to parse.</param>
        /// <param name="entityId"> The resulting entity ID. EntityId.None if parsing was unsuccessful.</param>
        /// <param name="errorStr"> If unsuccessful, the error string describes why parsing failed.</param>
        /// <returns>True if successfully parsed, false otherwise.</returns>
        public static bool TryParseFromString(string str, out EntityId entityId, out string errorStr)
        {
            entityId = None;
            errorStr = null;

            if (string.IsNullOrEmpty(str))
            {
                errorStr = "Cannot parse null or empty string into EntityId";
                return false;
            }

            if (str == "None")
            {
                return true;
            }

            string[] parts = str.Split(':');
            if (parts.Length != 2)
            {
                errorStr = Invariant($"Invalid EntityId format '{str}'");
                return false;
            }

            // None must not have value
            if (parts[0] == "None")
            {
                errorStr = Invariant($"EntityId None must not have value '{str}'");
                return false;
            }

            // Parse kind
            if (!EntityKindRegistry.TryFromName(parts[0], out EntityKind kind))
            {
                errorStr = Invariant($"Invalid EntityKind in {str}");
                return false;
            }

            // Parse unique id
            if (!TryParseValue(parts[1], out ulong id, out string valueErrorStr))
            {
                errorStr = valueErrorStr;
                return false;
            }

            if (id >= MaxValue)
            {
                errorStr = Invariant($"Invalid value in {str}");
                return false;
            }
            
            entityId = Create(kind, id);
            return true;
        }

        /// <summary>
        /// Parses EntityId from string, and validates the Kind is <paramref name="expectedKind"/>. On failure
        /// throws FormatException. Note that there is no special case for <see cref="EntityKind.None"/> kind.
        /// It is not accepted unless <paramref name="expectedKind"/> is <see cref="EntityKind.None"/>.
        /// </summary>
        public static EntityId ParseFromStringWithKind(EntityKind expectedKind, string str)
        {
            EntityId entityId = ParseFromString(str);
            if (entityId.Kind != expectedKind)
                throw new FormatException($"Illegal EntityKind in {str}");
            return entityId;
        }

        public static bool operator ==(EntityId a, EntityId b) => a._rawKind == b._rawKind && a._rawValue == b._rawValue;
        public static bool operator !=(EntityId a, EntityId b) => a._rawKind != b._rawKind || a._rawValue != b._rawValue;

        public readonly bool Equals(EntityId other) => this == other;

        public override readonly bool Equals(object obj) => (obj is EntityId) ? (this == (EntityId)obj) : false;

        public readonly int CompareTo(EntityId other)
        {
            if (_rawKind < other._rawKind)
                return -1;
            else if (_rawKind > other._rawKind)
                return +1;
            else if (_rawValue < other._rawValue)
                return -1;
            else if (_rawValue > other._rawValue)
                return +1;
            else
                return 0;
        }

        /// <summary>
        /// Check if the EntityId is a valid one:
        /// - The EntityKind is a valid, existing value
        /// - The EntityId is not a None (including illegal Nones with Value != 0)
        /// </summary>
        public readonly bool IsValid
        {
            get
            {
                // \note Also return false for invalid EntityIds where Kind == None and Value != 0
                EntityKind kind = Kind;
                return EntityKindRegistry.IsValid(kind) && (kind != EntityKind.None);
            }
        }

        public readonly bool IsOfKind(EntityKind kind) => Kind == kind;

        // \note manually calculated for 58 bits of id
        public const string ValidIdCharacters       = "023456789ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"; // 1, I, and l omitted to avoid confusion
        public const int    NumValidIdCharacters    = 59; // must be ValidIdCharacters.Length
        public const int    IdLength                = 10;

        public static string ValueToString(ulong val)
        {
            // \todo [petri] only do these once somewhere
            MetaDebug.Assert(ValidIdCharacters.Length == NumValidIdCharacters, "NumValidCharacters != ValidIdCharacters.Length");
            MetaDebug.Assert(System.Math.Pow(NumValidIdCharacters, IdLength) >= (1ul << NumValueBits), "NumIdCharacters is too small");
            MetaDebug.Assert(System.Math.Pow(NumValidIdCharacters, IdLength - 1) < (1ul << NumValueBits), "NumIdCharacters is too large");

            Span<char> chars = stackalloc char[IdLength];
            for (int ndx = IdLength - 1; ndx >= 0; ndx--)
            {
                chars[ndx] = ValidIdCharacters[(int)(val % NumValidIdCharacters)];
                val /= NumValidIdCharacters;
            }
            MetaDebug.Assert(val == 0, "Remainder left when converting id to string");
            return new string(chars);
        }

        static bool TryParseValue(string str, out ulong value, out string errorStr)
        {
            value = 0;
            errorStr = null;

            if (str.Length != IdLength)
            {
                errorStr = Invariant($"EntityId values are required to be exactly {IdLength} characters, got {str.Length} in '{str}'");
                return false;
            }

            ulong id = 0;
            for (int ndx = 0; ndx < IdLength; ndx++)
            {
                int v = ValidIdCharacters.IndexOf(str[ndx]);
                if (v == -1)
                {
                    errorStr = Invariant($"Invalid EntityId character '{str[ndx]}'");
                    return false;
                }
                id = id * NumValidIdCharacters + (uint)v;
            }

            if (id < 0 || id >= MaxValue)
            {
                errorStr = Invariant($"Invalid EntityId value '{str}'");
                return false;
            }

            value = id;
            return true;
        }

        /// <summary>
        /// For an EntityId <c>"Kind:ValueString"</c> returns <c>(Kind, ValueString)</c>.
        /// </summary>
        public readonly (string, string) GetKindValueStrings() => (Kind.ToString(), ValueToString(Value));

        public override readonly int GetHashCode() => Util.CombineHashCode(_rawKind.GetHashCode(), _rawValue.GetHashCode());

        public override readonly string ToString()
        {
            EntityKind kind = Kind;
            if (kind == EntityKind.None)
                return (Value == 0) ? "None" : $"InvalidNone:{ValueToString(Value)}"; // Value != 0 is an invalid value
            else
                return $"{kind}:{ValueToString(Value)}";
        }

        int IComparable.CompareTo(object obj) => (obj is EntityId other) ? CompareTo(other) : 1;
    }
}
