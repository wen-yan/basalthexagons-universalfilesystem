using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Aliyun.OSS;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Configuration;
using BasaltHexagons.UniversalFileSystem.Core.Disposing;
using Microsoft.Extensions.Configuration;
using ObjectMetadata = BasaltHexagons.UniversalFileSystem.Core.ObjectMetadata;

namespace BasaltHexagons.UniversalFileSystem.AliyunOss;

[AsyncMethodBuilder(typeof(ContinueOnAnyAsyncMethodBuilder))]
class AliyunOssFileSystem : AsyncDisposable, IFileSystem
{
    public AliyunOssFileSystem(IOss client, IConfiguration settings)
    {
        this.Client = client;
        this.Settings = settings;
    }

    private IOss Client { get; }
    private IConfiguration Settings { get; }

    #region IFileSystem

    public async Task CopyFileAsync(Uri sourceUri, Uri destUri, bool overwrite, CancellationToken cancellationToken)
    {
        (string sourceBucketName, string sourceKey) = DeconstructUri(sourceUri);
        (string destBucketName, string destKey) = DeconstructUri(destUri);

        CopyObjectRequest request = new(sourceBucketName, sourceKey, destBucketName, destKey);
        CopyObjectResult result =
            await Task<CopyObjectResult>.Factory.FromAsync(this.Client.BeginCopyObject, this.Client.EndCopyResult, request, null);
    }

    public async IAsyncEnumerable<ObjectMetadata> ListObjectsAsync(Uri prefix, bool recursive, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        (string bucketName, string keyPrefix) = DeconstructUri(prefix);
        if (!this.Client.DoesBucketExist(bucketName))
            yield break;

        Queue<string> keyPrefixQueue = new();
        keyPrefixQueue.Enqueue(keyPrefix);

        while (keyPrefixQueue.Count > 0)
        {
            ListObjectsRequest request = new(bucketName)
            {
                Prefix = keyPrefix,
                Delimiter = "/",
            };

            while (true)
            {
                ObjectListing result = await Task<ObjectListing>.Factory
                    .FromAsync(this.Client.BeginListObjects, this.Client.EndListObjects, request, null);

                foreach (OssObjectSummary obj in result.ObjectSummaries)
                {
                    Uri path = ConstructUir(prefix.Scheme, obj.BucketName, obj.Key);
                    yield return new ObjectMetadata(path, ObjectType.File, obj.Size, obj.LastModified.ToUniversalTime());
                }

                foreach (string commonPrefix in result.CommonPrefixes)
                {
                    Uri path = ConstructUir(prefix.Scheme, bucketName, commonPrefix);
                    yield return new ObjectMetadata(path, ObjectType.Prefix, null, null);

                    if (recursive)
                    {
                        keyPrefixQueue.Enqueue(commonPrefix);
                    }
                }

                if (!result.IsTruncated)
                    break;
                request.Marker = result.NextMarker;
            }
        }
    }

    public Task<ObjectMetadata?> GetFileMetadataAsync(Uri uri, CancellationToken cancellationToken)
    {
        (string bucketName, string key) = DeconstructUri(uri);
        GetObjectMetadataRequest request = new(bucketName, key);

        Aliyun.OSS.ObjectMetadata metadata = this.Client.GetObjectMetadata(request);
        return Task.FromResult<ObjectMetadata?>(new ObjectMetadata(uri, ObjectType.File, metadata.ContentLength,
            metadata.LastModified.ToUniversalTime()));
    }

    public async Task<Stream> GetFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        (string bucketName, string key) = DeconstructUri(uri);
        GetObjectRequest request = new(bucketName, key);

        OssObject ossObject = await Task<OssObject>.Factory.FromAsync(
            this.Client.BeginGetObject, this.Client.EndGetObject, request, null);

        // TODO: dispose `ossObject`
        return ossObject.ResponseStream;
    }

    public async Task PutFileAsync(Uri uri, Stream stream, bool overwrite, CancellationToken cancellationToken)
    {
        (string bucketName, string key) = DeconstructUri(uri);

        if (!overwrite && this.Client.DoesObjectExist(bucketName, key))
            throw new ArgumentException($"Object {uri} already exists.");

        await this.TryCreateBucketIfNotExistsAsync(bucketName, cancellationToken);
        await Task<PutObjectResult>.Factory.FromAsync(this.Client.BeginPutObject, this.Client.EndPutObject, bucketName, key, stream, null);
    }

    public Task<bool> DeleteFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        (string bucketName, string key) = DeconstructUri(uri);
        if (!this.Client.DoesObjectExist(bucketName, key))
            return Task.FromResult(false);

        DeleteObjectResult result = this.Client.DeleteObject(bucketName, key);
        return Task.FromResult(true);
    }

    public async Task MoveFileAsync(Uri oldUri, Uri newUri, bool overwrite, CancellationToken cancellationToken)
    {
        await this.CopyFileAsync(oldUri, newUri, overwrite, cancellationToken);
        await this.DeleteFileAsync(oldUri, cancellationToken);
    }

    public Task<bool> DoesFileExistAsync(Uri uri, CancellationToken cancellationToken)
    {
        (string bucketName, string key) = DeconstructUri(uri);
        return Task.FromResult(this.Client.DoesObjectExist(bucketName, key));
    }

    #endregion

    private static Uri ConstructUir(string scheme, string bucket, string key)
    {
        UriBuilder builder = new()
        {
            Scheme = scheme,
            Host = bucket,
            Path = key,
        };
        return builder.Uri;
    }

    private static (string Bucket, string Key) DeconstructUri(Uri uri)
    {
        string bucket = uri.Host;
        string key = uri.AbsolutePath;//.TrimStart('/');
        return (bucket, key);
    }

    private Task TryCreateBucketIfNotExistsAsync(string bucketName, CancellationToken cancellationToken)
    {
        if ((this.Settings.GetBoolValue("CreateBucketIfNotExists", () => null) ?? false) &&
            !this.Client.DoesBucketExist(bucketName))
        {
            this.Client.CreateBucket(bucketName);
        }

        return Task.CompletedTask;
    }
}