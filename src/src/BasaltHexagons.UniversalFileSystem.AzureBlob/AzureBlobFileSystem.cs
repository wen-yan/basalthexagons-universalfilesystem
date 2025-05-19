using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Configuration;
using BasaltHexagons.UniversalFileSystem.Core.Disposing;
using BasaltHexagons.UniversalFileSystem.Core.Exceptions;
using BasaltHexagons.UniversalFileSystem.Core.IO;
using Microsoft.Extensions.Configuration;

namespace BasaltHexagons.UniversalFileSystem.AzureBlob;

[AsyncMethodBuilder(typeof(ContinueOnAnyAsyncMethodBuilder))]
public class AzureBlobFileSystem : AsyncDisposable, IFileSystem
{
    public AzureBlobFileSystem(BlobServiceClient client, IConfiguration settings)
    {
        this.Client = client;
        this.Settings = settings;
    }

    private BlobServiceClient Client { get; }
    private IConfiguration Settings { get; }

    #region IFileSystem

    public async Task CopyFileAsync(Uri sourceUri, Uri destUri, bool overwrite, CancellationToken cancellationToken)
    {
        if (sourceUri == destUri)
            throw new ArgumentException("Can't copy file to itself.");

        if (!await this.DoesFileExistAsync(sourceUri, cancellationToken))
            throw new FileNotExistsException(sourceUri);

        BlobClient sourceBlobClient = this.GetBlobClient(sourceUri);
        BlobClient destBlobClient = this.GetBlobClient(destUri);

        if (!overwrite && await this.DoesFileExistAsync(destUri, cancellationToken))
            throw new FileExistsException(destUri);

        await this.TryCreateBlobContainerIfNotExistsAsync(destUri, cancellationToken);

        BlobLeaseClient sourceBlobLeaseClient = new(sourceBlobClient);
        try
        {
            await sourceBlobLeaseClient.AcquireAsync(BlobLeaseClient.InfiniteLeaseDuration, cancellationToken: cancellationToken);
            CopyFromUriOperation operation = await destBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri, null, cancellationToken);
            Response response = await operation.WaitForCompletionResponseAsync(cancellationToken);
        }
        finally
        {
            await sourceBlobLeaseClient.ReleaseAsync(); // Don't use cancellation, want to it complete
        }
    }

    public async Task<bool> DeleteFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        BlobClient blobClient = this.GetBlobClient(uri);
        return await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<Stream> GetFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!await this.DoesFileExistAsync(uri, cancellationToken))
            throw new FileNotExistsException(uri);

        BlobClient blobClient = this.GetBlobClient(uri);
        Response<BlobDownloadStreamingResult> response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        if (!response.HasValue || response.Value.Content == null)
            throw new FileNotExistsException(uri);

        return new LinkedDisposingStream(
            new PositionSupportedStream(response.Value.Content, 0, response.Value.Details.ContentLength),
            [], [response.Value]);
    }

    public async Task<ObjectMetadata> GetFileMetadataAsync(Uri uri, CancellationToken cancellationToken)
    {
        BlobClient blobClient = this.GetBlobClient(uri);
        if (!await this.DoesFileExistAsync(uri, cancellationToken))
            throw new FileNotExistsException(uri);

        BlobProperties properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        return new(uri, ObjectType.File, properties.ContentLength, properties.LastModified.UtcDateTime);
    }

    public async IAsyncEnumerable<ObjectMetadata> ListObjectsAsync(Uri prefix, bool recursive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        BlobContainerClient containerClient = this.GetContainerClient(prefix);

        if (!await containerClient.ExistsAsync(cancellationToken)) yield break;

        (string container, string prefixKey) = this.DeconstructUri(prefix);

        Queue<string> keyPrefixQueue = new();
        keyPrefixQueue.Enqueue(prefixKey);

        while (keyPrefixQueue.Count > 0)
        {
            string key = keyPrefixQueue.Dequeue();
            await foreach (BlobHierarchyItem blobHierarchyItem in containerClient.GetBlobsByHierarchyAsync(
                               prefix: key, delimiter: "/", cancellationToken: cancellationToken))
            {
                if (blobHierarchyItem.IsPrefix)
                {
                    Uri uri = ConstructUri(prefix.Scheme, container, blobHierarchyItem.Prefix);
                    yield return new ObjectMetadata(uri, ObjectType.Prefix, null, null);

                    if (recursive)
                        keyPrefixQueue.Enqueue(blobHierarchyItem.Prefix);
                }
                else
                {
                    Uri uri = ConstructUri(prefix.Scheme, container, blobHierarchyItem.Blob.Name);
                    yield return new ObjectMetadata(uri, ObjectType.File,
                        blobHierarchyItem.Blob.Properties.ContentLength,
                        blobHierarchyItem.Blob.Properties.LastModified?.UtcDateTime);
                }
            }
        }
    }

    public async Task MoveFileAsync(Uri oldUri, Uri newUri, bool overwrite, CancellationToken cancellationToken)
    {
        await this.CopyFileAsync(oldUri, newUri, overwrite, cancellationToken);
        await this.DeleteFileAsync(oldUri, cancellationToken);
    }

    public async Task PutFileAsync(Uri uri, Stream stream, bool overwrite, CancellationToken cancellationToken)
    {
        if (!overwrite && await this.DoesFileExistAsync(uri, cancellationToken))
            throw new FileExistsException(uri);

        await this.TryCreateBlobContainerIfNotExistsAsync(uri, cancellationToken);
        BlobClient blobClient = this.GetBlobClient(uri);
        await blobClient.UploadAsync(stream, overwrite, cancellationToken);
    }

    public async Task<bool> DoesFileExistAsync(Uri uri, CancellationToken cancellationToken)
    {
        BlobClient blobClient = this.GetBlobClient(uri);
        Response<bool> response = await blobClient.ExistsAsync(cancellationToken);
        return response.HasValue && response.Value;
    }

    #endregion

    private static Uri ConstructUri(string scheme, string container, string key)
    {
        UriBuilder builder = new()
        {
            Scheme = scheme,
            Host = container,
            Path = key,
        };
        return builder.Uri;
    }

    private (string Container, string Key) DeconstructUri(Uri uri)
    {
        string bucket = uri.Host;
        string key = uri.AbsolutePath.TrimStart('/');
        return (bucket, key);
    }

    private BlobContainerClient GetContainerClient(Uri uri) => this.Client.GetBlobContainerClient(uri.Host);

    private BlobClient GetBlobClient(Uri uri)
    {
        BlobContainerClient containerClient = this.GetContainerClient(uri);
        return containerClient.GetBlobClient(uri.AbsolutePath);
    }

    private async Task TryCreateBlobContainerIfNotExistsAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (this.Settings.GetBoolValue("CreateBlobContainerIfNotExists", () => null) ?? false)
        {
            BlobContainerClient containerClient = this.GetContainerClient(uri);
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                await this.Client.CreateBlobContainerAsync(uri.Host, cancellationToken: cancellationToken);
            }
        }
    }
}