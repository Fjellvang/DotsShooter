// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Session;
using System;
using System.Security.Cryptography;

namespace Metaplay.Server
{
    public static class SessionResumptionTokenUtil
    {
        public static byte[] GenerateResumptionToken(EntityId playerId, SessionToken sessionToken)
        {
            Span<byte> buffer = stackalloc byte[32];
            InternalGenerateResumptionToken(playerId, sessionToken, buffer);
            return buffer.ToArray();
        }

        public static bool ValidateResumptionToken(EntityId playerId, SessionToken sessionToken, byte[] resumptionToken)
        {
            Span<byte> buffer = stackalloc byte[32];
            InternalGenerateResumptionToken(playerId, sessionToken, buffer);
            return CryptographicOperations.FixedTimeEquals(resumptionToken, buffer);
        }

        /// <summary>
        /// Generates HMAC of (player || sessionToken)
        /// </summary>
        static void InternalGenerateResumptionToken(EntityId playerId, SessionToken sessionToken, Span<byte> output256)
        {
            ReadOnlySpan<byte>  nonce256    = GlobalStateProxyActor.ActiveSharedClusterNonce.Get().Nonce;
            Span<byte>          key         = stackalloc byte[64];
            Span<byte>          source      = stackalloc byte[20];

            // Create K (take nonce, mix something in). Reset trailer.
            nonce256.CopyTo(key);
            key[0] ^= 0x12;
            key[5] ^= 0x76;
            key[32..64].Clear();

            // Content to HMAC
            _ = BitConverter.TryWriteBytes(source[0..4], playerId.Kind.Value);
            _ = BitConverter.TryWriteBytes(source[4..12], playerId.Value);
            _ = BitConverter.TryWriteBytes(source[12..20], sessionToken.Value);

            _ = HMACSHA256.HashData(key: key, source: source, destination: output256);
        }
    }
}
