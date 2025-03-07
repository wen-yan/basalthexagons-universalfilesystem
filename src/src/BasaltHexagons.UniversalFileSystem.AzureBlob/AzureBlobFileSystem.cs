﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Configuration;
using BasaltHexagons.UniversalFileSystem.Core.Disposing;
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

    public async Task CopyObjectAsync(Uri sourcePath, Uri destPath, bool overwriteIfExists,
        CancellationToken cancellationToken)
    {
        BlobClient sourceBlobClient = this.GetBlobClient(sourcePath);
        BlobClient destBlobClient = this.GetBlobClient(destPath);

        if (!overwriteIfExists && await destBlobClient.ExistsAsync(cancellationToken))
        {
            throw new ArgumentException($"Object {destPath} already exists.");
        }

        await this.TryCreateBlobContainerIfNotExistsAsync(destPath, cancellationToken);

        BlobLeaseClient sourceBlobLeaseClient = new(sourceBlobClient);
        try
        {
            await sourceBlobLeaseClient.AcquireAsync(BlobLeaseClient.InfiniteLeaseDuration, cancellationToken: cancellationToken);
            CopyFromUriOperation operation = await destBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri, null, cancellationToken);
            Response response = await operation.WaitForCompletionResponseAsync(cancellationToken);
        }
        finally
        {
            await sourceBlobLeaseClient.ReleaseAsync();
        }
    }

    public async Task<bool> DeleteObjectAsync(Uri path, CancellationToken cancellationToken)
    {
        BlobClient blobClient = this.GetBlobClient(path);
        return await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<Stream> GetObjectAsync(Uri path, CancellationToken cancellationToken)
    {
        // TODO: close BlobDownloadStreamingResult too
        BlobClient blobClient = this.GetBlobClient(path);
        Response<BlobDownloadStreamingResult> response =
            await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        if (!response.HasValue || response.Value.Content == null)
            throw new ArgumentException($"Object {path} not found.");

        return response.Value.Content;
    }

    public async Task<ObjectMetadata?> GetObjectMetadataAsync(Uri path, CancellationToken cancellationToken)
    {
        BlobClient blobClient = this.GetBlobClient(path);
        Response<bool> existsResponse = await blobClient.ExistsAsync(cancellationToken);
        if (!existsResponse.HasValue || !existsResponse.Value)
            return null;

        BlobProperties properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        return new(path, ObjectType.File, properties.ContentLength, properties.LastModified.UtcDateTime);
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
            await foreach (BlobHierarchyItem blobHierarchyItem in containerClient.GetBlobsByHierarchyAsync(prefix: key,
                               delimiter: "/", cancellationToken: cancellationToken))
            {
                if (blobHierarchyItem.IsPrefix)
                {
                    Uri path = ConstructUri(prefix.Scheme, container, blobHierarchyItem.Prefix);
                    yield return new ObjectMetadata(path, ObjectType.Prefix, null, null);

                    if (recursive)
                        keyPrefixQueue.Enqueue(blobHierarchyItem.Prefix);
                }
                else
                {
                    Uri path = ConstructUri(prefix.Scheme, container, blobHierarchyItem.Blob.Name);
                    yield return new ObjectMetadata(path, ObjectType.File,
                        blobHierarchyItem.Blob.Properties.ContentLength,
                        blobHierarchyItem.Blob.Properties.LastModified?.UtcDateTime);
                }
            }
        }
    }

    public async Task MoveObjectAsync(Uri oldPath, Uri newPath, bool overwriteIfExists, CancellationToken cancellationToken)
    {
        await this.CopyObjectAsync(oldPath, newPath, overwriteIfExists, cancellationToken);
        await this.DeleteObjectAsync(oldPath, cancellationToken);
    }

    public async Task PutObjectAsync(Uri path, Stream stream, bool overwriteIfExists,
        CancellationToken cancellationToken)
    {
        if (!overwriteIfExists)
        {
            ObjectMetadata? existingObject = await this.GetObjectMetadataAsync(path, cancellationToken);
            if (existingObject != null)
                throw new ArgumentException($"Object {path} already exists.");
        }

        await this.TryCreateBlobContainerIfNotExistsAsync(path, cancellationToken);
        BlobClient blobClient = this.GetBlobClient(path);
        await blobClient.UploadAsync(stream, overwriteIfExists, cancellationToken);
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

    private async Task TryCreateBlobContainerIfNotExistsAsync(Uri path, CancellationToken cancellationToken)
    {
        if (this.Settings.GetBoolValue("CreateBlobContainerIfNotExists", () => null) ?? false)
        {
            BlobContainerClient containerClient = this.GetContainerClient(path);
            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                await this.Client.CreateBlobContainerAsync(path.Host, cancellationToken: cancellationToken);
            }
        }
    }
}