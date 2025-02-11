// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Microsoft.Extensions.Hosting;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Metaplay.Cloud.Application
{
    /// <summary>
    /// Register SIGTERM and CTRL-C handler which gracefully shuts down the application (eg, shutdown all entity shards).
    /// </summary>
    public class ApplicationTerminationHandler : IDisposable
    {
        public readonly ManualResetEventSlim Terminated = new ManualResetEventSlim(false);

        ConsoleCancelEventHandler _ctrlCHandler   = null;
        PosixSignalRegistration   _sigtermHandler = null;

        void UnregisterHandlers()
        {
            _sigtermHandler?.Dispose();
            _sigtermHandler = null;

            Console.CancelKeyPress -= _ctrlCHandler;
            _ctrlCHandler          =  null;
        }

        public ApplicationTerminationHandler(IHostApplicationLifetime lifetime, IMetaLogger logger, bool enableKeyboardInput)
        {
            _sigtermHandler = PosixSignalRegistration.Create(
                PosixSignal.SIGTERM,
                (ctx) =>
                {
                    // Capture SIGTERM once
                    UnregisterHandlers();
                    ctx.Cancel = true;

                    logger.Warning("Got SIGTERM, allow process to terminate..");
                    Terminated.Set();
                    lifetime.StopApplication();
                });

            if (enableKeyboardInput)
            {
                _ctrlCHandler = (object sender, ConsoleCancelEventArgs e) =>
                {
                    // Capture CTRL-C once
                    if (e.SpecialKey == ConsoleSpecialKey.ControlC)
                    {
                        UnregisterHandlers();
                        e.Cancel = true;

                        logger.Warning("Got CTRL-C, allow process to terminate.. (press Ctrl-C again to force quit)");
                        Terminated.Set();
                        lifetime.StopApplication();
                    }
                };
                Console.CancelKeyPress += _ctrlCHandler;
            }
        }

        public void Dispose()
        {
            UnregisterHandlers();
        }
    }
}
