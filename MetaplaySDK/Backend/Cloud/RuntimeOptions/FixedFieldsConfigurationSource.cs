// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Microsoft.Extensions.Configuration;

namespace Metaplay.Cloud.RuntimeOptions
{
    class FixedFieldsConfigurationSource : IConfigurationSource
    {
        readonly MetaDictionary<string, string> _fields;

        public FixedFieldsConfigurationSource(MetaDictionary<string, string> fields)
        {
            _fields = fields;
        }

        IConfigurationProvider IConfigurationSource.Build(IConfigurationBuilder builder) => new FixedFieldsConfigurationProvider(_fields);
    }

    class FixedFieldsConfigurationProvider : ConfigurationProvider
    {
        readonly MetaDictionary<string, string> _fields;

        public FixedFieldsConfigurationProvider(MetaDictionary<string, string> fields)
        {
            _fields = fields;
        }

        public override void Load()
        {
            Data = new MetaDictionary<string, string>(_fields);
        }
    }
}
