// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for server-sent-events.
    /// This is slightly ghetto test code. In reality, it should be event driven rather than polling for events.
    /// The task handling inside Get() should probably be implemented differently too, eg: one thread for all client
    /// instead of one per client. We may even want to switch to Web Sockets instead so that the client can subscribe
    /// to individual topics.
    /// </summary>
    public class SseController : GameAdminApiController
    {
        static readonly string _serverStartTimeEvent = "data: " + new SseEvent("serverStartTime", MetaTime.Now.MillisecondsSinceEpoch.ToString(CultureInfo.InvariantCulture)).AsJson() + "\n\n";

        // <summary>
        // Start receiving events.
        // Usage:  GET /api/sse
        // Test:   curl -N http://localhost:5550/api/sse
        // </summary>
        [HttpGet("sse")]
        [RequirePermission(MetaplayPermissions.ApiGeneralView)]
        public async Task Get(CancellationToken ct)
        {
            HttpContext.Items.Add("_MetaplayLongRunningQuery", new object());

            // Reply with headers and server start time immediately.
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"]= "no";
            try
            {
                await Response.WriteAsync(_serverStartTimeEvent, ct);
                await Response.Body.FlushAsync(ct);

                // Events that we want to check for
                List<SseEventChecker> eventCheckers = new List<SseEventChecker>
                {
                    new ActiveGameConfigSseEventChecker(),
                    new RuntimeOptionsSseEventChecker(),
                };

                // Enter a loop, checking for and sending events every few seconds until the client disconnects
                const int PollIntervalInSeconds      = 5;
                const int KeepAliveTimeInSeconds     = 10;
                int       TimeSinceLastSendInSeconds = 0;
                while (!ct.IsCancellationRequested)
                {
                    // Check for events from any of the event checkers
                    bool eventSent = false;
                    foreach (SseEventChecker eventChecker in eventCheckers)
                    {
                        SseEvent newEvent = eventChecker.CheckForEvent();
                        if (newEvent != null)
                        {
                            await Response.WriteAsync($"data: {newEvent.AsJson()}\n\n", ct);
                            eventSent = true;
                        }
                    }

                    // If no events have been sent for a period of time then we need to send a keep-alive
                    if (!eventSent && TimeSinceLastSendInSeconds >= KeepAliveTimeInSeconds)
                    {
                        await Response.WriteAsync(":\n\n", ct);
                        eventSent = true;
                    }

                    if (eventSent)
                    {
                        await Response.Body.FlushAsync(ct);
                        TimeSinceLastSendInSeconds = 0;
                    }
                    else
                    {
                        TimeSinceLastSendInSeconds += PollIntervalInSeconds;
                    }

                    await Task.Delay(PollIntervalInSeconds * 1000, ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Encapsulates an event that can be sent to the client. Contains a message type and some optional opaque data
        /// </summary>
        class SseEvent
        {
            object Event = null;
            public SseEvent(string name, object data = null)
            {
                Event = new
                {
                    name = name,
                    value = data
                };
            }
            public string AsJson()
            {
                return JsonSerialization.SerializeToString(Event);
            }
        }

        /// <summary>
        /// Abstract base class for checking for events that might get sent out over SSE
        /// </summary>
        abstract class SseEventChecker
        {
            public abstract SseEvent CheckForEvent();
        }

        /// <summary>
        /// Event checker for changes in the active game config version
        /// </summary>
        class ActiveGameConfigSseEventChecker : SseEventChecker
        {
            MetaGuid _activeVersion;

            public ActiveGameConfigSseEventChecker()
            {
               _activeVersion = GetActiveVersionOrNone();
            }
            public override SseEvent CheckForEvent()
            {
                MetaGuid newActiveVersion = GetActiveVersionOrNone();
                if (_activeVersion != newActiveVersion)
                {
                    _activeVersion = newActiveVersion;
                    return new SseEvent("activeGameConfigChanged", null);
                }
                return null;
            }

            MetaGuid GetActiveVersionOrNone()
            {
                return GlobalStateProxyActor.ActiveGameConfig.Get()?.BaselineStaticGameConfigId ?? MetaGuid.None;
            }
        }

        /// <summary>
        /// Event checker for changes in the runtime options
        /// </summary>
        class RuntimeOptionsSseEventChecker : SseEventChecker
        {
            MetaTime _lastUpdateTime;
            public RuntimeOptionsSseEventChecker()
            {
                _lastUpdateTime = RuntimeOptionsRegistry.Instance._lastUpdateTime;
            }
            public override SseEvent CheckForEvent()
            {
                MetaTime newGenerationCount = RuntimeOptionsRegistry.Instance._lastUpdateTime;
                if (_lastUpdateTime != newGenerationCount)
                {
                    _lastUpdateTime = newGenerationCount;
                    return new SseEvent("runtimeOptionsChanged", null);
                }
                return null;
            }
        }
    }
}
