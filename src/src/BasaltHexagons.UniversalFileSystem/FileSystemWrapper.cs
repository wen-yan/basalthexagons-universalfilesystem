using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Disposing;

namespace BasaltHexagons.UniversalFileSystem;

/// <summary>
/// Wrapper of IFileSystem
/// - Check if object is disposed
/// - Handle exceptions
/// </summary>
[AsyncMethodBuilder(typeof(ContinueOnAnyAsyncMethodBuilder))]
class FileSystemWrapper : AsyncDisposable, IFileSystem
{
    public FileSystemWrapper(IFileSystem fileSystem)
    {
        this.FileSystem = fileSystem;
    }

    private IFileSystem FileSystem { get; }

    #region IFileSystem

    public Task CopyFileAsync(Uri sourceUri, Uri destUri, bool overwrite, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.FileSystem.CopyFileAsync(sourceUri, destUri, overwrite, cancellationToken);
    }

    public Task<bool> DeleteFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.FileSystem.DeleteFileAsync(uri, cancellationToken);
    }

    public Task<Stream> GetFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.FileSystem.GetFileAsync(uri, cancellationToken);
    }

    public Task<ObjectMetadata> GetFileMetadataAsync(Uri uri, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.FileSystem.GetFileMetadataAsync(uri, cancellationToken);
    }

    public IAsyncEnumerable<ObjectMetadata> ListObjectsAsync(Uri prefix, bool recursive, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.FileSystem.ListObjectsAsync(prefix, recursive, cancellationToken);
    }

    public Task MoveFileAsync(Uri oldUri, Uri newUri, bool overwrite, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.FileSystem.MoveFileAsync(oldUri, newUri, overwrite, cancellationToken);
    }

    public Task PutFileAsync(Uri uri, Stream stream, bool overwrite, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.FileSystem.PutFileAsync(uri, stream, overwrite, cancellationToken);
    }

    public Task<bool> DoesFileExistAsync(Uri uri, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.FileSystem.DoesFileExistAsync(uri, cancellationToken);
    }

    #endregion
}