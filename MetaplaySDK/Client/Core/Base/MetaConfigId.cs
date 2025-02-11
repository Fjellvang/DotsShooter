// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using System;
using System.Reflection;
using static System.FormattableString;

namespace Metaplay.Core
{
    public interface IMetaConfigId : IMetaRefBase
    {
        object ResolveRefObject(IGameConfigDataResolver resolver);
        object TryResolveRefObject(IGameConfigDataResolver resolver);
    }

    /// <summary>
    /// An alternative to <see cref="MetaRef{TItem}"/> to be used when you don't want the reference to be resolved at config specialization creation time,
    /// but instead the reference will need to be resolved with <see cref="GetItem"/> or <see cref="TryGetItem"/> at the use site and the current game config
    /// will need to supplied at that resolve time.
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public class MetaConfigId<TItem> : IMetaConfigId where TItem : class, IGameConfigData
    {
        public MetaConfigId(object key)
        {
            CheckKeyValidity(key);
            KeyObject = key;
        }

        public Type   ItemType  => typeof(TItem);
        public object KeyObject { get; }
        
        static readonly Type _keyType =
            typeof(TItem)
                .GetGenericInterfaceTypeArguments(typeof(IHasGameConfigKey<>))[0];

        object IMetaConfigId.ResolveRefObject(IGameConfigDataResolver resolver)
        {
            TItem item = (TItem)resolver.TryResolveReference(ItemType, KeyObject);
            if (item == null)
                throw new InvalidOperationException(Invariant($"Encountered a {GetType().ToGenericTypeString()} reference to unknown item '{KeyObject}'"));

            return item;
        }

        object IMetaConfigId.TryResolveRefObject(IGameConfigDataResolver resolver)
        {
            return (TItem)resolver.TryResolveReference(ItemType, KeyObject);
        }

        /// <summary>
        /// Gets the item of type <see cref="TItem"/> that the reference is to. Throws if the reference cannot be resolved.
        /// </summary>
        /// <param name="resolver">The game config resolver to use to resolve the reference, i.e. the current game config.</param>
        /// <exception cref="InvalidOperationException">Throws if the reference cannot be resolved.</exception>
        /// <returns>The item that the reference is to.</returns>
        public TItem GetItem(IGameConfigDataResolver resolver)
        {
            return (TItem)((IMetaConfigId)this).ResolveRefObject(resolver);
        }

        /// <summary>
        /// Try to get the item of type <see cref="TItem"/> that the reference is to. Returns null if the reference cannot be resolved.
        /// Normally you should use <see cref="GetItem"/> instead, unless you have an explicit reason why the item reference might not resolve.
        /// This could be, for example, that you have a reference from the PlayerModel to a game config item, and that item might be removed from the game config.
        /// </summary>
        /// <param name="resolver">The game config resolver to use to resolve the reference, i.e. the current game config.</param>
        /// <returns>The item that the reference is to, or null if the reference could not be resolved.</returns>
        public TItem TryGetItem(IGameConfigDataResolver resolver)
        {
            return (TItem)((IMetaConfigId)this).TryResolveRefObject(resolver);
        }
        
        /// <summary>
        /// Create a MetaConfigId with the specified key. The key must not be null,
        /// and must be of an appropriate type for TItem.
        /// </summary>
        public static MetaConfigId<TItem> FromKey(object key)
        {
            return new MetaConfigId<TItem>(key);
        }

        static void CheckKeyValidity(object key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key), $"MetaConfigId<{typeof(TItem).Name}>: cannot have null key");

            if (!_keyType.IsAssignableFrom(key.GetType()))
            {
                throw new InvalidOperationException(
                    $"Key '{key}' of type {key.GetType().ToGenericTypeString()} cannot be used as a key in MetaConfigId<{typeof(TItem).Name}>, "
                    + $"because {typeof(TItem).Name} is a {typeof(TItem).GetGenericInterface(typeof(IGameConfigData<>)).ToGenericTypeString()} which has key type {_keyType.ToGenericTypeString()}");
            }
        }
        
        public override string ToString()
        {
            return Util.ObjectToStringInvariant(KeyObject);
        }

        /// <summary>
        /// Two MetaConfigIds are equal if:
        /// Their keys are equal, and their item types are compatible
        ///   (i.e. assignable to each other)
        /// </summary>
        bool EqualsImpl(IMetaConfigId other)
        {
            if (other is null)
                return false;

            // One item type must be assignable to the other, and the keys must be equal.
            Type otherItemType = other.ItemType;
            return (typeof(TItem).IsAssignableFrom(otherItemType) || otherItemType.IsAssignableFrom(typeof(TItem)))
                && KeyObject.Equals(other.KeyObject);
        }

        /// <inheritdoc cref="EqualsImpl"/>
        public override bool Equals(object obj)
            => obj is IMetaConfigId other && EqualsImpl(other);

        /// <inheritdoc cref="EqualsImpl"/>
        public bool Equals(MetaConfigId<TItem> other)
            => EqualsImpl(other);

        public override int GetHashCode() => KeyObject.GetHashCode();

        public static bool operator ==(MetaConfigId<TItem> a, MetaConfigId<TItem> b)
        {
            if (a is null)
                return b is null;
            return a.Equals(b);
        }

        public static bool operator !=(MetaConfigId<TItem> a, MetaConfigId<TItem> b)
        {
            return !(a == b);
        }
    }

    public static class MetaConfigIdUtil
    {
        // Dummy type, only used to statically assert the type and name of MetaConfigId<>.FromKey .
        // See DummyFromKey below. This type is not actually used at runtime.
        class DummyGameConfigData : IGameConfigData<int>
        {
            public int ConfigKey => throw new NotImplementedException();
        }

        // This, too, only exists to assert the static type and name of MetaConfigId<>.FromKey .
        // If FromKey's type is changed, you should update the usage of fromKeyMethod.Invoke
        // in the CreateFromKey method below.
        static readonly Func<object, MetaConfigId<DummyGameConfigData>> DummyFromKey = MetaConfigId<DummyGameConfigData>.FromKey;

        /// <summary>
        /// Dynamic-typed helper for <see cref="MetaConfigId{TItem}.FromKey(object)"/>
        /// </summary>
        public static IMetaConfigId CreateFromKey(Type metaConfigIdType, object key)
        {
            MethodInfo fromKeyMethod = metaConfigIdType.GetMethod(DummyFromKey.Method.Name, BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"No public static {DummyFromKey.Method.Name} method found from {metaConfigIdType.ToGenericTypeString()}!");
            return (IMetaConfigId)fromKeyMethod.Invoke(null, new object[] {key});
        }
    }
}
