// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Analytics;
using Metaplay.Core.Analytics;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Metaplay.Server.AdminApi.Controllers
{
    public class AnalyticsEventsController : GameAdminApiController
    {
        AnalyticsEventRegistry _registry;
        BigQueryFormatter      _formatter;

        public AnalyticsEventsController(AnalyticsEventRegistry registry, BigQueryFormatter formatter)
        {
            _registry  = registry;
            _formatter = formatter;
        }

        /// <summary>
        /// API endpoint to get information about all known analytics events
        /// Usage: GET /api/analyticsEvents
        /// </summary>
        [HttpGet("analyticsEvents")]
        [RequirePermission(MetaplayPermissions.ApiAnalyticsEventsView)]
        public ActionResult<IEnumerable<AnalyticsEventSpec>> GetAnalyticsEvents()
        {
            return AsResult(_registry.AllEventSpecs);
        }

        /// <summary>
        /// API endpoint to get bigquery example of an analytics event
        /// Usage: GET /api/analyticsEvents/{EVENTCODE}/bigQueryExample
        /// </summary>
        [HttpGet("analyticsEvents/{eventCodeStr}/bigQueryExample")]
        [RequirePermission(MetaplayPermissions.ApiAnalyticsEventsView)]
        public ActionResult GetBigQueryExample(string eventCodeStr)
        {
            int eventTypeCode = Convert.ToInt32(eventCodeStr, CultureInfo.InvariantCulture);
            AnalyticsEventSpec eventSpec = _registry.EventSpecs.Values.FirstOrDefault(eventSpec => eventSpec.TypeCode == eventTypeCode);
            if (eventSpec == null)
                return NotFound();

            return new JsonResult(
                _formatter.GetExampleResultObject(eventSpec),
                AdminApiJsonSerialization.UntypedSerializationSettings);
        }
    }
}
