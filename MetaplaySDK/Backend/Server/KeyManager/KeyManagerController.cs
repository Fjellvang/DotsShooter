// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Server.AdminApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Metaplay.Server.KeyManager
{
    [Route(".well-known")]
    public class KeyManagerController : MetaplayController
    {
        /// <summary>
        /// Get the recent history of public keys managed by <see cref="KeyManagerActor"/>.
        /// <para>
        /// Example output looks like the following:
        /// <code>
        ///  "keys": [
        ///    {
        ///      "kty": "RSA",
        ///      "use": "sig",
        ///      "kid": "3d7a456f-8728-44f7-98b6-c9d3f5b2e2bb",
        ///      "e": "AQAB",
        ///      "n": "1vL-lN-sM2ZQ4fFLc0k9HD14Y7EgldhPLVR-WJJfsk3-hMggZ9nN...",
        ///      "alg": "RS256"
        ///    }
        ///  ]
        /// </code>
        /// </para>
        /// </summary>
        /// <returns></returns>
        [HttpGet("jwks.json")]
        [AllowAnonymous]
        [Produces("application/json")]
        public async Task<ActionResult> GetJwks()
        {
            KeyManagerActor.GetJwksResponse response = await EntityAskAsync(KeyManagerActor.EntityId, KeyManagerActor.GetJwksRequest.Instance);
            return Content(response.JwksJson);
        }
    }
}
