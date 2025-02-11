// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Options;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using System;
using System.Threading.Tasks;

namespace Metaplay.Server.KeyManager
{
    [RuntimeOptions("KeyManager", isStatic: true, "Signing key manager options.")]
    class KeyManagerOptions : RuntimeOptionsBase
    {
        [MetaDescription("Number of bits in the signing key.")]
        public int KeySize { get; private set; } = 2048;

        [MetaDescription("Rotation interval of the key pairs.")]
        public TimeSpan RotationInterval { get; private set; } = TimeSpan.FromHours(1);

        [MetaDescription("Number of public keys to retain. If exceeded, the oldest keys are discarded.")]
        public int NumPublicKeysToRetain { get; private set; } = 4;

        public override Task OnLoadedAsync()
        {
            if (KeySize < 1024 || KeySize > 16384)
                throw new InvalidOperationException($"Invalid KeySize {KeySize}, must be between 1024 and 16384");

            if (RotationInterval < TimeSpan.FromMinutes(1) || RotationInterval >= TimeSpan.FromDays(1))
                throw new InvalidOperationException($"Invalid RotationInterval {RotationInterval}, must be between 1 minute and 1 day");

            if (NumPublicKeysToRetain < 2 || NumPublicKeysToRetain >= 100)
                throw new InvalidOperationException($"Invalid NumPublicKeysToRetain {NumPublicKeysToRetain}, must be between 2 minute and 100 day");

            return Task.CompletedTask;
        }
    }
}
