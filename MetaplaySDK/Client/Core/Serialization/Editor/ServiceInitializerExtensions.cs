// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if NETCOREAPP
using Metaplay.Cloud;
#endif
using Metaplay.Core.Serialization;
using System.IO;
using System.Reflection;

namespace Metaplay.Core
{
    public static class ServiceInitializerExtensions
    {
        public static IServiceInitializers ConfigureMetaSerialization(
            this IServiceInitializers initializers,
            string applicationName,
            string outputDirectory = null,
            string errorDirectory = null,
            bool useMemberAccessTrampolines = false,
            bool generateRuntimeTypeInfo = false)
        {
            outputDirectory ??= Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            errorDirectory  ??= Path.Join(outputDirectory, "Errors");

            initializers.Add(_ => new MetaSerializerTypeScanner().GetTypeInfo());
            initializers.Add(
                provider =>
                {
                    #if NETCOREAPP
                    IMetaLogger logger = MetaLogger.ForContext<TaggedSerializerRoslyn>();
                    logger.Information("Compiling serializer {dll}",$"Metaplay.Generated.{applicationName}.dll");
                    #endif
                    Assembly generatedDll = RoslynSerializerCompileCache.GetOrCompileAssembly(
                        outputDir: outputDirectory,
                        dllFileName: $"Metaplay.Generated.{applicationName}.dll",
                        errorDir: errorDirectory,
                        useMemberAccessTrampolines: useMemberAccessTrampolines,
                        generateRuntimeTypeInfo: generateRuntimeTypeInfo,
                        provider.Get<MetaSerializerTypeInfo>());
                    return TaggedSerializerRoslyn.CreateFromAssembly(generatedDll, forceRunInitEagerly: true);
                });
            return initializers;
        }
    }
}
