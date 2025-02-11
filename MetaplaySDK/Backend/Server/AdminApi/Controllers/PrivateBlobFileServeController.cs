// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Services;
using Metaplay.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi.Controllers
{
    [Route("file")]
    public class PrivateBlobFileServeController : MetaplayAdminApiController
    {
        static readonly Lazy<IBlobStorage> _storage = new Lazy<IBlobStorage>(InitStorage);

        static IBlobStorage InitStorage()
        {
            BlobStorageOptions opts = RuntimeOptionsRegistry.Instance.GetCurrent<BlobStorageOptions>();
            if (string.IsNullOrEmpty(opts.ExposedPrivatePathPrefix))
                return null;
            return RuntimeOptionsRegistry.Instance.GetCurrent<BlobStorageOptions>().CreatePrivateBlobStorage(opts.ExposedPrivatePathPrefix);
        }

        [HttpGet("{*filePath}")]
        [RequirePermission(MetaplayPermissions.PrivateBlobFileServeRead)]
        public async Task<IActionResult> Get(string filePath)
        {
            if (_storage.Value == null)
                return NotFound();

            filePath = Path.GetRelativePath(".", filePath);
            if (filePath.StartsWith("..", StringComparison.Ordinal))
                return NotFound();
            filePath = filePath.Replace('\\', '/');
            string       contentType = "application/octet-stream";
            IBlobStorage blobStorage = _storage.Value;

            _logger.LogDebug("Forwarding file from private blob storage: {Path}", filePath);

            if (blobStorage is S3BlobStorage s3BlobStorage)
            {
                (Stream contentStream, Dictionary<string, string> headers) = await s3BlobStorage.GetContentAndHeadersAsync(filePath);
                if (contentStream == null)
                    return NotFound();
                if (headers.TryGetValue("Content-Type", out string contentTypeFromHeader))
                    contentType = contentTypeFromHeader;
                foreach (KeyValuePair<string, string> header in headers)
                    Response.Headers[header.Key] = header.Value;
                return new FileStreamResult(contentStream, contentType);
            }

            if (new FileExtensionContentTypeProvider().TryGetContentType(filePath, out string contentTypeFromExtension))
                contentType = contentTypeFromExtension;
            byte[] content = await blobStorage.GetAsync(filePath);
            if (content == null)
                return NotFound();
            return new FileContentResult(content, contentType);
        }
    }
}

