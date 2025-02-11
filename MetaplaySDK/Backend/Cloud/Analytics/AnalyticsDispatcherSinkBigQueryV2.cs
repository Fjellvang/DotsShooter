// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Analytics
{
    /// <summary>
    /// Writes events into a BigQuery table in a Firebase-like format.
    /// </summary>
    public class AnalyticsDispatcherSinkBigQueryV2 : AnalyticsDispatcherSinkBase
    {
        AnalyticsSinkBigQueryOptions       _opts;
        IByteStringBatchWriter             _writer;
        AnalyticsSinkMetrics               _metrics;
        IByteStringBatchWriter.BatchBuffer _currentBatchBuffer;
        DateTime?                          _autoflushAt;
        BigQueryFormatterV2                _formatterV2;

        AnalyticsDispatcherSinkBigQueryV2(IMetaLogger log, AnalyticsSinkBigQueryOptions opts, IByteStringBatchWriter writer, AnalyticsSinkMetrics metrics) : base(log)
        {
            _opts = opts;
            _writer = writer;
            _metrics = metrics;
            _currentBatchBuffer = null;
            _autoflushAt = null;

            if (!MetaplayServices.TryGet(out _formatterV2))
                throw new InvalidOperationException($"BigQueryFormatter must be initialized for AnalyticsDispatcherSinkBigQuery to work");
        }

        /// <summary>
        /// Create instance of the sink, if it is enabled in the options. Otherwise, return null.
        /// </summary>
        public static async Task<AnalyticsDispatcherSinkBigQueryV2> TryCreateAsync()
        {
            AnalyticsSinkBigQueryOptions opts = RuntimeOptionsRegistry.Instance.GetCurrent<AnalyticsSinkBigQueryOptions>();
            if (!opts.Enabled || !opts.UseV2)
                return null;

            IMetaLogger            log     = MetaLogger.ForContext<AnalyticsDispatcherSinkBigQueryV2>();
            AnalyticsSinkMetrics   metrics = AnalyticsSinkMetrics.ForSink("bigquery");
            IByteStringBatchWriter writer;

            if (opts.DebugPrintToStdoutOnly)
                writer = new BigQueryDebugLogBatchWriterV2(log);
            else
                writer = await BigQueryBatchWriterV2.CreateAsync(log, opts.BigQueryProjectId, opts.BigQueryCredentialsJson, opts.BigQueryDatasetId, opts.BigQueryTableId, opts.NumChunkBuffers, metrics);

            return new AnalyticsDispatcherSinkBigQueryV2(log, opts, writer, metrics);
        }

        public override async ValueTask DisposeAsync()
        {
            // Flush final buffer
            if (_currentBatchBuffer != null)
            {
                _log.Information("Final flush: {NumFinalEvents} events", _currentBatchBuffer.NumRows);
                FlushBatchBuffer();
            }

            // Cleanup writer
            await _writer.DisposeAsync();

            await base.DisposeAsync();
        }

        public override void EnqueueBatches(List<AnalyticsEventBatch> batches)
        {
            AnalyticsEventBatchHelper.EventEnumerator enumerator = AnalyticsEventBatchHelper.EnumerateBatches(batches).GetEnumerator();
            while (enumerator.MoveNext())
            {
                // We have something to write. Ensure we have a write buffer

                if (_currentBatchBuffer == null)
                {
                    _currentBatchBuffer = _writer.TryAllocateBatchBuffer();
                    if (_currentBatchBuffer != null)
                    {
                        // Successfully created new write buffer.
                        _autoflushAt = DateTime.UtcNow + _opts.MaxPendingDuration;
                    }
                    else
                    {
                        int numRemainingEvents = enumerator.NumRemainingEvents + 1; // +1 because this event was dropped too
                        int numRemainingBatches = enumerator.NumRemainingBatches + 1; // +1 because this batch was dropped too

                        _log.Warning("Unable to allocate write buffer, dropping {NumEvents} events in {NumBatches} batches", numRemainingEvents, numRemainingBatches);

                        _metrics.BatchesDropped.Inc(numRemainingBatches);
                        _metrics.EventsDropped.Inc(numRemainingEvents);
                        break;
                    }
                }

                // Write the events the buffer until the buffer becomes full or we run out of events.
                // \note: the inner do-while is redundant but is there to communicate the expected code flow.

                do
                {
                    _formatterV2.WriteEvent(_currentBatchBuffer, enumerator.Current, _opts.BigQueryEnableRowDeduplication);
                    if (_currentBatchBuffer.NumRows >= _opts.EventsPerChunk)
                    {
                        FlushBatchBuffer();
                        break;
                    }
                } while (enumerator.MoveNext());
            }

            if (_autoflushAt.HasValue && DateTime.UtcNow > _autoflushAt)
                FlushBatchBuffer();
        }

        void FlushBatchBuffer()
        {
            if (_currentBatchBuffer == null)
                throw new InvalidOperationException("no buffer to flush");

            _writer.SubmitBufferForWriting(_currentBatchBuffer);
            _currentBatchBuffer = null;
            _autoflushAt = null;
        }
    }
}
