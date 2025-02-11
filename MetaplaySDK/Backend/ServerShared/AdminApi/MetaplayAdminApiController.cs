// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Json;
using Metaplay.Server.AdminApi.AuditLog;
using Metaplay.Server.AdminApi.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi
{
    internal static class ServiceCollectionExtensions
    {
        internal static void DisableDataProtectionProvider(this IServiceCollection services)
        {
            ServiceDescriptor dataProtectionProviderDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IDataProtectionProvider));
            if (dataProtectionProviderDescriptor == null)
                throw new Exception("The IDataProtectionProvider could not be found in the IServiceCollection");

            _ = services.Replace(
                ServiceDescriptor.Describe(
                    dataProtectionProviderDescriptor.ServiceType,
                    sp => throw new NotImplementedException("DataProtectionProvider intentionally disabled!"),
                    dataProtectionProviderDescriptor.Lifetime));
        }
    }

    /// <summary>
    /// Configure the authentication domain "AdminApi" HTTP endpoints. These are used by the LiveOps Dashboard.
    /// </summary>
    public class AdminApiAuthenticationConfig : AuthenticationDomainConfig
    {
        public override void ConfigureServices(IServiceCollection services, AdminApiOptions opts)
        {
            // Warn if auth is disabled (unless we're running in a local environment, when it's ok to do that)
            if (opts.Type == AuthenticationType.None && RuntimeEnvironmentInfo.Instance.EnvironmentFamily != EnvironmentFamily.Local)
                _log.Warning("Authentication disabled for HTTP API! Are you sure?");

            // Validate AdminApi controllers
            ValidateAdminApiControllers(opts.ResolvedPermissions.PermissionGroups);

            // Disable Data Protection provider: we don't encrypt tokens or session state so this is only to silence the warnings
            // about the disk storage being ephemeral when running in a container. The registered provider throws if someone
            // tries to use it to avoid accidental usage.
            // If we start using ASP.NET sessions (or authentication middleware that uses cookies), CSRF protection, ASP.NET Core Identity,
            // this needs to be replaced with persisted storage such as a database (likely shared across multiple ASP.NET servers).
            // Note: Also remove the logging overrides for 'Microsoft.AspNetCore.DataProtection.*' when enabling DataProtection.
            // \todo Disabled for now as causes exceptions when deployed in cloud -- figure out why
            //services.DisableDataProtectionProvider();

            // Configure CORS
            services.AddCors(options =>
            {
                options.AddPolicy(MetaplayAdminApiController.CorsPolicy, builder =>
                {
                    // If running locally, allow requests from 5551 (from 'npm run serve')
                    if (RuntimeOptionsBase.IsLocalEnvironment)
                    {
                        builder
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials()
                          .WithOrigins("http://localhost:5551");
                    }
                });
            });

            // Register authentication scheme for ASP.NET based on authentication method used
            AuthenticationBuilder authBuilder = services.AddAuthentication();
            switch (opts.Type)
            {
                case AuthenticationType.None:
                    authBuilder.AddScheme<AnonymousAuthenticationOptions, AnonymousAuthenticationHandler>(MetaplayAdminApiController.AuthenticationScheme, options =>
                    {
                    });
                    break;

                case AuthenticationType.JWT:
                    authBuilder.AddJwtBearer(MetaplayAdminApiController.AuthenticationScheme, options =>
                    {
                        options.Authority = opts.GetAdminApiDomain();
                        options.Audience  = opts.JwtConfiguration.Audience;
                        options.SaveToken = true;
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                // If standard Authorization header is not provided, try finding token from BearerTokenSource (usually 'Metaplay-IdToken')
                                if (!context.Request.Headers.ContainsKey("Authorization"))
                                {
                                    if (context.Request.Headers.TryGetValue(opts.JwtConfiguration.BearerTokenSource, out StringValues bearerToken))
                                    {
                                        context.Token = bearerToken.ToString();
                                    }
                                }
                                return Task.CompletedTask;
                            }
                        };
                    });
                    break;

                default:
                    throw new InvalidOperationException($"Invalid or incomplete auth type '{opts.Type}' chosen.");
            }

            // Register the authorization handler for [RequirePermission]
            services.AddSingleton<IAuthorizationHandler, AdminApiPermissionHandler>();

            // Register IHttpContextAccessor for AdminApiPermissionHandler
            services.AddHttpContextAccessor();
        }

        public override void ConfigureApp(WebApplication app, AdminApiOptions opts)
        {
            // Register a 404 handler with the same CORS policy as proper endpoints (to get sensible error messages)
            Register404Handler(app, pathPrefix: MetaplayAdminApiController.RoutePathPrefix, corsPolicy: MetaplayAdminApiController.CorsPolicy, handler: (HttpContext ctx, string sanitizedPath) =>
            {
                _log.Warning("Request to non-existent admin API endpoint {Method} '{SanitizedPath}'", ctx.Request.Method, sanitizedPath);
            });
        }

        public static void ValidateAdminApiControllers(PermissionGroupDefinition[] permissionGroups)
        {
            HashSet<string> usedPermissions = new HashSet<string>();
            HashSet<string> enabledPermissionDefs =
                permissionGroups
                .SelectMany(group => group.Permissions)
                .Where(perm => perm.IsEnabled)
                .Select(perm => perm.Name)
                .ToHashSet();

            // Check that all AdminApi controller endpoints specify a [RequirePermission] attribute.
            foreach (Type controller in TypeScanner.GetDerivedTypes<MetaplayAdminApiController>())
            {
                IEnumerable<MethodInfo> handlerMethods =
                    controller.GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public)
                    .Where(methodInfo => methodInfo.HasCustomAttribute<HttpMethodAttribute>());
                foreach (MethodInfo methodInfo in handlerMethods)
                {
                    // Check for legacy [AllowAnomymous] usage
                    bool hasAnonymousAttribute = methodInfo.HasCustomAttribute<AllowAnonymousAttribute>();
                    if (hasAnonymousAttribute)
                        throw new InvalidOperationException($"AdminApi controller endpoint {controller.ToGenericTypeString()}.{methodInfo.Name}() uses the deprecated [AllowAnonymous] attribute. Replace it with [RequirePermission(MetaplayPermissions.Anyone)].");

                    // Check that the endpoint has a [RequirePermission(..)] attribute
                    RequirePermissionAttribute permissionAttribute = methodInfo.GetCustomAttribute<RequirePermissionAttribute>();
                    if (permissionAttribute == null)
                        throw new InvalidOperationException($"AdminApi controller endpoint {controller.ToGenericTypeString()}.{methodInfo.Name}() must specify the [RequirePermission] attribute.");

                    // Check permission of the enabled endpoints
                    if (controller.IsMetaFeatureEnabled())
                    {
                        usedPermissions.Add(permissionAttribute.PermissionName);

                        // Check that the permission of the [RequirePermission(..)] refers to a declared Permission.
                        if (!enabledPermissionDefs.Contains(permissionAttribute.PermissionName))
                            throw new InvalidOperationException($"Unknown permission. AdminApi controller endpoint {controller.ToGenericTypeString()}.{methodInfo.Name}() references permission {permissionAttribute.PermissionName} with [RequirePermission] attribute but there is no such [Permission] defined in server code.");
                    }

#if false // \note Enable to find all the HTTP endpoints not following our guidelines -- gets really spammy for now
                    // Check that the return value specifies a type
                    // \todo This is very spammy don't merge as is, perhaps add an enable-flag to runtime options or add a silence-list or something
                    Type returnType = methodInfo.ReturnType;

                    // Unwrap generic Task<T>
                    bool isGenericTask = returnType.IsGenericTypeOf(typeof(Task<>));
                    Type unwrappedType = isGenericTask ? returnType.GenericTypeArguments[0] : returnType;

                    // Detect various bad/unsafe types
                    bool isBadReturnType = unwrappedType == typeof(object) || unwrappedType == typeof(ActionResult) || unwrappedType == typeof(ActionResult<object>) || unwrappedType == typeof(IActionResult);

                    // Allow:
                    // - void or Task types to indicate no return value
                    // - ActionResult<T> to indicate typed return value (with possibility of errors)
                    bool isValidReturnType =
                        !isBadReturnType &&
                        (returnType == typeof(Task) || returnType == typeof(void) || unwrappedType.IsGenericTypeOf(typeof(ActionResult<>)));

                    if (!isValidReturnType)
                        Serilog.Log.Logger.Warning("{Controller}.{Method}() returns {ReturnType}. Use {RecommendedOkType} instead (or {EmptyRecommendedType} if there is no return value).", controller.ToGenericTypeString(), methodInfo.Name, returnType.ToGenericTypeString(), isGenericTask ? "Task<ActionResult<T>>" : "ActionResult<T>", "Task/void");
                    else
                        Serilog.Log.Logger.Debug("{Controller}.{Method}() returns {ReturnType}", controller.ToGenericTypeString(), methodInfo.Name, returnType.ToGenericTypeString());
#endif
                }

                if (!controller.IsPublic)
                    throw new InvalidOperationException($"AdminApi controller {controller.ToGenericTypeString()} is not public. Controllers must be public.");
            }

            // Check that all defined permissions are used at least once by a controller. The only exception are
            // permissions that are only ever checked on the Dashboard - these are marked as DashboardOnly inside
            // the Permission attribute
            foreach (PermissionGroupDefinition permissionGroup in permissionGroups)
            {
                foreach (PermissionDefinition permissionDefinition in permissionGroup.Permissions)
                {
                    if (permissionDefinition.IsDashboardOnly)
                        continue;
                    if (usedPermissions.Contains(permissionDefinition.Name))
                        continue;
                    if (!permissionDefinition.IsEnabled)
                        continue;

                    throw new InvalidOperationException($"Unused permission. The permission {permissionDefinition.Name} has a [Permission] definition in group [AdminApiPermissionGroup(\"{permissionGroup.Title}\")] but the permission is not referenced inside an [RequirePermission] attribute by any server code.");
                }
            }
        }
    }

    /// <summary>
    /// Base class for all Dashboard-facing AdminApi endpoints. Any controller derived from this
    /// class will have routes starting with '/api'.
    ///
    /// <see cref="OkObjectResult"/>, <see cref="ObjectResult"/>, and <see cref="JsonResult"/> results will be wrapped in a <see cref="JsonResultWithErrors"/>
    /// This means that any errors thrown in serialization will not stop serialization, the serializer will ignore the offending property and move on to the next property.
    ///
    /// Includes helpers for:
    /// - Authorization helpers
    /// - Writing audit logs
    /// </summary>
    [Route(RoutePathPrefix)]
    [EnableCors(CorsPolicy)]
    [Authorize(AuthenticationSchemes = AuthenticationScheme)]
    public abstract class MetaplayAdminApiController : MetaplayController
    {
        public const string RoutePathPrefix      = "api";
        public const string CorsPolicy           = "AdminApiCorsPolicy";
        public const string AuthenticationScheme = "AdminApi";

        static readonly JsonSerializerSettings _baseSettings = new JsonSerializerSettings();

        static MetaplayAdminApiController()
        {
            AdminApiJsonSerialization.ApplySettings(_baseSettings);
        }

        [NonAction]
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Result is JsonResult jsonResult)
            {
                context.Result = new JsonResultWithErrors(jsonResult.Value, jsonResult.SerializerSettings ?? _baseSettings, _logger)
                {
                    ContentType = jsonResult.ContentType,
                    StatusCode = jsonResult.StatusCode,
                };
            }
            else if (context.Result is ObjectResult objectResult && objectResult.Value is not string)
            {
                context.Result = new JsonResultWithErrors(objectResult.Value, _baseSettings, _logger)
                {
                    ContentType = objectResult.ContentTypes.FirstOrDefault(),
                    StatusCode  = objectResult.StatusCode,
                };
            }

            base.OnActionExecuted(context);
        }

        /// <summary>
        /// Return the id of the current user. If auth /is not/ enabled then this
        /// simply returns "auth_not_enabled". If auth /is/ enabled then we return
        /// one of the following:
        ///     1) Email of the user, or if that cannot be found
        ///     2) user_id from the oauth token, of if that cannot be found
        ///     3) "no_id" - this is returned if the user is not authenticated yet
        /// There are two version of this function:
        ///     1) GetUserId() for use inside endpoints where user is implicitly known
        ///     2) GetUserId(user) for use outside endpoints (eg: exception filter)
        /// </summary>
        /// <returns></returns>
        protected string GetUserId()
        {
            return GetUserId(User);
        }

        static public string GetUserId(ClaimsPrincipal user)
        {
            AdminApiOptions authOpts = RuntimeOptionsRegistry.Instance.GetCurrent<AdminApiOptions>();

            switch (authOpts.Type)
            {
                case AuthenticationType.None:
                    return "auth_not_enabled";

                case AuthenticationType.JWT:
                    string userId = user.Claims.FirstOrDefault(claim => claim.Type == "https://schemas.metaplay.io/email")?.Value;
                    if (string.IsNullOrEmpty(userId))
                        userId = user.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Email)?.Value;
                    if (string.IsNullOrEmpty(userId))
                        userId = user.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;
                    if (string.IsNullOrEmpty(userId))
                        return "no_id";
                    return userId;

                default:
                    throw new InvalidOperationException("Invalid or incomplete auth type chosen.");
            }
        }

        /// <summary>
        /// Return a list of roles that are active for the user making the HTTP request, modified by Metaplay-AssumedUserRoles where applicable.
        ///
        /// The following rules are followed (in the specified order):
        /// - If AdminApiOptions.Type == None:
        ///   - If authOpts.NoneConfiguration.AllowAssumeRoles is true and the HTTP header `Metaplay-AssumedUserRoles` specifies roles, those roles are used (\note We trust the client as they'd have game-admin rights anyway)
        ///   - Otherwise, AdminApiOptions.NoneConfiguration.DefaultRole is used.
        /// - If AdminApiOptions.Type == JWT:
        ///   - Extract the roles using ResolveActualUserRoles()
        /// </summary>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        static public string[] GetActiveUserRoles(HttpContext httpContext)
        {
            // Resolve roles the user actually has
            string[] resolvedRoles = ResolveActualUserRoles(httpContext);

            // AuthenticationType-specific handling
            AdminApiOptions authOpts = RuntimeOptionsRegistry.Instance.GetCurrent<AdminApiOptions>();
            if (authOpts.Type == AuthenticationType.None)
            {
                // Override with default role if no actual roles found
                if (resolvedRoles.Length == 0)
                    resolvedRoles = [authOpts.NoneConfiguration.DefaultRole];

                // Apply assumed roles, if provided and allowed.
                // \todo Always allow assuming roles for GameAdmins ?
                if (authOpts.NoneConfiguration.AllowAssumeRoles)
                {
                    // Try to grab the list of assumed roles from the special request header and fall back to a
                    // default value if the cookie does not exist or if no assumed roles are provided in the request
                    if (httpContext.Request.Headers.ContainsKey("Metaplay-AssumedUserRoles"))
                    {
                        string[] assumedRoles = httpContext.Request.Headers.GetCommaSeparatedValues("Metaplay-AssumedUserRoles");
                        resolvedRoles = assumedRoles;
                    }
                }
            }

            return resolvedRoles;
        }

        /// <summary>
        /// Response type for the /currentroles endpoint.
        /// </summary>
        //public class CurrentRolesResponse
        //{
        //    public string[] Roles { get; set; }
        //}

        // Example Metaplay-Userinfo header payload: {
        //   "amr":["oidc"],
        //   "aud":["metaplay.idler.test"],
        //   "auth_time":1712686234,
        //   "email":"account@metaplay.io",
        //   "email_verified":false,
        //   "https://schemas.metaplay.io/email":"account@metaplay.io",
        //   "https://schemas.metaplay.io/roles":["metaplay.idler.test.game-admin"],
        //   "iat":1712686235,
        //   "iss":"https://auth.metaplay.dev",
        //   "rat":1712686228,
        //   "sub":"f61b75ad-7e0c-4ac3-846a-33f2eaa7ad33",
        //   "updated_at":1695021941767
        // }
        public class UserInfoHeader
        {
            [JsonProperty("aud")]
            public string[] Audience        { get; set; }
            [JsonProperty("auth_time")]
            public int      AuthenticatedAt { get; set; }
            [JsonProperty("email")]
            public string   Email           { get; set; }
            [JsonProperty("email_verified")]
            public bool     EmailVerified   { get; set; }
            [JsonProperty("https://schemas.metaplay.io/email")]
            public string   MetaplayEmail   { get; set; }
            [JsonProperty("https://schemas.metaplay.io/roles")]
            public string[] MetaplayRoles   { get; set; }
            [JsonProperty("iat")]
            public int      IssuedAt        { get; set; }
            [JsonProperty("iss")]
            public string   Issuer          { get; set; }
            [JsonProperty("sub")]
            public string   MetaplayId      { get; set; }
        }

        /// <summary>
        /// Filter a list of fully-qualified role names (eg, metaplay.idler.develop.game-admin) that match
        /// the current environment's role prefix (eg, metaplay.idler.develop.) and returns the matching
        /// entries with the role prefixed removed (eg, game-admin).
        /// </summary>
        /// <param name="prefixedRoles"></param>
        /// <returns></returns>
        static IEnumerable<string> FilterMatchingRolesOnlyAndShorten(IEnumerable<string> prefixedRoles, string rolePrefix)
        {
            foreach (string prefixedRole in prefixedRoles)
            {
                if (prefixedRole.StartsWith(rolePrefix, StringComparison.Ordinal))
                    yield return prefixedRole.Substring(rolePrefix.Length);
            }
        }

        /// <summary>
        /// Resolve the roles for the specific user. Uses the following rules:
        /// If Metaplay-Userinfo header is present and has 'https://schemas.metaplay.io/roles':
        ///   Use roles from the Metaplay-Userinfo header
        /// Otherwise:
        ///   Resolve roles from the user's token (an id token must be provided)
        /// </summary>
        /// <param name="context">HttpContext for the request</param>
        /// <returns>Array of shorthand role names that the user has for this environment (eg, game-admin)</returns>
        static string[] ResolveActualUserRoles(HttpContext context)
        {
            AdminApiOptions opts = RuntimeOptionsRegistry.Instance.GetCurrent<AdminApiOptions>();
            string rolePrefix = opts.GetRolePrefix();

            // Try the Metaplay-Userinfo header first: if it contains roles, use them, otherwise use the old code paths
            if (context.Request.Headers.TryGetValue("Metaplay-Userinfo", out StringValues userInfoHeader))
            {
                // \todo also support non-base64 encoded payload
                string userInfoJson = Encoding.UTF8.GetString(Convert.FromBase64String(userInfoHeader.ToString()));
                UserInfoHeader header = JsonSerialization.Deserialize<UserInfoHeader>(userInfoJson);
                if (header.MetaplayRoles != null)
                    return FilterMatchingRolesOnlyAndShorten(header.MetaplayRoles, rolePrefix).ToArray();
            }

#if false // code for querying /userinfo endpoint manually
            if (opts.Type == AuthenticationType.JWT && opts.JwtConfiguration.CurrentRolesUri != null)
            {
                // Request the roles from the external CurrentRolesUri endpoint
                string accessToken = await context.GetTokenAsync(MetaplayAdminApiController.AuthenticationScheme, "access_token");
                CurrentRolesResponse response = await HttpUtil.RequestJsonGetAsync<CurrentRolesResponse>(
                    HttpUtil.DefaultJsonClient,
                    opts.JwtConfiguration.CurrentRolesUri,
                    new AuthenticationHeaderValue("Bearer", accessToken));
                if (response.Roles == null)
                    return [];

                // Only keep the roles for this environment & drop the prefixes
                return FilterMatchingRolesOnlyAndShorten(response.Roles, rolePrefix).ToArray();
            }
#endif

            // Get all valid roles for the user from the provided id token
            string issuer = opts.GetAdminApiDomain();
            IEnumerable<string> matchingPrefixedRoles =
                context.User
                .FindAll(claim => issuer == null || claim.Issuer == issuer)             // Find all claims with matching issuer (or all claim if no issuer specified)
                .Where(claim => claim.Type == "https://schemas.metaplay.io/roles")      // Select all Metaplay role claims
                .Select(claim => claim.Value);                                          // Grab values (names of roles)

            // Only choose roles starting with this environment's prefix (eg, 'metaplay.idler.demo.')
            // Drop the environment name to get the pure role names of the role (eg, 'game-admin')
            return FilterMatchingRolesOnlyAndShorten(matchingPrefixedRoles, rolePrefix).ToArray();
        }

        /// <summary>
        /// Given a list of roles, return the set of unique permissions that
        /// are available to these roles.
        /// </summary>
        /// <param name="roles"></param>
        /// <returns></returns>
        static public string[] GetPermissionsFromRoles(string[] roles)
        {
            AdminApiOptions adminApiOpts = RuntimeOptionsRegistry.Instance.GetCurrent<AdminApiOptions>();
            return adminApiOpts.RolePermissions
                .Where(rolePermissions => roles.Contains(rolePermissions.Key))
                .SelectMany(rolePermissions => rolePermissions.Value)
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// Check if any role in a set of roles has the given permission. The role names must not
        /// contain the environment prefix, but must be the pure role name, eg, 'game-admin'.
        /// </summary>
        /// <param name="roles"></param>
        /// <param name="permission"></param>
        /// <returns></returns>
        public static bool HasPermissionForRoles(string[] roles, string permission)
        {
            AdminApiOptions adminApiOpts = RuntimeOptionsRegistry.Instance.GetCurrent<AdminApiOptions>();
            return adminApiOpts.ResolvedPermissions.RolesForPermission[permission].Any(role => roles.Contains(role));
        }

        // \note Audit log writes are here because they access the AdminApi-specific userId. The methods
        //       could be moved to MetaplayBaseController with injection of the userId from here.
        #region Audit log

        /// <summary>
        /// Log a single audit log event
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        protected async Task WriteAuditLogEventAsync(EventBuilder builder)
        {
            MetaTime timeNow = MetaTime.Now;

            // Create persisted audit log event
            AuditLog.EventId eventId = AuditLog.EventId.FromTime(timeNow);
            EventSource source = EventSource.FromAdminApi(GetUserId());
            IPAddress sourceIpAddress = TryGetRemoteIp();
            PersistedAuditLogEvent entry = new PersistedAuditLogEvent(eventId, source, builder.Target, sourceIpAddress, builder.Payload);
            LogEventToConsole(builder.Target, builder.Payload, eventId);

            // Write to DB
            await MetaDatabase.Get().InsertAsync(entry);
        }

        /// <summary>
        /// Log a group of related audit log events with many-to-many relationships
        /// </summary>
        /// <param name="builders"></param>
        /// <returns></returns>
        protected async Task WriteRelatedAuditLogEventsAsync(List<EventBuilder> builders)
        {
            MetaTime timeNow = MetaTime.Now;

            // Give each event an id
            List<(EventBuilder builder, AuditLog.EventId id)> buildersWithIds = new List<(EventBuilder, AuditLog.EventId)>();
            List<AuditLog.EventId> eventIds = new List<AuditLog.EventId>();
            builders.ForEach(builder =>
            {
                AuditLog.EventId eventId = AuditLog.EventId.FromTime(timeNow);
                buildersWithIds.Add((builder, eventId));
                eventIds.Add(eventId);
            });

            // Create persisted audit log events
            EventSource source = EventSource.FromAdminApi(GetUserId());
            IPAddress sourceIpAddress = TryGetRemoteIp();
            List<PersistedAuditLogEvent> entries = new List<PersistedAuditLogEvent>();
            buildersWithIds.ForEach(builderWithId =>
            {
                List<AuditLog.EventId> relatedIds = eventIds.FindAll(x => x != builderWithId.id).ToList();
                builderWithId.builder.Payload.SetRelatedEventIds(relatedIds);
                PersistedAuditLogEvent entry = new PersistedAuditLogEvent(builderWithId.id, source, builderWithId.builder.Target, sourceIpAddress, builderWithId.builder.Payload);
                entries.Add(entry);
                LogEventToConsole(builderWithId.builder.Target, builderWithId.builder.Payload, builderWithId.id);
            });

            // Write to DB
            await MetaDatabase.Get().MultiInsertOrIgnoreAsync(entries);
        }


        /// <summary>
        /// Log a parent audit log event with a one-to-many relationship to children events
        /// </summary>
        /// <param name="parentBuilder"></param>
        /// <param name="childBuilders"></param>
        /// <returns></returns>
        protected async Task WriteParentWithChildrenAuditLogEventsAsync(EventBuilder parentBuilder, List<EventBuilder> childBuilders)
        {
            MetaTime timeNow = MetaTime.Now;

            // Give each child event and id
            List<(EventBuilder builder, AuditLog.EventId id)> childBuildersWitIds = new List<(EventBuilder, AuditLog.EventId)>();
            List<AuditLog.EventId> childEventIds = new List<AuditLog.EventId>();
            childBuilders.ForEach(builder =>
            {
                AuditLog.EventId eventId = AuditLog.EventId.FromTime(timeNow);
                childBuildersWitIds.Add((builder, eventId));
                childEventIds.Add(eventId);
            });

            EventSource source = EventSource.FromAdminApi(GetUserId());
            List<PersistedAuditLogEvent> entries = new List<PersistedAuditLogEvent>();

            // Create parent persisted audit log events
            IPAddress sourceIpAddress = TryGetRemoteIp();
            AuditLog.EventId parentEventId = AuditLog.EventId.FromTime(timeNow);
            parentBuilder.Payload.SetRelatedEventIds(childEventIds);
            PersistedAuditLogEvent parentEntry = new PersistedAuditLogEvent(parentEventId, source, parentBuilder.Target, sourceIpAddress, parentBuilder.Payload);
            entries.Add(parentEntry);
            LogEventToConsole(parentBuilder.Target, parentBuilder.Payload, parentEventId);

            // Create children persisted audit log events
            List<AuditLog.EventId> parentIdAsList = new List<AuditLog.EventId> { parentEventId };
            childBuildersWitIds.ForEach(builderWithId =>
            {
                builderWithId.builder.Payload.SetRelatedEventIds(parentIdAsList);
                PersistedAuditLogEvent entry = new PersistedAuditLogEvent(builderWithId.id, source, builderWithId.builder.Target, sourceIpAddress, builderWithId.builder.Payload);
                entries.Add(entry);
                LogEventToConsole(builderWithId.builder.Target, builderWithId.builder.Payload, builderWithId.id);
            });

            // Write to DB
            await MetaDatabase.Get().MultiInsertOrIgnoreAsync(entries);
        }

        private void LogEventToConsole(EventTarget target, EventPayloadBase payload, Server.AdminApi.AuditLog.EventId id)
        {
            _logger.LogInformation("Audit Log event: {Target} {EventClass} {EventId}", target, payload.GetType().FullName, id);
        }

        #endregion // Audit log
    }
}
