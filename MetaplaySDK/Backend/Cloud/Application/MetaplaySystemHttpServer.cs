// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using Serilog;
using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Cloud.Application
{
    /// <summary>
    /// System HTTP server, used to communicate with the infrastructure. Supports health checks, readiness
    /// checks, and request to gracefully shutdown the cluster.
    /// </summary>
    public class MetaplaySystemHttpServer : BackgroundService
    {
        [DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        static extern void WindowsMemset(IntPtr dst, int c, long count);

        [DllImport("libc.so.6", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        static extern void LinuxMemset(IntPtr dst, int c, long count);

        [DllImport("libc.so.7", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        static extern void FreeBsdMemset(IntPtr dst, int c, long count);

        [DllImport("libc.dylib", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        static extern void OsxMemset(IntPtr dst, int c, long count);

        readonly Serilog.ILogger        _log = Log.ForContext<MetaplaySystemHttpServer>();
        readonly EnvironmentOptions     _opts;
        readonly WebApplication         _app;

        public MetaplaySystemHttpServer(EnvironmentOptions envOpts)
        {
            _opts = envOpts;
            _app = BuildApp(envOpts);
        }

        WebApplication BuildApp(EnvironmentOptions envOpts)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(Array.Empty<string>());

            // Don't capture Ctrl-C
            builder.Services.AddSingleton<IHostLifetime>(new SimpleHostLifetime());

            // Use Serilog for logging
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog();

            builder.AddAndConfigureResponseCompression();

            WebApplication app = builder.Build();

            app.UseResponseCompression();

            // Configure listen host & port
            app.Urls.Add(Invariant($"http://{envOpts.SystemHttpListenHost}:{envOpts.SystemHttpPort}"));

            // Root endpoint
            app.MapGet("/", () => "Ok, but nothing here!");

            // Basic health check (always respond with healthy status)
            app.MapGet("/healthz", () =>
            {
                _log.Debug("Responding to health check with OK (StatusCode=200)");
                return "Node is healthy!";
            });

            // Check whether node is ready (cluster services are initialized -- we're ready to receive traffic)
            app.MapGet("/isReady", () =>
            {
                if (ClusterCoordinatorActor.IsReady())
                    return Results.Content("Node is ready!");
                else
                    return Results.Problem("Node is not ready!", statusCode: (int)HttpStatusCode.ServiceUnavailable);
            });

            // Request cluster to shut down gracefully
            app.MapPost("/gracefulShutdown", () =>
            {
                _log.Information("Graceful cluster shutdown requested");
                ClusterCoordinatorActor.RequestClusterShutdown();
                return "Cluster graceful shutdown sequence initiated!";
            });

            // Warn about accidentally using GET /gracefulShutdown (instead of POST)
            app.MapGet("/gracefulShutdown", () =>
            {
                _log.Error("Received GET request to /gracefulShutdown -- must use POST instead!");
                return "Received GET request to /gracefulShutdown -- must use POST instead!";
            });

            // Force the process to hard crash with a segfault, for debugging purposes only
            app.MapGet("/forceSegfault", () =>
            {
                _log.Error("Triggering a forced segfault due to GET request at /forceSegfault");
                try
                {
                    IntPtr badPtr = 12345;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        WindowsMemset(badPtr, 0xcd, 100);
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        LinuxMemset(badPtr, 0xcd, 100);
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                        FreeBsdMemset(badPtr, 0xcd, 100);
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        OsxMemset(badPtr, 0xcd, 100);
                    else
                        _log.Error($"Unsupported OS for forced segfault: {Environment.OSVersion}");
                }
                finally
                {
                    Environment.FailFast("Forced segfault did not terminate the process, exiting immediately..");
                }
            });

            // Return 404 for all unknown paths
            app.MapFallback(async ctx =>
            {
                string sanitizedPath = Util.SanitizePathForDisplay(ctx.Request.Path);
                _log.Warning("Request to invalid path: {SystemHttpPath}", sanitizedPath);
                ctx.Response.StatusCode = 404;
                await ctx.Response.WriteAsync($"Error: Invalid path '{ctx.Request.Path}'");
            });

            return app;
        }

        protected override Task ExecuteAsync(CancellationToken ct)
        {
            _log.Information("Starting Metaplay system HTTP server on {SystemHttpHost}:{SystemHttpPort}..", _opts.SystemHttpListenHost, _opts.SystemHttpPort);
            return _app.RunAsync(ct);
        }
    }

    public class MetaplayMetricsHttpServer : BackgroundService
    {
        readonly IWebHost _app;

        public MetaplayMetricsHttpServer(EnvironmentOptions envOpts)
        {
            IWebHostBuilder builder = new WebHostBuilder()
                .UseKestrelCore()
                .Configure(
                    app =>
                    {
                        // Add Prometheus ASP.NET exporter
                        app.UseMetricServer(settings => { });
                    })
                .UseUrls(Invariant($"http://+:{envOpts.MetricPort}"))
                .SuppressStatusMessages(true);

            builder.ConfigureServices(
                services =>
                {
                    services.AddSingleton<IHostLifetime>(new SimpleHostLifetime());
                });

            _app = builder.Build();
        }

        protected override Task ExecuteAsync(CancellationToken ct)
        {
            return _app.RunAsync(ct);
        }
    }

}
