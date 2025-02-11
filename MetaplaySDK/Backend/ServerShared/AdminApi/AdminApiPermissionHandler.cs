// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Server.AdminApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Threading.Tasks;

// \note Inject this into the controllers namespace so users don't need additional using statements
namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Mark an HTTP endpoint to require a specific AdminApi permission. To allow endpoint to be accessed by
    /// anyone who has been authenticated, you can use the special permission <c>MetaplayPermissions.Anyone</c>.
    /// See <c>MetaplayPermissions</c> in the <c>Metaplay.Server</c> project for built-in permissions, or
    /// define your own, typically in <c>GamePermissions</c>.
    /// </summary>
    public class RequirePermissionAttribute : TypeFilterAttribute
    {
        public const string SpecialPermissionAnyone = "<anyone>";

        public readonly string PermissionName;

        /// Used only for filtering out <see cref="SpecialPermissionAnyone"/> to not be sent to the dashboard.
        /// If more special permission are added, figure out what to do with this method.
        public static bool FilterFromDashboard(string permissionName) =>
            permissionName == SpecialPermissionAnyone;

        public RequirePermissionAttribute(string permissionName) : base(typeof(RequirePermissionFilter))
        {
            PermissionName = permissionName ?? throw new ArgumentNullException(nameof(permissionName), "Must provide a valid permission name!");

            Arguments = new[] { new AdminApiPermissionRequirement(permissionName) };
            Order = int.MinValue;
        }
    }
}

namespace Metaplay.Server.AdminApi
{
    /// <summary>
    /// Authorization error when no roles are found for the user making the request. This should not happen as the
    /// perimeter should only let requests thru that have valid roles specified.
    /// </summary>
    class NoRolesFoundFailureReason : AuthorizationFailureReason
    {
        public NoRolesFoundFailureReason(IAuthorizationHandler handler) : base(handler, "no roles found for the user")
        {
        }
    }

    /// <summary>
    /// Convert <see cref="RequirePermissionAttribute"/> to <see cref="AdminApiPermissionRequirement"/> so that they can be
    /// checked by <see cref="AdminApiPermissionHandler"/>.
    /// </summary>
    public class RequirePermissionFilter : Attribute, IAsyncAuthorizationFilter
    {
        private readonly IAuthorizationService _authService;
        private readonly AdminApiPermissionRequirement _requirement;

        public RequirePermissionFilter(IAuthorizationService authService, AdminApiPermissionRequirement requirement)
        {
            _authService = authService;
            _requirement = requirement;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            AuthorizationResult result = await _authService.AuthorizeAsync(context.HttpContext.User, null, _requirement);
            if (!result.Succeeded)
                context.Result = new ForbidResult();
        }
    }

    /// <summary>
    /// The <see cref="RequirePermissionAttribute"/> are converted to this type using the <see cref="RequirePermissionFilter"/>
    /// for validation using <see cref="AdminApiPermissionHandler"/>.
    /// </summary>
    public class AdminApiPermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; } // Required permission for the endpoint

        public AdminApiPermissionRequirement(string permission)
        {
            Permission = permission ?? throw new ArgumentNullException(nameof(permission));
        }
    }

    /// <summary>
    /// Handler for validating that invocations to AdminApi endpoints are done with the required role(s) as defined by
    /// <see cref="RequirePermissionAttribute"/> (subsequently converted to <see cref="AdminApiPermissionRequirement"/>.
    /// </summary>
    public class AdminApiPermissionHandler : AuthorizationHandler<AdminApiPermissionRequirement>
    {
        private readonly ILogger                _logger;
        private readonly IHttpContextAccessor   _httpContextAccessor;

        public AdminApiPermissionHandler(ILogger<AdminApiPermissionHandler> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// This handler validates whether the requester is allowed to access the given endpoint (ie, satisfies the permission requirement):
        /// - Resolve the valid role claims for this environment from the identity in the ASP.NET request context
        /// - Check whether the specified permission requirement is satisfied by any of the valid roles associated with the identity
        /// </summary>
        /// <param name="context">The HTTP request context</param>
        /// <param name="requirement">The permission requirement for the endpoint being accessed</param>
        /// <returns></returns>
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminApiPermissionRequirement requirement)
        {
            // Resolve the roles active for this user
            string[] roles = MetaplayAdminApiController.GetActiveUserRoles(_httpContextAccessor.HttpContext) ?? [];
            //_logger.LogDebug("Roles resolved for user {User}: {Roles}", MetaplayAdminApiController.GetUserId(context.User), PrettyPrint.Compact(roles));

            // Respond with roles used for the request
            _httpContextAccessor.HttpContext.Response.Headers.Append("Metaplay-ActiveRoles", new StringValues(roles));

            // Handle special permissions
            // \note Do this first to allow access even when the user has no roles (so that we can provide the proper error page)
            if (requirement.Permission == RequirePermissionAttribute.SpecialPermissionAnyone)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // If no roles found for the user, fail the check
            if (roles.Length == 0)
            {
                _logger.LogWarning("Requirement check for permission could not be satisfied due to no roles found for the user");
                context.Fail(new NoRolesFoundFailureReason(this));
                return Task.CompletedTask;
            }

            // Check if any of the roles are allowed to access the given permission
            if (MetaplayAdminApiController.HasPermissionForRoles(roles, requirement.Permission))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Otherwise, it's a failure
            _logger.LogWarning("Requirement check for permission {Permission} could not be satisfied by roles: {Roles}", requirement.Permission, string.Join(", ", roles));
            context.Fail(new MissingPermissionFailureReason(this, requirement.Permission));
            return Task.CompletedTask;
        }
    }
}
