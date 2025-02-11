// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Services;
using Metaplay.Core.Network;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for various user related functions
    /// </summary>
    public class UserController : GameAdminApiController
    {
        public class UserInfoResponse
        {
            public required string  Sub     { get; init; }  // Metaplay Id
            public required string  Name    { get; init; }
            public required string  Email   { get; init; }
            public required string  Picture { get; init; }
            public string[]         Roles   { get; init; }
        }

        /// <summary>
        /// API endpoint to query info about the current dashboard user
        /// Usage:  GET /api/userInfo
        /// Test:   curl http://localhost:5550/api/userInfo
        /// </summary>
        [HttpGet("userInfo")]
        [RequirePermission(MetaplayPermissions.Anyone)]
        public async Task<ActionResult<UserInfoResponse>> GetUserInfo()
        {
            AdminApiOptions opts = RuntimeOptionsRegistry.Instance.GetCurrent<AdminApiOptions>();
            switch (opts.Type)
            {
                case AuthenticationType.Disabled:
                case AuthenticationType.None:
                    return new UserInfoResponse
                    {
                        Sub = null, // Metaplay Id
                        Name = "n/a",
                        Email = "n/a",
                        Picture = null,
                    };

                case AuthenticationType.JWT:
                    // Request the payload from the external UserInfoUri endpoint
                    string accessToken = await HttpContext.GetTokenAsync(MetaplayAdminApiController.AuthenticationScheme, "access_token");
                    return await HttpUtil.RequestJsonGetAsync<UserInfoResponse>(
                        HttpUtil.SharedJsonClient,
                        opts.JwtConfiguration.UserInfoUri,
                        new AuthenticationHeaderValue("Bearer", accessToken));

                default:
                    throw new MetaHttpRequestError($"Unsupported AuthenticationType.{opts.Type}");
            }
        }

        public class UserResponse
        {
            public required string[] Roles          { get; init; }
            public required string[] Permissions    { get; init; }
        }

        /// <summary>
        /// Returns information about the current user
        /// Usage:  GET /api/authDetails/user
        /// </summary>
        /// <returns></returns>
        [HttpGet("authDetails/user")]
        [RequirePermission(MetaplayPermissions.Anyone)]
        public ActionResult<UserResponse> GetUser()
        {
            // NB: The return values here are untrusted. If we are using authentication
            // then these could have been spoofed in the JWT - that's ok, the Dashboard
            // cannot do anything malicious with them and we don't trust them inside
            // the server code
            string[] roles       = GetActiveUserRoles(HttpContext);
            string[] permissions = GetPermissionsFromRoles(roles);

            return new UserResponse
            {
                Roles       = roles,
                Permissions = permissions,
            };
        }

        public class AllPermissionsAndRolesResponse
        {
            public class Permission
            {
                public required string Name         { get; init; }
                public required string Description  { get; init; }
            }

            public class PermissionGroup
            {
                public required string          Title       { get; init; }
                public required Permission[]    Permissions { get; init; }
            }

            public required PermissionGroup[]   PermissionGroups    { get; init; }
            public required string[]            Roles               { get; init; }
        }

        /// <summary>
        /// Returns a list of all defined permissions and roles. Used by the Dashboard
        /// Usage:  GET /api/authDetails/allPermissionsAndRoles
        /// </summary>
        /// <returns></returns>
        [HttpGet("authDetails/allPermissionsAndRoles")]
        [RequirePermission(MetaplayPermissions.Anyone)]
        public ActionResult<AllPermissionsAndRolesResponse> GetAllPermissionsAndRoles()
        {
            AdminApiOptions adminApiOpts = RuntimeOptionsRegistry.Instance.GetCurrent<AdminApiOptions>();
            return new AllPermissionsAndRolesResponse
            {
                PermissionGroups = AdminApiOptions.GetAllPermissions()
                    .Select(permissionGroup => new AllPermissionsAndRolesResponse.PermissionGroup
                    {
                        Title = permissionGroup.Title,
                        Permissions = permissionGroup.Permissions
                            .Where(perm => perm.IsEnabled)
                            .Where(perm => !RequirePermissionAttribute.FilterFromDashboard(perm.Name))
                            .Select(permission => new AllPermissionsAndRolesResponse.Permission
                            {
                                Name        = permission.Name,
                                Description = permission.Description
                            }).ToArray()
                    }).ToArray(),
                Roles = adminApiOpts.Roles.ToArray()
            };
        }
    }
}
