// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace Metaplay.Core
{
    class EntityKindTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
                return true;

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string str)
                return EntityKind.FromName(str);

            return base.ConvertFrom(context, culture, value);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(string))
                return true;

            return base.CanConvertTo(context, destinationType);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is EntityKind entityKind)
                return entityKind.ToString();

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    /// <summary>
    /// Dynamic enumeration identifying a family of game entities. The entity kinds are used in
    /// <see cref="EntityId"/> with combination of value to uniquely identify entities.
    ///
    /// Typical game entity examples include Player, Guild, Matchmaker and Match. On the backend,
    /// there are additional entities not visible to the client, for example: InAppValidator,
    /// GlobalStateManager, PushNotifier, and so on.
    ///
    /// To introduce game-specific <see cref="EntityKind"/>s, please add them to your game's
    /// <c>EntityKindGame</c> class where you can register the EntityKinds and their values.
    /// </summary>
    [TypeConverter(typeof(EntityKindTypeConverter))]
    public struct EntityKind : IEquatable<EntityKind>, IComparable<EntityKind>
    {
        public const int MaxValue = 1024; // 10 bits are used for storing EntityKind in EntityId (exclusive)

        public static readonly EntityKind None = FromValue(0); // \note This doesn't get included in the registry!

        public readonly int Value;

        public readonly string Name => EntityKindRegistry.GetName(this);

        public EntityKind(int value)
        {
            if (value < 0 || value >= MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value));

            Value = value;
        }

        public static EntityKind FromValue(int value) => new EntityKind(value);
        public static EntityKind FromName(string name) => EntityKindRegistry.FromName(name);

        public static bool operator ==(EntityKind a, EntityKind b) => a.Value == b.Value;
        public static bool operator !=(EntityKind a, EntityKind b) => a.Value != b.Value;

        public bool Equals(EntityKind other) => Value == other.Value;
        public int CompareTo(EntityKind other) => Value.CompareTo(other.Value);

        public override readonly bool Equals(object obj) => (obj is EntityKind other) ? (this == other) : false;
        public override readonly int GetHashCode() => Value;
        public override readonly string ToString() => Name;
    }

    /// <summary>
    /// Set of multiple <see cref="EntityKind"/>s. Stored internally as an array of ulongs (128 bytes in total).
    /// Don't use in high-performance code!
    /// </summary>
    public readonly struct EntityKindMask : IEquatable<EntityKindMask>
    {
        const int VectorSize = (EntityKind.MaxValue + 63) / 64;

        // Accessor for _rawVector that defaults to all-zero vector
        readonly ulong[] Vector => _rawVector ?? s_noneVector;

        // Bitvector with a bit for each EntityKind -- can be null for empty masks
        readonly ulong[] _rawVector;

        // Statically allocated all-zero vector for empty masks
        static readonly ulong[] s_noneVector = new ulong[VectorSize];

        /// <summary>Empty mask, no values set.</summary>
        public static readonly EntityKindMask None = new EntityKindMask(s_noneVector);

        /// <summary>Mask of all valid <see cref="EntityKind"/>s (defined by the SDK or the project).</summary>
        public static readonly EntityKindMask All = new EntityKindMask(EntityKindRegistry.AllValues.Where(kind => kind != EntityKind.None));

        /// <summary>Return true if the set is empty, i.e., no EntityKinds are set.</summary>
        public readonly bool IsEmpty => _rawVector == null || _rawVector.All(val => val == 0);

        EntityKindMask(ulong[] vector)
        {
            if (vector.Length != VectorSize)
                throw new ArgumentException($"The input vector must be {VectorSize} elements long, got {vector.Length}");
            _rawVector = vector;
        }

        public EntityKindMask(IEnumerable<EntityKind> kinds)
        {
            _rawVector = new ulong[VectorSize];
            foreach (EntityKind kind in kinds)
            {
                int value = kind.Value;
                int ndx = value >> 6;
                int offset = value & 63;
                _rawVector[ndx] |= 1ul << offset;
            }
        }

        public static EntityKindMask FromEntityKind(EntityKind entityKind) =>
            new EntityKindMask(new EntityKind[] { entityKind });

        public readonly IEnumerable<EntityKind> GetKinds()
        {
            ulong[] vector = Vector;
            for (int value = 0; value < EntityKind.MaxValue; value++)
            {
                int ndx = value >> 6;
                int offset = value & 63;
                if ((vector[ndx] & (1ul << offset)) != 0)
                    yield return EntityKind.FromValue(value);
            }
        }

        public static bool operator ==(EntityKindMask a, EntityKindMask b) => a.Vector.SequenceEqual(b.Vector);
        public static bool operator !=(EntityKindMask a, EntityKindMask b) => !a.Vector.SequenceEqual(b.Vector);
        public static EntityKindMask operator &(EntityKindMask a, EntityKindMask b) => new EntityKindMask(Intersect(a.Vector, b.Vector));
        public static EntityKindMask operator |(EntityKindMask a, EntityKindMask b) => new EntityKindMask(Union(a.Vector, b.Vector));
        public static EntityKindMask Except(EntityKindMask a, EntityKindMask b) => new EntityKindMask(Except(a.Vector, b.Vector));

        static ulong[] Intersect(ulong[] a, ulong[] b)
        {
            ulong[] res = new ulong[VectorSize];
            for (int ndx = 0; ndx < VectorSize; ndx++)
                res[ndx] = a[ndx] & b[ndx];
            return res;
        }

        static ulong[] Union(ulong[] a, ulong[] b)
        {
            ulong[] res = new ulong[VectorSize];
            for (int ndx = 0; ndx < VectorSize; ndx++)
                res[ndx] = a[ndx] | b[ndx];
            return res;
        }

        static ulong[] Except(ulong[] a, ulong[] b)
        {
            ulong[] res = new ulong[VectorSize];
            for (int ndx = 0; ndx < VectorSize; ndx++)
                res[ndx] = a[ndx] & ~b[ndx];
            return res;
        }

        public readonly bool IsSet(EntityKind kind)
        {
            int value = kind.Value;
            int ndx = value >> 6;
            int offset = value & 63;
            return (Vector[ndx] & (1ul << offset)) != 0;
        }

        public readonly bool Equals(EntityKindMask other) => this == other;
        public override readonly bool Equals(object obj) => (obj is EntityKindMask other) ? this == other : false;
        public override readonly int GetHashCode() => Util.ComputeSequenceHash(Vector);
        public override readonly string ToString() => string.Join(" | ", GetKinds().Select(kind => kind.Name));
    }
}
