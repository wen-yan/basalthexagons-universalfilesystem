using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Disposing;
using BasaltHexagons.UniversalFileSystem.Core.Exceptions;

namespace BasaltHexagons.UniversalFileSystem.File;

[AsyncMethodBuilder(typeof(ContinueOnAnyAsyncMethodBuilder))]
class FileFileSystem : AsyncDisposable, IFileSystem
{
    #region IFileSystem

    public async IAsyncEnumerable<ObjectMetadata> ListObjectsAsync(Uri prefix, bool recursive, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnumerationOptions enumerationOptions = new()
        {
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        async IAsyncEnumerable<ObjectMetadata> EnumerateDirectoryAsync(string directory, string startsWith)
        {
            foreach (string entry in Directory.EnumerateFiles(directory, $"{startsWith}*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();

                ObjectMetadata metadata = await this.GetObjectMetadataInternalAsync(new Uri(entry), false, cancellationToken);
                yield return metadata;
            }

            foreach (string entry in Directory.EnumerateDirectories(directory, $"{startsWith}*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();

                ObjectMetadata metadata = await this.GetObjectMetadataInternalAsync(new Uri(entry), true, cancellationToken);
                yield return metadata;

                if (!recursive) continue;

                await foreach (ObjectMetadata objectMetadata in EnumerateDirectoryAsync(entry, string.Empty))
                {
                    yield return objectMetadata;
                }
            }
        }

        int lastSlashIndex = prefix.AbsolutePath.LastIndexOf('/');
        string directory = prefix.AbsolutePath.Substring(0, lastSlashIndex);
        string startsWith = prefix.AbsolutePath.Substring(lastSlashIndex + 1);

        Uri directoryUri = new UriBuilder()
        {
            Scheme = prefix.Scheme,
            Host = prefix.Host,
            Path = directory,
        }.Uri;

        if (!await this.DoesDirectoryExistAsync(directoryUri, cancellationToken))
            yield break;

        await foreach (ObjectMetadata objectMetadata in EnumerateDirectoryAsync(directory, startsWith))
        {
            yield return objectMetadata;
        }
    }

    public Task<ObjectMetadata> GetFileMetadataAsync(Uri uri, CancellationToken cancellationToken)
    {
        return this.GetObjectMetadataInternalAsync(uri, false, cancellationToken);
    }

    public async Task<Stream> GetFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!await this.DoesFileExistAsync(uri, cancellationToken))
            throw new FileNotExistsException(uri);
        return new FileStream(uri.AbsolutePath, FileMode.Open, FileAccess.Read);
    }

    public async Task PutFileAsync(Uri uri, Stream stream, bool overwrite, CancellationToken cancellationToken)
    {
        if (!overwrite && await this.DoesFileExistAsync(uri, cancellationToken))
            throw new FileExistsException(uri);

        string? dir = Path.GetDirectoryName(uri.AbsolutePath);
        if (dir != null)
            Directory.CreateDirectory(dir);
        await using FileStream fileStream = new(uri.AbsolutePath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
        await stream.CopyToAsync(fileStream, cancellationToken);
    }

    public async Task<bool> DeleteFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!await this.DoesFileExistAsync(uri, cancellationToken))
            return false;

        System.IO.File.Delete(uri.AbsolutePath);
        return true;
    }

    public async Task MoveFileAsync(Uri oldPath, Uri newPath, bool overwrite, CancellationToken cancellationToken)
    {
        if (oldPath == newPath)
            throw new ArgumentException("Can't move file to itself.");

        if (!await this.DoesFileExistAsync(oldPath, cancellationToken))
            throw new FileNotExistsException(oldPath);

        if (!overwrite && await this.DoesFileExistAsync(newPath, cancellationToken))
            throw new FileExistsException(newPath);

        string? directory = Path.GetDirectoryName(newPath.AbsolutePath);
        if (directory == null)
            throw new ArgumentException($"Can't get directory from uri {newPath}");
        Directory.CreateDirectory(directory);

        System.IO.File.Move(oldPath.AbsolutePath, newPath.AbsolutePath, overwrite);
    }

    public async Task CopyFileAsync(Uri sourceUri, Uri destUri, bool overwrite, CancellationToken cancellationToken)
    {
        if (sourceUri == destUri)
            throw new ArgumentException("Can't copy file to itself.");

        if (!await this.DoesFileExistAsync(sourceUri, cancellationToken))
            throw new FileNotExistsException(sourceUri);

        if (!overwrite && await this.DoesFileExistAsync(destUri, cancellationToken))
            throw new FileExistsException(destUri);

        string? directory = Path.GetDirectoryName(destUri.AbsolutePath);
        if (directory == null)
            throw new ArgumentException($"Can't get directory from uri {destUri}");
        Directory.CreateDirectory(directory);

        System.IO.File.Copy(sourceUri.AbsolutePath, destUri.AbsolutePath, overwrite);
    }

    public Task<bool> DoesFileExistAsync(Uri uri, CancellationToken cancellationToken) =>
        Task.FromResult(System.IO.File.Exists(uri.AbsolutePath));

    #endregion

    private Task<bool> DoesDirectoryExistAsync(Uri uri, CancellationToken cancellationToken) =>
        Task.FromResult(System.IO.Directory.Exists(uri.AbsolutePath));

    private async Task<ObjectMetadata> GetObjectMetadataInternalAsync(Uri uri, bool returnDirectory, CancellationToken cancellationToken)
    {
        if (await this.DoesFileExistAsync(uri, cancellationToken))
            return new ObjectMetadata(uri, ObjectType.File, new FileInfo(uri.AbsolutePath).Length, System.IO.File.GetLastWriteTimeUtc(uri.AbsolutePath));

        if (returnDirectory && await this.DoesDirectoryExistAsync(uri, cancellationToken))
            return new ObjectMetadata(new Uri($"{uri}/"), ObjectType.Prefix, null, null);

        throw new FileNotExistsException(uri);
    }
}