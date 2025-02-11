// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Serialization;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Metaplay.Core.Web3
{
    public class NftTypeSpec
    {
        public Type             NftType;
        public NftCollectionId  CollectionId;
        public NftMetadataSpec  MetadataSpec;

        public NftTypeSpec(Type nftType, NftCollectionId collectionId, NftMetadataSpec metadataSpec)
        {
            NftType         = nftType;
            CollectionId    = collectionId;
            MetadataSpec    = metadataSpec;
        }
    }

    public class NftTypeRegistry
    {
        public static NftTypeRegistry Instance => MetaplayServices.Get<NftTypeRegistry>();

        public bool HasAnyNftTypes => _specs.Count > 0;

        public NftTypeSpec GetSpec(Type nftType)
        {
            if (nftType == null)
                throw new ArgumentNullException(nameof(nftType));

            if (!_specs.TryGetValue(nftType, out NftTypeSpec spec))
            {
                if (_isEnabled)
                    throw new KeyNotFoundException($"Type {nftType} hasn't been registered as an NFT type. NFT types must inherit {nameof(MetaNft)} and have {nameof(MetaNftAttribute)}.");
                else
                    throw new InvalidOperationException($"{nameof(NftTypeRegistry)} is not populated because the game does not set {nameof(MetaplayFeatureFlags.EnableWeb3)}=true in its {nameof(IMetaplayCoreOptionsProvider)}'s {nameof(IMetaplayCoreOptionsProvider.Options)}.{nameof(MetaplayCoreOptions.FeatureFlags)}");
            }

            return spec;
        }

        public IEnumerable<NftTypeSpec> GetAllSpecs() => _specs.Values;

        readonly bool _isEnabled;
        readonly MetaDictionary<Type, NftTypeSpec> _specs;

        public static NftTypeRegistry Create(MetaplayCoreOptions options)
        {
            if (options.FeatureFlags.EnableWeb3)
                return CreateRegistry();
            else
                return new NftTypeRegistry(isEnabled: false, new MetaDictionary<Type, NftTypeSpec>());
        }

        NftTypeRegistry(bool isEnabled, MetaDictionary<Type, NftTypeSpec> specs)
        {
            _isEnabled = isEnabled;
            _specs = specs;
        }

        static NftTypeRegistry CreateRegistry()
        {
            List<NftTypeSpec> specs = new List<NftTypeSpec>();

            foreach (Type nftType in MetaSerializerTypeRegistry.GetConcreteDerivedTypes<MetaNft>())
            {
                MetaNftAttribute attribute = nftType.GetCustomAttribute<MetaNftAttribute>(inherit: true);
                if (attribute == null)
                    throw new InvalidOperationException($"Type {nftType} inherits {nameof(MetaNft)} but is missing {nameof(MetaNftAttribute)}.");

                specs.Add(new NftTypeSpec(
                    nftType,
                    attribute.CollectionId,
                    new NftMetadataSpec(nftType)));
            }

            MetaDictionary<Type, NftTypeSpec> specsDict =
                specs
                .OrderBy(spec => spec.CollectionId)
                .ThenBy(spec => spec.NftType.ToNamespaceQualifiedTypeString(), StringComparer.Ordinal)
                .ToMetaDictionary(spec => spec.NftType);

            return new NftTypeRegistry(isEnabled: true, specsDict);
        }

        #region Helpers

        public NftKey GetNftKey(Type nftType, NftId tokenId)
        {
            return new NftKey(GetCollectionId(nftType), tokenId);
        }

        public NftKey GetNftKey(MetaNft nft)
        {
            return GetNftKey(nft.GetType(), nft.TokenId);
        }

        public NftCollectionId GetCollectionId(Type nftType)
        {
            return GetSpec(nftType).CollectionId;
        }

        public NftCollectionId GetCollectionId(MetaNft nft)
        {
            return GetCollectionId(nft.GetType());
        }

        public JObject GenerateMetadata(MetaNft nft)
        {
            return GetSpec(nft.GetType()).MetadataSpec.GenerateMetadata(nft);
        }

        #endregion
    }
}
