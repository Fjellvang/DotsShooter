// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Forms;
using Metaplay.Core.Localization;
using Metaplay.Core.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Core.Config
{
    /// <summary>
    /// Game-specific parameters for the localizations build
    /// </summary>
    [MetaSerializable, MetaReservedMembers(101, 200)]
    public abstract class LocalizationsBuildParameters : IMetaIntegration<LocalizationsBuildParameters>, IGameDataBuildParameters
    {
        [MetaMember(101), MetaValidateRequired, MetaFormLayoutOrderHint(-1)]
        public GameConfigBuildSource DefaultSource;
    }

    [MetaSerializableDerived(100)]
    public class DefaultLocalizationsBuildParameters : LocalizationsBuildParameters
    {
    }

    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class LocalizationsBuild : IMetaIntegrationConstructible<LocalizationsBuild>
    {
        public IGameConfigSourceFetcherProvider SourceFetcherProvider { get; set; }
        BuildSourceFetcherAndMetadata           _fetcher;
        protected IGameConfigSourceFetcher SourceFetcher => _fetcher.Fetcher;

        // Parse per-language sheet to LocalizationLanguage
        protected static LocalizationLanguage ParseLocalizationLanguageSheet(SpreadsheetContent sheet)
        {
            LanguageId           languageId = LanguageId.FromString(sheet.Name);
            LocalizationLanguage language   = LocalizationLanguage.FromSpreadsheetContent(languageId, sheet);
            return language;
        }

        // Default implementation of localizations build.
        // Assumes that the source has an entry by name "Localizations" that contains localization languages
        // as SpreadsheetContent with per-language columns.
        protected virtual async Task<IEnumerable<LocalizationLanguage>> BuildAsync(LocalizationsBuildParameters buildParams, CancellationToken ct)
        {
            SpreadsheetContent content = (SpreadsheetContent)await SourceFetcher.Fetch("Localizations").Get();
            return GameConfigHelper
                .SplitLanguageSheets(content, allowMissingTranslations: false)
                .Select(ParseLocalizationLanguageSheet);
        }

        public async Task<ConfigArchive> CreateArchiveAsync(MetaTime createdAt, LocalizationsBuildParameters buildParams, CancellationToken ct)
        {
            // Configure source fetcher for 'source'
            IGameConfigSourceFetcher      fetcher  = await SourceFetcherProvider.GetFetcherForBuildSourceAsync(buildParams.DefaultSource, ct);
            GameConfigBuildSourceMetadata metadata = await fetcher.GetMetadataAsync(ct);
            _fetcher = new BuildSourceFetcherAndMetadata(fetcher, metadata);

            // Build LocalizationLanguage entries
            IEnumerable<LocalizationLanguage> languages = await BuildAsync(buildParams, ct);

            // Create the final ConfigArchive
            return ConfigArchive.CreateFromBuild(createdAt, languages.Select(x => x.ExportBinaryConfigArchiveEntry()));
        }
    }
}
