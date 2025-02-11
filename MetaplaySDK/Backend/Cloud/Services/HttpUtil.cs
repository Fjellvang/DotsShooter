// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.IO;
using Metaplay.Core.Json;
using Metaplay.Core.Memory;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Services
{
    public static class HttpUtil
    {
        /// <summary>
        /// Globally shared JSON HTTP client. Stateless, does not do any caching or remember any cookies.
        /// </summary>
        public static readonly HttpClient SharedJsonClient = CreateJsonHttpClient();

        public static HttpClient CreateJsonHttpClient(AuthenticationHeaderValue authenticationHeader = null)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (authenticationHeader != null)
                httpClient.DefaultRequestHeaders.Authorization = authenticationHeader;
            return httpClient;
        }

        /// <summary>
        /// Performs HTTP GET to the <paramref name="requestUri"/>. Upon successful response code, returns the content as byte array.
        /// </summary>
        public static async Task<byte[]> RequestRawGetAsync(HttpClient client, string requestUri, AuthenticationHeaderValue authHeader = null, CancellationToken ct = default)
        {
            // Configure the request
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (authHeader != null)
                request.Headers.Authorization = authHeader;

            // Make the request
            using HttpResponseMessage response = await client.SendAsync(request, ct).ConfigureAwait(false);
            byte[] responsePayload = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

            // Throw on error
            response.EnsureSuccessStatusCode();

            return responsePayload;
        }

        /// <summary>
        /// Performs HTTP GET to the <paramref name="requestUri"/>. Upon successful response code, decodes the response JSON into <typeparamref name="T"/>
        /// and returns it.
        /// </summary>
        public static async Task<T> RequestJsonGetAsync<T>(HttpClient client, string requestUri, AuthenticationHeaderValue authHeader = null, CancellationToken ct = default)
        {
            // Configure the request
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (authHeader != null)
                request.Headers.Authorization = authHeader;

            // Make the request
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            return await ParseJsonResponseAsync<T>(response).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs HTTP POST to the <paramref name="requestUrl"/> with JSON payload of <paramref name="payload"/>. Upon successful response code, decodes
        /// the response JSON into <typeparamref name="T"/> and returns it.
        /// </summary>
        public static async Task<T> RequestJsonPostAsync<T>(HttpClient client, string requestUrl, string payload, AuthenticationHeaderValue authHeader = null, CancellationToken ct = default)
        {
            return await RequestWithMethodAsync<T>(client, requestUrl, HttpMethod.Post, payload, authHeader, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs HTTP PUT to the <paramref name="requestUrl"/> with JSON payload of <paramref name="payload"/>. Upon successful response code, decodes
        /// the response JSON into <typeparamref name="T"/> and returns it.
        /// </summary>
        public static async Task<T> RequestJsonPutAsync<T>(HttpClient client, string requestUrl, string payload, AuthenticationHeaderValue authHeader = null, CancellationToken ct = default)
        {
            return await RequestWithMethodAsync<T>(client, requestUrl, HttpMethod.Put, payload, authHeader, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs HTTP POST to the <paramref name="requestUrl"/> with JSON payload of <paramref name="payload"/>. Upon successful response code, decodes
        /// the response JSON into <typeparamref name="TResult"/> and returns it.
        /// </summary>
        public static Task<TResult> RequestJsonPostAsync<TRequest, TResult>(HttpClient client, string requestUrl, TRequest payload, AuthenticationHeaderValue authHeader = null, CancellationToken ct = default)
        {
            string jsonPayload = JsonSerialization.SerializeToString(payload);
            return RequestJsonPostAsync<TResult>(client, requestUrl, jsonPayload, authHeader, ct);
        }

        /// <summary>
        /// Performs HTTP PUT to the <paramref name="requestUrl"/> with JSON payload of <paramref name="payload"/>. Upon successful response code, decodes
        /// the response JSON into <typeparamref name="TResult"/> and returns it.
        /// </summary>
        public static Task<TResult> RequestJsonPutAsync<TRequest, TResult>(HttpClient client, string requestUrl, TRequest payload, AuthenticationHeaderValue authHeader = null, CancellationToken ct = default)
        {
            string jsonPayload = JsonSerialization.SerializeToString(payload);
            return RequestJsonPutAsync<TResult>(client, requestUrl, jsonPayload, authHeader, ct);
        }

        public static async Task<T> RequestDeleteAsync<T>(HttpClient client, string requestUrl, AuthenticationHeaderValue authHeader = null, CancellationToken ct = default)
        {
            // Configure the request
            using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Delete, requestUrl);
            if (authHeader != null)
                httpRequest.Headers.Authorization = authHeader;

            return await RequestAsync<T>(client, httpRequest, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the given HTTP Request. Upon successful response code, decodes
        /// the response JSON into <typeparamref name="T"/> and returns it.
        /// </summary>
        public static async Task<T> RequestAsync<T>(HttpClient client, HttpRequestMessage httpRequest, CancellationToken ct = default)
        {
            using HttpResponseMessage response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            return await ParseJsonResponseAsync<T>(response, ct).ConfigureAwait(false);
        }

        // \todo docs
        static async Task<T> RequestWithMethodAsync<T>(HttpClient client, string requestUrl, HttpMethod method, string payload, AuthenticationHeaderValue authHeader = null, CancellationToken ct = default)
        {
            // Configure the payload
            using HttpContent httpContent = new StringContent(payload);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // Configure the request
            using HttpRequestMessage httpRequest = new HttpRequestMessage(method, requestUrl);
            if (authHeader != null)
                httpRequest.Headers.Authorization = authHeader;
            httpRequest.Content = httpContent;

            // Make the request
            return await RequestAsync<T>(client, httpRequest, ct).ConfigureAwait(false);
        }

        static async Task<T> ParseJsonResponseAsync<T>(HttpResponseMessage response, CancellationToken ct = default)
        {
            // Read to temporarily allocated pooled buffers. Each segment is 16kB. The goal is to avoid
            // generating garbage, and keep the rented pool buffers of sane size. If this is not done, a
            // large query would allocate a large temporary buffer, and large allocations go directly into
            // Large Object Heap. LOH garbage gets then cleaned only at Gen2 GC.
            //
            // Note that we need this manual reading since the ReadAsStreamAsync doesn't actually guarantee
            // a "live" stream to the connection and is allowed to read all into a temporary buffer and then
            // create internally a MemoryStream. This memory stream could use a single contiguous byte array,
            // which then causes LOH garbage. However, while returning this "live" is not guaranteed, it's not
            // forbidden either and in some test ReadAsStreamAsync does successfully return a real stream.
            // But since this behavior is not guaranteed, we cannot rely on it.
            //
            // But even if the ReadAsStreamAsync does return a live stream, it is not sufficient. The Newtonsoft
            // JsonSerializer does not support async parsing, so the Deserialize would be sync-over-async
            // and that might cause Thread Pool stuttering. By buffering the stream manually, we can work around
            // both of these issues.
            using SegmentedIOBuffer buffer = new SegmentedIOBuffer(segmentSize: 16384);
            using (Stream writer = new IOWriter(buffer).ConvertToStream())
            {
                await response.Content.CopyToAsync(writer, ct).ConfigureAwait(false);
            }

            using Stream reader = new IOReader(buffer).ConvertToStream();
            using StreamReader textReader = new StreamReader(reader);

            // Throw on error. The content is most likely an error message so include it in the exception. Some
            // servers reply very large error messages, for example by echoing the request back, so truncate any
            // messages longer than 60 kB.
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = ReadString(textReader, maxNumChars: 30_000); // 60KB
                string errorMessage;
                bool readWholeContent = textReader.EndOfStream;
                if (errorContent == "")
                    errorMessage = $"Server replied with a non-success status code {response.StatusCode}";
                else if (readWholeContent)
                    errorMessage = $"Server replied with a non-success status code {response.StatusCode}: {errorContent}";
                else
                    errorMessage = $"Server replied with a non-success status code {response.StatusCode} (truncated): {errorContent}";

                throw new HttpRequestException(errorMessage, inner: null, statusCode: response.StatusCode);
            }

            using JsonTextReader jsonReader = new JsonTextReader(textReader);

            JsonSerializer jsonSerializer = JsonSerializer.CreateDefault();
            return jsonSerializer.Deserialize<T>(jsonReader);
        }

        static string ReadString(StreamReader textReader, int maxNumChars)
        {
            using IMemoryOwner<char> tempBufferAlloc = MemoryPool<char>.Shared.Rent(maxNumChars);
            Span<char> tempBufferSpan = tempBufferAlloc.Memory.Span;
            int numChars = textReader.Read(tempBufferSpan);
            return new string(tempBufferSpan.Slice(0, numChars));
        }
    }
}
