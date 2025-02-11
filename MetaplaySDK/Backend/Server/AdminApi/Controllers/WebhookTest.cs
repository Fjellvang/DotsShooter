// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Webhook test controller
    /// </summary>
    public class WebhookTestController : MetaplayWebhookController
    {
        /// <summary>
        /// Usage:  GET /webhook/test
        /// </summary>
        [HttpGet("test")]
        public ActionResult GetTest()
        {
            return Ok(new
            {
                success = true,
                method = Request.Method,
                headers = Request.Headers.Keys.ToDictionary(key => key, key => Request.Headers[key]),
                queryString = Request.QueryString.Value,
            });
        }

        /// <summary>
        /// Usage:  POST /webhook/test
        /// </summary>
        [HttpPost("test")]
        public async Task<ActionResult> PostTestAsync()
        {
            return Ok(new
            {
                success = true,
                method = Request.Method,
                headers = Request.Headers.Keys.ToDictionary(key => key, key => Request.Headers[key]),
                queryString = Request.QueryString.Value,
                body = await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync(),
            });
        }
    }
}
