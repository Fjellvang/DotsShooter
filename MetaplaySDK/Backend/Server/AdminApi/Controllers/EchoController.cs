// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Services.Geolocation;
using Metaplay.Core.Player;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Net;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for echoing details back to the requester
    /// </summary>
    public class EchoController : MetaplayAdminApiController
    {
        /// <summary>
        /// Echo request details back to the requester.
        /// Usage:  GET /api/echo
        /// Test:   curl http://localhost:5550/api/echo
        ///
        /// <para>
        /// <b>Warning: </b> Echoing http headers back can be dangerous. It allows JS to read HTTPOnly cookies,
        /// violating their safety. Additionally, the infrastructure between browser and AdminAPI injects custom
        /// headers like access tokens and authentication information.
        /// </para>
        /// <para>
        /// For there reasons, this endpoint is powerful in debugging both the browser but also
        /// infrastructure-injected parameters.
        /// </para>
        /// <para>
        /// In cloud-deployments, this endpoint is by default inaccessible. To enable this endpoint, you must define in
        /// your deployment heml values:
        /// <code>
        /// debug:
        ///     headerEcho: true
        /// </code>
        /// </para>
        /// </summary>
        [HttpGet("echo")]
        [RequirePermission(MetaplayPermissions.Anyone)]
        public ActionResult Echo()
        {
            IPAddress remoteIp = TryGetRemoteIp();
            PlayerLocation? location = remoteIp != null ? Geolocation.Instance.TryGetPlayerLocation(remoteIp) : null;

            object result = new
            {
                Headers = Request.Headers.ToDictionary(item => item.Key, item => item.Value.ToList()),
                Method = Request.Method,
                ContentType = Request.ContentType,
                ContentLength = Request.ContentLength,
                Path = Request.Path,
                Host = Request.Host.ToString(),
                Protocol = Request.Protocol,
                Cookies = Request.Cookies.ToDictionary(item => item.Key, item => item.Value), // \note: This leaks HTTP-Only cookies. Useful for debugging.
                QueryString = Request.QueryString.ToString(),
                Query = Request.Query.ToDictionary(item => item.Key, item => item.Value.ToList()),
                Metaplay = new
                {
                    UserId = GetUserId(),
                    RemoteIp = remoteIp?.ToString(),
                    Location = location.HasValue ? location.Value.Country.IsoCode : null,
                },
            };

            return Ok(result);
        }
    }
}
