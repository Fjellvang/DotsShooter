// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Metaplay.Server.AdminApi;

/// <summary>
/// Integration point for adding custom services to the admin api.
/// The <see cref="ConfigureServices"/> method will be called during host creation to configure extra services.
/// </summary>
public interface IAdminApiServices : IMetaIntegration<IAdminApiServices>
{
    public void ConfigureServices(IServiceCollection builderServices, AuthenticationDomainConfig[] authDomains);
}
