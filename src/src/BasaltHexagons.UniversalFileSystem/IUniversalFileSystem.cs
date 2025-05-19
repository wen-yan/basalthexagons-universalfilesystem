using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Exceptions;

namespace BasaltHexagons.UniversalFileSystem;

public interface IUniversalFileSystem : IAsyncDisposable
{
    IAsyncEnumerable<ObjectMetadata> ListObjectsAsync(Uri prefix, bool recursive, CancellationToken cancellationToken);
    Task<ObjectMetadata> GetFileMetadataAsync(Uri uri, CancellationToken cancellationToken);
    Task<Stream> GetFileAsync(Uri uri, CancellationToken cancellationToken);
    Task PutFileAsync(Uri uri, Stream content, bool overwrite, CancellationToken cancellationToken);
    Task<bool> DeleteFileAsync(Uri uri, CancellationToken cancellationToken);
    Task MoveFileAsync(Uri oldUri, Uri newUri, bool overwrite, CancellationToken cancellationToken);
    Task CopyFileAsync(Uri sourceUri, Uri destUri, bool overwrite, CancellationToken cancellationToken);
    Task<bool> DoesFileExistAsync(Uri uri, CancellationToken cancellationToken);
}

public static class UniversalFileSystemNoCancellationExtensions
{
    public static IAsyncEnumerable<ObjectMetadata> ListObjectsAsync(this IUniversalFileSystem ufs, Uri prefix, bool recursive)
        => ufs.ListObjectsAsync(prefix, recursive, CancellationToken.None);

    public static Task<ObjectMetadata> GetFileMetadataAsync(this IUniversalFileSystem ufs, Uri uri)
        => ufs.GetFileMetadataAsync(uri, CancellationToken.None);

    public static Task<Stream> GetFileAsync(this IUniversalFileSystem ufs, Uri uri)
        => ufs.GetFileAsync(uri, CancellationToken.None);

    public static Task PutFileAsync(this IUniversalFileSystem ufs, Uri uri, Stream content, bool overwrite)
        => ufs.PutFileAsync(uri, content, overwrite, CancellationToken.None);

    public static Task<bool> DeleteFileAsync(this IUniversalFileSystem ufs, Uri uri)
        => ufs.DeleteFileAsync(uri, CancellationToken.None);

    public static Task MoveFileAsync(this IUniversalFileSystem ufs, Uri oldUri, Uri newUri, bool overwrite)
        => ufs.MoveFileAsync(oldUri, newUri, overwrite, CancellationToken.None);

    public static Task CopyFileAsync(this IUniversalFileSystem ufs, Uri sourceUri, Uri destUri, bool overwrite)
        => ufs.CopyFileAsync(sourceUri, destUri, overwrite, CancellationToken.None);

    public static Task<bool> DoesFileExistAsync(this IUniversalFileSystem ufs, Uri uri)
        => ufs.DoesFileExistAsync(uri, CancellationToken.None);
}

public static class UniversalFileSystemExtensions
{
    public static async Task<string> GetFileStringAsync(this IUniversalFileSystem ufs, Uri uri, CancellationToken cancellationToken = default)
    {
        await using Stream stream = await ufs.GetFileAsync(uri, cancellationToken);
        using TextReader reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public static async Task PutFileAsync(this IUniversalFileSystem ufs, Uri uri, string content, bool overwrite, CancellationToken cancellationToken = default)
    {
        await using MemoryStream stream = new();
        await using (TextWriter writer = new StreamWriter(stream, leaveOpen: true))
        {
            await writer.WriteAsync(content.AsMemory(), cancellationToken);
        }

        stream.Seek(0, SeekOrigin.Begin);
        await ufs.PutFileAsync(uri, stream, overwrite, cancellationToken);
    }

    public static async Task CopyFilesRecursivelyAsync(this IUniversalFileSystem ufs, Uri sourceUri, Uri destUri, bool overwrite, CancellationToken cancellationToken = default)
    {
        IAsyncEnumerable<ObjectMetadata> files = ufs.ListObjectsAsync(sourceUri, true, cancellationToken)
            .Where(x => x.ObjectType == ObjectType.File);

        await foreach (ObjectMetadata file in files.WithCancellation(cancellationToken))
        {
            Uri relativeUri = sourceUri.MakeRelativeUri(file.Uri);
            Uri targetUri = new(destUri, relativeUri);

            try
            {
                await ufs.CopyFileAsync(file.Uri, targetUri, overwrite, cancellationToken);
            }
            catch (FileExistsException) when (!overwrite)
            {
                // Ignore this exception, as it's expected.
            }
        }
    }
}