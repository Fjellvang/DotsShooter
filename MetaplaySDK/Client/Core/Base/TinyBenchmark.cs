// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Diagnostics;
using System.Linq;
using static System.FormattableString;

namespace Metaplay.Core
{
    /// <summary>
    /// A simple benchmarking class for ad-hoc tests.
    /// </summary>
    public class TinyBenchmark
    {
        /// <summary>
        /// Choose between fast runs or accurate results.
        /// </summary>
        public enum Mode
        {
            Fast = 0,       //!< Optimize for speed -- takes about 5sec per benchmark (plus 5sec for pre-warming)
            Accurate = 1,   //!< Optimize for accuracy -- takes about 30sec per benchmark
        }

        /// <summary>
        /// Run benchmark for a single operation. If benchmarking multiple related operations, use
        /// <see cref="MultiExecute(Mode, ValueTuple{string, Action}[])"/> instead for more reliable results.
        /// </summary>
        /// <param name="mode">Benchmarking mode (fast vs accurate)</param>
        /// <param name="name">Name of the operation</param>
        /// <param name="op">Operation callback</param>
        public static void Execute(Mode mode, string name, Action op)
        {
            MultiExecute(mode, (name, op));
        }

        /// <summary>
        /// Run a benchmark for a group of operations. Interleaves the running of the operations
        /// such that the results would still remain comparable in the case of CPU throttling.
        /// </summary>
        /// <param name="mode">Benchmarking mode (fast vs accurate)</param>
        /// <param name="benchmarks">An array of (Name, Operation) tuples to benchmark.</param>
        /// <exception cref="ArgumentException"></exception>
        public static void MultiExecute(Mode mode, params (string Name, Action Op)[] benchmarks)
        {
            MultiExecuteImpl(mode, benchmarks);
        }

        [Obsolete("Deprecated! Use the MultiExecute() if benchmarking a group of methods or Execute() for a single method")]
        public static void Execute(string name, int numRepeats, Action op)
        {
            Execute(Mode.Fast, name, op);
        }

        static void MultiExecuteImpl(Mode mode, (string Name, Action Op)[] benchmarks)
        {
#if UNITY_EDITOR
            DebugLog.WriteLine("WARNING: RUNNING BENCHMARKS IN UNITY EDITOR -- the results will not be very representative of the performance in final builds");
#elif UNITY_2018_1_OR_NEWER
#   if DEBUG || DEVELOPMENT_BUILD
            DebugLog.WriteLine("WARNING: RUNNING IN DEVELOPMENT BUILD -- run benchmark in release mode to get representative results");
#   endif
#elif !RELEASE
            DebugLog.WriteLine("WARNING: NOT RUNNING IN RELEASE MODE -- run benchmark in release mode, eg with 'dotnet run -c Release', to get representative results");
#endif

            int numBatches = mode switch
            {
                Mode.Fast => 50,
                Mode.Accurate => 250,
                _ => throw new ArgumentException($"Invalid Mode {mode}")
            };

            int numBenchmarks = benchmarks.Length;

            // Pre-warm by running the benchmarks for a specified amount of time
            TimeSpan prewarmDuration = TimeSpan.FromSeconds(5);
            Stopwatch swPrewarmTotal = Stopwatch.StartNew();
            while (true)
            {
                for (int benchmarkNdx = 0; benchmarkNdx < numBenchmarks; benchmarkNdx++)
                    DoReps(benchmarks[benchmarkNdx].Op, 10);

                if (swPrewarmTotal.Elapsed >= prewarmDuration)
                    break;
            }

            // DebugLog.WriteLine("Prewarm: {0}", PrettyPrintElapsed(swPrewarmTotal.Elapsed));

            // For each benchmark, find the numRepeats so that the total run takes 10..20ms
            int[] numRepeatsPerBenchmark = new int[numBenchmarks];
            for (int benchmarkNdx = 0; benchmarkNdx < numBenchmarks; benchmarkNdx++)
            {
                int numRepeats = 1;
                TimeSpan threshold = TimeSpan.FromMilliseconds(100);
                while (true)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    for (int ndx = 0; ndx < 10; ndx++)
                        DoReps(benchmarks[benchmarkNdx].Op, numRepeats);
                    sw.Stop();

                    TimeSpan elapsed = sw.Elapsed;
                    if (elapsed >= threshold)
                        break;

                    numRepeats *= 2;
                }

                numRepeatsPerBenchmark[benchmarkNdx] = numRepeats;
            }

            // DebugLog.WriteLine("Calibration done: {0}", string.Join(", ", numRepeatsPerBenchmark));

            // Execute each operation X times and measure each run.
            // Run operations in batches of 10 iterations to before switching benchmarks.
            const int BatchSize = 10;
            int numIters = numBatches * BatchSize;
            TimeSpan[][] totalElapsed = Enumerable.Range(0, numBenchmarks).Select(_ => new TimeSpan[numIters]).ToArray();
            long[] totalAllocated = new long[numBenchmarks];
            for (int batchNdx = 0; batchNdx < numBatches; batchNdx++)
            {
                for (int benchmarkNdx = 0; benchmarkNdx < numBenchmarks; benchmarkNdx++)
                {
                    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    for (int ndx = 0; ndx < BatchSize; ndx++)
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        DoReps(benchmarks[benchmarkNdx].Op, numRepeatsPerBenchmark[benchmarkNdx]);
                        sw.Stop();
                        totalElapsed[benchmarkNdx][batchNdx*BatchSize + ndx] = sw.Elapsed;
                    }
                    totalAllocated[benchmarkNdx] += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                }
            }

            // Print results
            int maxNameLength = benchmarks.Select(b => b.Name.Length).Max();
            for (int benchmarkNdx = 0; benchmarkNdx < numBenchmarks; benchmarkNdx++)
            {
                string name = benchmarks[benchmarkNdx].Name;
                string namePadding = new string(' ', maxNameLength - name.Length);

                // Compute average allocated bytes per iteration
                int numRepeats = numRepeatsPerBenchmark[benchmarkNdx];
                long bytesAllocated = (long)((double)totalAllocated[benchmarkNdx] / (numIters * numRepeats));

                // Iteration time is the average time of the 10% of fastest runs.
                // This seems to empirically give a low variance.
                Array.Sort(totalElapsed[benchmarkNdx]);
                int numSamplesToUse = numIters / 10;
                double opTime = SumTimeSpans(totalElapsed[benchmarkNdx].AsSpan().Slice(0, numSamplesToUse)).TotalMilliseconds / (numSamplesToUse * numRepeats);
                DebugLog.WriteLine("{0}:{1} {2}/op, {3} bytes allocated [{4} repeats]", name, namePadding, PrettyPrintElapsed(opTime), bytesAllocated, numRepeats);
            }
        }

        static void DoReps(Action action, int numRepeats)
        {
            for (int iter = 0; iter < numRepeats; iter++)
                action();
        }

        static TimeSpan SumTimeSpans(ReadOnlySpan<TimeSpan> inputs)
        {
            TimeSpan total = TimeSpan.Zero;
            foreach (TimeSpan input in inputs)
                total += input;
            return total;
        }

        static string PrettyPrintElapsed(double elapsedMS)
        {
            if (elapsedMS >= 1000.0)
                return Invariant($"{elapsedMS / 1000.0:0.00}s");
            else if (elapsedMS >= 1.0)
                return Invariant($"{elapsedMS:0.00}ms");
            else
                return Invariant($"{elapsedMS * 1000.0:0.00}us");
        }
    }
}
