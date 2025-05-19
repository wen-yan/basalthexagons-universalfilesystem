using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Disposing;

namespace BasaltHexagons.UniversalFileSystem;

[AsyncMethodBuilder(typeof(ContinueOnAnyAsyncMethodBuilder))]
class UniversalFileSystem : AsyncDisposable, IUniversalFileSystem
{
    public UniversalFileSystem(IFileSystemStore implStore)
    {
        this.ImplStore = implStore;
    }

    private IFileSystemStore ImplStore { get; }

    private IFileSystem GetImpl(Uri uri) => this.ImplStore.Create(uri);


    #region IUniversalFileSystem

    public IAsyncEnumerable<ObjectMetadata> ListObjectsAsync(Uri prefix, bool recursive, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.GetImpl(prefix).ListObjectsAsync(prefix, recursive, cancellationToken);
    }

    public Task<ObjectMetadata> GetFileMetadataAsync(Uri uri, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.GetImpl(uri).GetFileMetadataAsync(uri, cancellationToken);
    }

    public Task<Stream> GetFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.GetImpl(uri).GetFileAsync(uri, cancellationToken);
    }

    public Task PutFileAsync(Uri uri, Stream stream, bool overwrite, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.GetImpl(uri).PutFileAsync(uri, stream, overwrite, cancellationToken);
    }

    public Task<bool> DeleteFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.GetImpl(uri).DeleteFileAsync(uri, cancellationToken);
    }

    public async Task MoveFileAsync(Uri oldUri, Uri newUri, bool overwrite, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        IFileSystem impl1 = this.GetImpl(oldUri);
        IFileSystem impl2 = this.GetImpl(newUri);

        if (!IsSameFileSystem(impl1, impl2))
        {
            await using (Stream stream = await impl1.GetFileAsync(oldUri, cancellationToken))
            {
                await impl2.PutFileAsync(newUri, stream, overwrite, cancellationToken);
            }

            await impl1.DeleteFileAsync(oldUri, cancellationToken);
        }
        else
        {
            await impl1.MoveFileAsync(oldUri, newUri, overwrite, cancellationToken);
        }
    }

    public async Task CopyFileAsync(Uri sourceUri, Uri destUri, bool overwrite, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        IFileSystem impl1 = this.GetImpl(sourceUri);
        IFileSystem impl2 = this.GetImpl(destUri);

        if (!IsSameFileSystem(impl1, impl2))
        {
            await using Stream stream = await impl1.GetFileAsync(sourceUri, cancellationToken);
            await impl2.PutFileAsync(destUri, stream, overwrite, cancellationToken);
        }
        else
        {
            await impl1.CopyFileAsync(sourceUri, destUri, overwrite, cancellationToken);
        }
    }

    public Task<bool> DoesFileExistAsync(Uri uri, CancellationToken cancellationToken)
    {
        this.CheckIsDisposed();
        return this.GetImpl(uri).DoesFileExistAsync(uri, cancellationToken);
    }

    #endregion

    private static bool IsSameFileSystem(IFileSystem impl1, IFileSystem impl2) => object.ReferenceEquals(impl1, impl2);
}