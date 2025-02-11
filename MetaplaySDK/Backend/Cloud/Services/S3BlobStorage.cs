// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Services
{
    public class S3BlobStorage : IBlobStorage
    {
        IAmazonS3       _client;
        string          _bucketName;
        string          _basePath;
        S3CannedACL     _cannedACL;

        public IAmazonS3    Client => _client;
        public string       BucketName => _bucketName;

        // \todo [petri] deprecate and remove explicit accessKey and secretKey -- assume always using external credentials (like IRSA)
        public S3BlobStorage(string accessKey, string secretKey, string regionName, string bucketName, string basePath, string cannedACL = null)
        {
            RegionEndpoint region = RegionEndpoint.GetBySystemName(regionName);
            bool explicitAccessKeys = !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey);
            _client     = explicitAccessKeys ? new AmazonS3Client(accessKey, secretKey, region) : new AmazonS3Client(region);
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            _basePath   = basePath ?? throw new ArgumentNullException(nameof(basePath));
            if (cannedACL != null)
            {
                if (cannedACL != "BucketOwnerFullControl")
                    throw new ArgumentException($"Invalid value for cannedACL: {cannedACL}, only 'BucketOwnerFullControl' is supported");
                _cannedACL = S3CannedACL.BucketOwnerFullControl;
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        // Convert backslashes (directory separator on windows hosts) to forward slashes for S3
        public string GetEntryKeyName(string fileName) => Path.Join(_basePath, fileName).Replace('\\', '/');

        #region IBlobRepository

        public async Task<byte[]> GetAsync(string fileName)
        {
            (Stream contents, HeadersCollection _) = await ReadObjectAsync(GetEntryKeyName(fileName)).ConfigureAwait(false);
            if (contents == null)
                return null;
            using (Stream responseStream = contents)
            using (MemoryStream memStream = new MemoryStream())
            {
                await responseStream.CopyToAsync(memStream).ConfigureAwait(false);
                return memStream.ToArray();
            }
        }

        public async Task<(Stream, Dictionary<string, string>)> GetContentAndHeadersAsync(string fileName)
        {
            (Stream contents, HeadersCollection headers) = await ReadObjectAsync(GetEntryKeyName(fileName)).ConfigureAwait(false);
            return (contents, headers?.Keys.ToDictionary(x => x, x => headers[x]));
        }

        public async Task PutAsync(string fileName, byte[] bytes, BlobStoragePutHints hintsMaybe = null)
        {
            string contentType = hintsMaybe?.ContentType ?? "binary/octet-stream";
            try
            {
                await WriteObjectAsync(GetEntryKeyName(fileName), bytes, contentType).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Let's inject fileName into the exception for easier debuggability.
                // \note: Use IOException to be consistent with Disk backend.
                throw new IOException($"S3 write to {fileName} failed", ex);
            }
        }

        public Task DeleteAsync(string fileName)
        {
            throw new System.NotImplementedException();
        }

        #endregion // IBlobRepository

        async Task<(Stream Content, HeadersCollection Headers)> ReadObjectAsync(string keyName)
        {
            GetObjectRequest request = new GetObjectRequest
            {
                BucketName  = _bucketName,
                Key         = keyName
            };

            try
            {
                GetObjectResponse response = await _client.GetObjectAsync(request).ConfigureAwait(false);
                // Return null if object doesn't exist
                if (response.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                    return (null, null);
                return (response.ResponseStream, response.Headers);
            }
            catch (AmazonS3Exception ex)
            {
                // Handle NoSuchKey
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return (null, null);

                DebugLog.Warning("Failed to GetObject key {KeyName} from bucket {BucketName}: {Exception}", keyName, _bucketName, ex);
                throw;
            }
        }

        async Task WriteObjectAsync(string keyName, byte[] bytes, string contentType)
        {
            using (MemoryStream memStream = new MemoryStream(bytes))
            {
                PutObjectRequest putRequest = new PutObjectRequest
                {
                    BucketName  = _bucketName,
                    Key         = keyName,
                    ContentType = contentType,
                    InputStream = memStream,
                    CannedACL   = _cannedACL
                };
                /*PutObjectResponse response =*/ await _client.PutObjectAsync(putRequest).ConfigureAwait(false);
            }
        }

        public string GetPresignedUrl(string filePath)
        {
            return GetPresignedUrl(HttpVerb.GET, filePath, TimeSpan.FromDays(7), contentLength: null);
        }

        public string GetPresignedUrl(HttpVerb verb, string filePath, TimeSpan expiresIn, int? contentLength)
        {
            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                Verb        = verb,
                BucketName  = _bucketName,
                Key         = GetEntryKeyName(filePath),
                Protocol    = Protocol.HTTPS,
                Expires     = DateTime.UtcNow + expiresIn,
            };

            if (contentLength.HasValue)
                request.Headers.ContentLength = contentLength.Value;

            string preSignedUrl = _client.GetPreSignedURL(request);
            return preSignedUrl;
        }
    }
}
