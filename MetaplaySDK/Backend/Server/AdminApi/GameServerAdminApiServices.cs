// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Server.AdminApi.Controllers;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Metaplay.Server.AdminApi;

public class GameServerAdminApiServices : IAdminApiServices
{
    public void ConfigureServices(IServiceCollection builderServices, AuthenticationDomainConfig[] authDomains)
    {
        builderServices.AddSingleton<IStatisticsPageReadStorageProvider, BusinessMetricsStatisticsPageReadStorageProvider>();
    }
}
