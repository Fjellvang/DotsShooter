// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Metaplay.Cloud.RuntimeOptions
{
    /// <summary>
    /// Base class for declaring a section of RuntimeOptions. The deriving concrete
    /// class should also be annotated with <see cref="RuntimeOptionsAttribute"/>.
    /// </summary>
    public abstract class RuntimeOptionsBase
    {
        public static bool IsServerApplication      => RuntimeEnvironmentInfo.Instance.ApplicationName == "Server";
        public static bool IsBotClientApplication   => RuntimeEnvironmentInfo.Instance.ApplicationName == "BotClient";

        public static bool IsLocalEnvironment       => RuntimeEnvironmentInfo.Instance.EnvironmentFamily == EnvironmentFamily.Local;
        public static bool IsDevelopmentEnvironment => RuntimeEnvironmentInfo.Instance.EnvironmentFamily == EnvironmentFamily.Development;
        public static bool IsStagingEnvironment     => RuntimeEnvironmentInfo.Instance.EnvironmentFamily == EnvironmentFamily.Staging;
        public static bool IsProductionEnvironment  => RuntimeEnvironmentInfo.Instance.EnvironmentFamily == EnvironmentFamily.Production;
        public static bool IsCloudEnvironment       => !IsLocalEnvironment;

        [IgnoreDataMember]
        protected IMetaLogger Log { get; }

        protected RuntimeOptionsBase()
        {
            Log = MetaLogger.ForContext(GetType());
        }

        /// <summary>
        /// Called after option fields have been resolved from config sources but before RuntimeOption becomes
        /// visible as Current in RuntimeOptionsRegisty. This method is useful for validating config,
        /// computing [ComputedValue] fields, and resolving secrets or other external data.
        /// </summary>
        public virtual Task OnLoadedAsync() => Task.CompletedTask;

        /// <inheritdoc cref="OnLoadedAsync()"/>
        public virtual Task OnLoadedAsync(RuntimeOptionsRegistry registry) => OnLoadedAsync();

        /// <summary>
        /// Wait for the given options to be ready and return it when it is. This is intended to be
        /// used from <see cref="RuntimeOptionsBase.OnLoadedAsync"/> to enable simple dependencies
        /// between runtime option types.
        ///
        /// <para>
        /// Note that this method only allows fetching other runtime options during the initialization
        /// but changes during runtime to the dependee will not trigger updates to where the values
        /// are used!
        /// </para>
        ///
        /// <para>
        /// This mechanism will eventually be replaced with proper support for declaring dependencies
        /// between runtime options types, including updating all the dependent types when the dependee
        /// changes.
        /// </para>
        /// </summary>
        /// <typeparam name="TOptions">Type of runtime options to wait for</typeparam>
        /// <returns>Reference to the options</returns>
        /// <exception cref="TimeoutException">Thrown if the requested options block isn't available in 10 seconds</exception>
        protected async Task<TOptions> GetDependencyAsync<TOptions>(RuntimeOptionsRegistry registry) where TOptions : RuntimeOptionsBase
        {
            // Wait for the result for 10sec
            for (int ndx = 0; ndx < 1000; ndx++)
            {
                TOptions opts = registry.GetCurrent<TOptions>();
                if (opts != null)
                    return opts;

                await Task.Delay(10);
            }

            throw new TimeoutException($"Timeout while waiting for runtime options type {typeof(TOptions).ToGenericTypeString()} to be available");
        }
    }
}
