// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Services.Geolocation
{
    /// <summary>
    /// Periodically reports certain geolocation-related metrics
    /// </summary>
    internal class GeolocationMetricsReporter : IAsyncDisposable
    {
        static Prometheus.Gauge c_residentDatabaseAge = Prometheus.Metrics.CreateGauge("game_geolocation_database_age", "Age of currently-resident geolocation database (in seconds)");

        GeolocationResidentStorage _residentStorage;

        bool _isDisposed = false;
        Task _mainLoopTask;
        CancellationTokenSource _mainLoopCts;

        public GeolocationMetricsReporter(GeolocationResidentStorage residentStorage)
        {
            _residentStorage = residentStorage ?? throw new ArgumentNullException(nameof(residentStorage));

            _mainLoopCts = new CancellationTokenSource();
            _mainLoopTask = Task.Run(() => MainLoopAsync(_mainLoopCts.Token));
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;

            // Trigger main loop cancellation and wait for it to complete.
            // Main loop shouldn't throw, but we're being defensive here by ignoring exceptions.

            _mainLoopCts.Cancel();
            try
            {
                await _mainLoopTask;
            }
            catch
            {
            }

            _mainLoopTask.Dispose();
            _mainLoopCts.Dispose();
        }

        static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

        async Task MainLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    ReportMetrics();
                }
                catch (Exception)
                {
                }

                try
                {
                    await Task.Delay(TickInterval, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        void ReportMetrics()
        {
            DateTime                        currentTime             = DateTime.UtcNow;
            GeolocationDatabaseMetadata?    residentMetadataMaybe   = _residentStorage.ResidentDatabaseMaybe?.Metadata;

            if (residentMetadataMaybe.HasValue)
            {
                GeolocationDatabaseMetadata residentMetadata = residentMetadataMaybe.Value;
                c_residentDatabaseAge.Set((currentTime - residentMetadata.BuildDate).TotalSeconds);
            }
        }
    }
}
