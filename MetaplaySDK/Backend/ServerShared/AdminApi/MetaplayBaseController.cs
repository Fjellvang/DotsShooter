// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;

namespace Metaplay.Server.AdminApi
{
    /// <summary>
    /// Configure the authentication domain "Webhook" for HTTP endpoints at /webhook.
    /// The controller does not apply any authentication on the endpoints and it is
    /// the responsibility of the individual endpoints to handle their own authentication.
    /// </summary>
    public class WebhookAuthenticationConfig : AuthenticationDomainConfig
    {
        public override void ConfigureServices(IServiceCollection services, AdminApiOptions opts)
        {
        }

        public override void ConfigureApp(WebApplication app, AdminApiOptions opts)
        {
            // Register a 404 handler with the same CORS policy as proper endpoints (to get sensible error messages)
            Register404Handler(app, pathPrefix: MetaplayWebhookController.RoutePathPrefix, corsPolicy: null, handler: (HttpContext ctx, string sanitizedPath) =>
            {
                _log.Warning("Request to non-existent webhook endpoint {Method} '{SanitizedPath}'", ctx.Request.Method, sanitizedPath);
            });
        }
    }

    /// <summary>
    /// User-facing base class for all webhook API endpoints. Any controller derived from this
    /// class will have routes starting with '/webhook'
    /// </summary>
    [Route(RoutePathPrefix)]
    public abstract class MetaplayWebhookController : MetaplayController
    {
        public const string RoutePathPrefix = "webhook";
    }

    /// <summary>
    /// Thread-local trick to pass initialization parameters to MetaplayController without
    /// needing them to be passed via the inheriting class's constructors.
    /// </summary>
    public struct MetaplayControllerInitContext
    {
        public ILogger          Logger      { get; private init; }
        public SharedApiBridge  ApiBridge   { get; private init; }

        public MetaplayControllerInitContext(ILogger logger, SharedApiBridge apiBridge)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ApiBridge = apiBridge ?? throw new ArgumentNullException(nameof(apiBridge));
        }

        public static ThreadLocal<MetaplayControllerInitContext> Instance = new ThreadLocal<MetaplayControllerInitContext>();
    }

    /// <summary>
    /// Internal base class for all Metaplay API controllers. Includes functionality for:
    /// - Parsing request body data
    /// - Querying various entity types
    ///
    /// NB: Client code should not derive from this controller directly as it has no route, rather
    /// you should derive from one of the MetaplayXXXControllers above
    ///
    /// Note: Inherits Controller (instead of ControllerBase) to get MVC view functionality.
    /// </summary>
    [ApiController]
    public abstract class MetaplayController : Controller
    {
        protected readonly ILogger          _logger;
        protected readonly SharedApiBridge  _apiBridge;

        protected virtual SharedApiBridge ApiBridge => _apiBridge;

        protected MetaplayController()
        {
            MetaplayControllerInitContext initContext = MetaplayControllerInitContext.Instance.Value;
            _logger     = initContext.Logger ?? throw new InvalidOperationException("MetaplayControllerInitContext.Logger is null");
            _apiBridge  = initContext.ApiBridge ?? throw new InvalidOperationException("MetaplayControllerInitContext.ApiBridge is null");
        }

        /// <summary>
        /// Parse and deserialise the request body as the specified class type
        /// Throws MetaplayHttpException on error
        /// </summary>
        /// <typeparam name="T">Type of class</typeparam>
        /// <returns>Deserialised object</returns>
        //[Obsolete("Use a method parameter with the [FromBody] attribute instead!")] // \todo use me to get warnings about manually parsing POST bodies
        protected async Task<T> ParseBodyAsync<T>()
        {
            try
            {
                using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
                using (TextReader textReader = new StringReader(await reader.ReadToEndAsync()))
                using (JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    T result = AdminApiJsonSerialization.Serializer.Deserialize<T>(jsonReader);
                    if (result == null)
                        throw new Exception("Expecting body data but no body was supplied.");
                    return result;
                }
            }
            catch (JsonReaderException ex)
            {
                throw new MetaplayHttpException(400, "Cannot parse body JSON.", ex.Message);
            }
            catch (Exception ex)
            {
                throw new MetaplayHttpException(400, "Cannot parse body.", ex.Message);
            }
        }

        /// <summary>
        /// Parse and deserialise the request body as the specified class type
        /// Throws MetaplayHttpException on error
        /// </summary>
        /// <typeparam name="T">Type of class</typeparam>
        /// <param name="type"></param>
        /// <returns>Deserialised object</returns>
        protected async Task<T> ParseBodyAsync<T>(Type type) where T : class
        {
            if (!type.IsDerivedFrom<T>())
                throw new MetaplayHttpException(400, "Bad class type in request.", $"Class {type.Name} is not derived from {typeof(T).Name}.");

            try
            {
                using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
                using (TextReader textReader = new StringReader(await reader.ReadToEndAsync()))
                using (JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    T result = (T)AdminApiJsonSerialization.Serializer.Deserialize(jsonReader, type);
                    if (result == null)
                        throw new Exception("Expecting body data but no body was supplied.");
                    return result;
                }
            }
            catch (JsonReaderException ex)
            {
                throw new MetaplayHttpException(400, "Cannot parse body JSON.", ex.Message);
            }
            catch (Exception ex)
            {
                throw new MetaplayHttpException(400, "Cannot parse body.", ex.Message);
            }
        }

        /// <summary>
        /// Parse and deserialise the request body as the specified class type
        /// Throws MetaplayHttpException on error
        /// </summary>
        /// <param name="type"></param>
        /// <returns>Deserialised object</returns>
        protected async Task<object> ParseBodyAsync(System.Type type)
        {
            try
            {
                using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
                using (TextReader textReader = new StringReader(await reader.ReadToEndAsync()))
                using (JsonReader jsonReader = new JsonTextReader(textReader))
                {
                    object result = AdminApiJsonSerialization.Serializer.Deserialize(jsonReader, type);
                    if (result == null)
                        throw new Exception("Expecting body data but no body was supplied.");
                    return result;
                }
            }
            catch (JsonReaderException ex)
            {
                throw new MetaplayHttpException(400, "Cannot parse body JSON.", ex.Message);
            }
            catch (Exception ex)
            {
                throw new MetaplayHttpException(400, "Cannot parse body.", ex.Message);
            }
        }

        protected async Task<byte[]> ReadBodyBytesAsync()
        {
            using (MemoryStream memStream = new MemoryStream(8192))
            {
                await Request.Body.CopyToAsync(memStream);
                return memStream.ToArray();
            }
        }

        /// <summary>
        /// Helper method to wrap a value in a <see cref="ActionResult{TValue}"/>. Useful for return types
        /// like <see cref="IEnumerable{T}"/> which do not get automatically coerced to the correspoding
        /// <see cref="ActionResult{TValue}"/>.
        /// </summary>
        protected static ActionResult<T> AsResult<T>(T value) => new ActionResult<T>(value);

        /// <inheritdoc cref="SharedApiBridge.TellEntityAsync(EntityId, MetaMessage)"/>
        protected Task TellEntityAsync(EntityId entityId, MetaMessage message) =>
            ApiBridge.TellEntityAsync(entityId, message);

        /// <inheritdoc cref="SharedApiBridge.EntityAskAsync{TResult}(EntityId, MetaMessage)"/>
        protected Task<TResult> EntityAskAsync<TResult>(EntityId entityId, MetaMessage message) where TResult : MetaMessage =>
            ApiBridge.EntityAskAsync<TResult>(entityId, message);

        /// <inheritdoc cref="SharedApiBridge.EntityAskAsync{TResult}(EntityId, EntityAskRequest{TResult})"/>
        protected Task<TResult> EntityAskAsync<TResult>(EntityId entityId, EntityAskRequest<TResult> message) where TResult : EntityAskResponse =>
            ApiBridge.EntityAskAsync<TResult>(entityId, message);

        /// <inheritdoc cref="SharedApiBridge.EntityAskAsync{TResult}(EntityId, EntityAskRequest)"/>
        [Obsolete("Type mismatch between TResult type parameter and the annotated response type for the request parameter type.", error: true)]
        protected Task<TResult> EntityAskAsync<TResult>(EntityId entityId, EntityAskRequest message) where TResult : MetaMessage =>
            ApiBridge.EntityAskAsync<TResult>(entityId, message);

        [Obsolete($"Renamed to {nameof(EntityAskAsync)} (for consistency with {nameof(EntityActor)}'s corresponding method)")]
        protected Task<TResult> AskEntityAsync<TResult>(EntityId entityId, MetaMessage message) where TResult : MetaMessage =>
            ApiBridge.EntityAskAsync<TResult>(entityId, message);

        /// <summary>
        /// Utility function to parse and validate a MetaGuid from a string
        /// Throws MetaplayHttpException on error
        /// </summary>
        /// <param name="metaGuidStr">String representation of the MetaGuid</param>
        /// <returns>EntityId of the player</returns>
        public static MetaGuid ParseMetaGuidStr(string metaGuidStr)
        {
            MetaGuid metaGuid;
            try
            {
                metaGuid = MetaGuid.Parse(metaGuidStr);
            }
            catch (FormatException ex)
            {
                throw new MetaplayHttpException(400, "Invalid MetaGuid.", $"MetaGuid {metaGuidStr} is not valid: {ex.Message}");
            }
            return metaGuid;
        }

        /// <inheritdoc cref="SharedApiBridge.ParseEntityIdStr(string, EntityKind)"/>
        protected EntityId ParseEntityIdStr(string entityIdStr, EntityKind expectedKind) =>
            ApiBridge.ParseEntityIdStr(entityIdStr, expectedKind);

        /// <inheritdoc cref="SharedApiBridge.TryGetPersistedEntityAsync(EntityId)"/>
        protected Task<(bool isFound, bool isInitialized, IPersistedEntity entity)> TryGetPersistedEntityAsync(EntityId entityId) =>
            ApiBridge.TryGetPersistedEntityAsync(entityId);

        /// <inheritdoc cref="SharedApiBridge.GetPersistedEntityAsync(EntityId)"/>
        protected Task<IPersistedEntity> GetPersistedEntityAsync(EntityId entityId) =>
            ApiBridge.GetPersistedEntityAsync(entityId);

        /// <summary>
        /// Return the IP of the remote address that made the Http request.
        /// </summary>
        /// <returns>IPAddress, can be null</returns>
        protected IPAddress TryGetRemoteIp()
        {
            // return IPAddress.Parse($"{new Random().Next(1,250)}.184.101.0");      // PKG - Change this to fake remote IP address for testing Audit Logs, etc

            if (Request.Headers.TryGetValue("X-Forwarded-For", out StringValues forwardedIps))
                if (IPAddress.TryParse(forwardedIps.First(), out IPAddress forwardedIpAddress))
                   return forwardedIpAddress;
            return HttpContext.Connection.RemoteIpAddress;
        }

        /// <summary>
        /// Checks whether the if-modified-since header is set, if so, we check whether it is older than <paramref name="lastModified"/>.
        /// <paramref name="lastModified"/> is always set to the response's last-modified header.
        /// </summary>
        /// <returns>True if if-modified-since header is newer or equal than <paramref name="lastModified"/></returns>
        protected bool IsCacheValidAndSetLastModified(DateTime? lastModified)
        {
            ResponseHeaders headers = Response.GetTypedHeaders();
            headers.CacheControl = new CacheControlHeaderValue()
            {
                NoCache = true,
                Private = true
            };

            headers.LastModified = lastModified?.ToUniversalTime();

            if (HttpContext.Request.Headers.ContainsKey(HeaderNames.IfModifiedSince))
            {
                RequestHeaders requestHeaders = HttpContext.Request.GetTypedHeaders();

                if (lastModified != null && requestHeaders.IfModifiedSince != null)
                {
                    // Subtract milliseconds since the http header has second precision
                    if (lastModified.Value.AddMilliseconds(-lastModified.Value.Millisecond) <= requestHeaders.IfModifiedSince.Value)
                        return true;
                }
            }

            return false;
        }
    }
}
