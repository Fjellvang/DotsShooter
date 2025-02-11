// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Google.Api.Gax.Grpc;
using Google.Apis.Bigquery.v2.Data;
using Google.Cloud.BigQuery.V2;
using Google.Cloud.BigQuery.Storage.V1;
using Google.Protobuf;
using Grpc.Core;
using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Cloud.Analytics
{
    public interface IByteStringBatchWriter
    {
        /// <summary>
        /// Represents a batch buffer that can be used to write rows into BigQuery table.
        /// </summary>
        public class BatchBuffer
        {
            public List<ByteString> Rows    = new List<ByteString>();
            public int              NumRows => Rows.Count;

            public void Add(ByteString row)
            {
                Rows.Add(row);
            }
        }

        public ValueTask DisposeAsync();

        /// <summary>
        /// Allocates new batch buffer. If the number of open (unsubmitted or submit has not yet finished) buffers
        /// exceeds the limit, returns null.
        /// </summary>
        public BatchBuffer TryAllocateBatchBuffer();

        /// <summary>
        /// Submits a batch for writing in background. Caller must not modify the buffer after this call.
        /// </summary>
        public void SubmitBufferForWriting(BatchBuffer buffer);
    }

    /// <summary>
    /// Helper to write rows into BigQuery table as batches in the background. BigQueryBatchWriter handles
    /// normal error conditions and retries as necessary.
    /// BigQueryBatchWriter does not buffer data boundlessy, instead BigQueryBatchWriter manages a limited
    /// set of <see cref="IByteStringBatchWriter.BatchBuffer"/>s. Caller should allocate a batch buffer from
    /// the writer, write rows to be added there and then submit the buffer back to the writer. If no free
    /// batch buffers are available, the allocation fails and caller may not proceed. Written batch buffers
    /// are available for allocation again after the write into BigQuery table has completed in the background.
    /// </summary>
    public class BigQueryBatchWriterV2(
        IMetaLogger log,
        BigQueryWriteClient writeClient,
        TableName tableName,
        AnalyticsSinkMetrics metrics,
        int maxNumOpenBatches)
        : IByteStringBatchWriter
    {
        readonly IMetaLogger          _log            = log ?? throw new ArgumentNullException(nameof(log));
        readonly BigQueryWriteClient  _writeClient    = writeClient ?? throw new ArgumentNullException(nameof(writeClient));
        readonly TableName            _tableName      = tableName ?? throw new ArgumentNullException(nameof(tableName));
        readonly ProtoSchema          _protoSchema    = new ProtoSchema {ProtoDescriptor = BigQuery.Event.Descriptor.ToProto()};
        readonly AnalyticsSinkMetrics _metrics        = metrics ?? throw new ArgumentNullException(nameof(metrics));
        readonly object               _lock           = new object();
        readonly List<Task>           _flushTasks     = new List<Task>();
        int                           _numOpenBatches = 0;

        public static async Task<BigQueryBatchWriterV2> CreateAsync(
            IMetaLogger log,
            string projectId,
            string credentialsJson,
            string datasetId,
            string tableId,
            int maxNumOpenBatches,
            AnalyticsSinkMetrics metrics)
        {
            // Create a BigQueryClient for interacting with BigQuery API.
            BigQueryClientBuilder builder = new BigQueryClientBuilder();
            builder.ProjectId       = projectId;
            builder.JsonCredentials = credentialsJson;
            BigQueryClient client         = await builder.BuildAsync();

            TableReference tableReference = client.GetTableReference(projectId, datasetId, tableId);
            // Test the table exists
            _ = await client.GetTableAsync(tableReference);

            // Create a BigQueryWriteClient for writing data to BigQuery Storage API.
            BigQueryWriteClientBuilder writeBuilder = new BigQueryWriteClientBuilder();
            writeBuilder.JsonCredentials            = credentialsJson;
            BigQueryWriteClient writeClient         = await writeBuilder.BuildAsync();

            TableName           tableName           = TableName.FromProjectDatasetTable(projectId, datasetId, tableId);

            return new BigQueryBatchWriterV2(log, writeClient, tableName, metrics, maxNumOpenBatches);
        }

        public async ValueTask DisposeAsync()
        {
            Task[] ongoingFlushes;
            lock (_lock)
            {
                ongoingFlushes = _flushTasks.ToArray();
            }

            await Task.WhenAll(ongoingFlushes).ConfigureAwait(false);
        }

        public IByteStringBatchWriter.BatchBuffer TryAllocateBatchBuffer()
        {
            lock (_lock)
            {
                if (_numOpenBatches >= maxNumOpenBatches)
                    return null;

                _numOpenBatches++;
            }

            return new IByteStringBatchWriter.BatchBuffer();
        }

        public void SubmitBufferForWriting(IByteStringBatchWriter.BatchBuffer buffer)
        {
            Task flushTask = Task.Run(async () => await ExecuteFlushAsync(buffer));
            lock (_lock)
            {
                _flushTasks.Add(flushTask);
                _ = flushTask.ContinueWith(
                    task =>
                    {
                        lock (_lock)
                        {
                            _flushTasks.Remove(flushTask);
                            _numOpenBatches--;
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        private static string FormatSize(long sizeInBytes)
        {
            string[] sizeUnits = ["B", "KB", "MB", "GB", "TB"];
            double   size      = sizeInBytes;
            int      unitIndex = 0;

            while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return Invariant($"{size:0.##} {sizeUnits[unitIndex]}");
        }

        async Task ExecuteFlushAsync(IByteStringBatchWriter.BatchBuffer buffer)
        {
            if (buffer.NumRows == 0)
                return;

            const int maxRetries = 3;
            int       retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    Stopwatch sw = Stopwatch.StartNew();

                    // Initialize streaming call, retrieving the stream object
                    BigQueryWriteClient.AppendRowsStream                                  appendRowsStream = _writeClient.AppendRows();
                    using AsyncDuplexStreamingCall<AppendRowsRequest, AppendRowsResponse> grpcCall         = appendRowsStream.GrpcCall;

                    // Use the default stream
                    WriteStreamName streamName = new WriteStreamName(_tableName.ProjectId, _tableName.DatasetId, _tableName.TableId, "_default");

                    // Create a batch of row data by appending serialized bytes to the
                    // SerializedRows repeated field.
                    AppendRowsRequest.Types.ProtoData protoData = new AppendRowsRequest.Types.ProtoData
                    {
                        WriterSchema = _protoSchema,
                        Rows         = new ProtoRows {SerializedRows    = {buffer.Rows}},
                    };

                    // The size of a single AppendRowsRequest must be less than 10 MB in size.
                    // Let's use 8MB as the limit for the batch size for safety.
                    int sizeLimit = 8 * 1024 * 1024;
                    // sizeLimit = 10 * 1024; // For testing purposes, limit the size to 10 KB.
                    int sizeInBytes = protoData.CalculateSize();

                    // Split the batch into smaller batches if the size exceeds the limit.
                    if (sizeInBytes > sizeLimit)
                    {
                        int numBatches = (int)Math.Ceiling((double)sizeInBytes / sizeLimit);
                        int batchSize  = (int)Math.Ceiling((double)buffer.NumRows / numBatches);
                        for (int i = 0; i < numBatches; i++)
                        {
                            int start = i * batchSize;
                            int end   = Math.Min((i + 1) * batchSize, buffer.NumRows);
                            AppendRowsRequest.Types.ProtoData protoDataBatch = new AppendRowsRequest.Types.ProtoData
                            {
                                WriterSchema = _protoSchema,
                                Rows         = new ProtoRows {SerializedRows = {buffer.Rows.GetRange(start, end - start)}},
                            };

                            _log.Debug(
                                "Writing batch ({BatchSize} out of total {SizeInBytes}) {BatchIndex} of {NumBatches} to BigQuery table ({DatasetId}, {TableId})",
                                FormatSize(protoDataBatch.CalculateSize()),
                                FormatSize(sizeInBytes),
                                i + 1,
                                numBatches,
                                _tableName.DatasetId,
                                _tableName.TableId);

                            await appendRowsStream.WriteAsync(
                                new AppendRowsRequest
                                {
                                    WriteStreamAsWriteStreamName = streamName,
                                    ProtoRows                    = protoDataBatch,
                                });
                        }
                    }
                    else
                    {
                        // Stream a request to the server.
                        _log.Debug("Writing batch ({SizeInBytes}) of {RowCount} rows to BigQuery table ({DatasetId}, {TableId})", FormatSize(sizeInBytes), buffer.NumRows, _tableName.DatasetId, _tableName.TableId);

                        await appendRowsStream.WriteAsync(
                            new AppendRowsRequest
                            {
                                WriteStreamAsWriteStreamName = streamName,
                                ProtoRows                    = protoData,
                            });
                    }

                    // Complete writing requests to the stream.
                    await appendRowsStream.WriteCompleteAsync();

                    // Sending requests and retrieving responses can be arbitrarily interleaved.
                    // Exact sequence will depend on client/server behavior.
                    // This will complete once all server responses have been processed.
                    await using (AsyncResponseStream<AppendRowsResponse> appenderResponseStream = appendRowsStream.GetResponseStream())
                    {
                        await foreach (AppendRowsResponse responseItem in appenderResponseStream)
                        {
                            if (responseItem.RowErrors?.Count > 0)
                            {
                                // Metrics. Report the whole batch as failed, but report events as precisely as possible.
                                _metrics.EventsDropped.Inc(buffer.NumRows);
                                _metrics.ChunksDropped.Inc();

                                // Log the error rows
                                Dictionary<string, string> errorRows = new Dictionary<string, string>();
                                foreach (RowError rowError in responseItem.RowErrors)
                                {
                                    errorRows.Add(BigQuery.Event.Parser.ParseFrom(buffer.Rows[(int)rowError.Index]).ToString(), rowError.ToString());
                                }

                                _log.Error("Error appending rows. Row errors: {ErrorRows}", errorRows);
                            }

                            // Success patch
                            _metrics.EventsFlushed.Inc(buffer.NumRows);
                            _metrics.ChunksFlushed.Inc();
                            _metrics.BatchWriteDuration.Observe(sw.Elapsed.TotalSeconds);
                        }
                    }

                    // If successful, break out of the retry loop
                    break;
                }
                catch (RpcException grpcEx)
                {
                    StatusCode grpcStatusCode = grpcEx.Status.StatusCode;

                    // Handle specific errors
                    if (grpcStatusCode is
                        StatusCode.Unavailable or
                        StatusCode.DeadlineExceeded or
                        StatusCode.Internal)
                    {
                        // Increment retry count and wait before retrying
                        retryCount++;
                        TimeSpan retryTime = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff

                        // Log the error
                        _log.Error(
                            "Writing into BigQuery table ({DatasetId}, {TableId}) failed with api error: {StatusCode} {Error}\nWill retry in {RetryTime}s ...",
                            _tableName.DatasetId,
                            _tableName.TableId,
                            grpcStatusCode,
                            grpcEx,
                            retryTime);

                        await Task.Delay(retryTime);
                    }
                    else
                    {
                        // Log the error
                        _log.Error(
                            "Writing into BigQuery table ({DatasetId}, {TableId}) failed with api error: {StatusCode} {Error}",
                            _tableName.DatasetId,
                            _tableName.TableId,
                            grpcStatusCode,
                            grpcEx);

                        // For permanent errors, drop the batch and exit
                        _metrics.EventsDropped.Inc(buffer.NumRows);
                        _metrics.ChunksDropped.Inc();
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("Writing into BigQuery table ({DatasetId}, {TableId}) failed with unknown error: {Error}", _tableName.DatasetId, _tableName.TableId, ex);
                }
            }
        }
    }

    /// <summary>
    /// A <see cref="BigQueryBatchWriterV2"/> that instead of writing the rows into big query, writes
    /// the inserted rows to console. For Debugging.
    /// </summary>
    public class BigQueryDebugLogBatchWriterV2(IMetaLogger log) : IByteStringBatchWriter
    {
        readonly IMetaLogger _log = log ?? throw new ArgumentNullException(nameof(log));

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public IByteStringBatchWriter.BatchBuffer TryAllocateBatchBuffer()
        {
            return new IByteStringBatchWriter.BatchBuffer();
        }

        void IByteStringBatchWriter.SubmitBufferForWriting(IByteStringBatchWriter.BatchBuffer buffer)
        {
            foreach (ByteString row in buffer.Rows)
            {
                try
                {
                    BigQuery.Event message      = BigQuery.Event.Parser.ParseFrom(row);
                    string         readableData = message.ToString();
                    _log.Debug("Dry-run write row into BigQuery table: {RowData}", PrettyPrint.Verbose(readableData));
                }
                catch (Exception ex)
                {
                    _log.Error("Failed to parse row data: {Error}", ex);
                }
            }
        }
    }
}
