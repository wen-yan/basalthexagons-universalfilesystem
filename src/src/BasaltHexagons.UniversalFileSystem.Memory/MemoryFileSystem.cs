using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Disposing;

namespace BasaltHexagons.UniversalFileSystem.Memory;

record File(byte[] Content, DateTime LastModifiedTimeUtc);

[AsyncMethodBuilder(typeof(ContinueOnAnyAsyncMethodBuilder))]
class MemoryFileSystem : AsyncDisposable, IFileSystem
{
    private Dictionary<string, File> _files = new();

    #region IFileSystem

    public IAsyncEnumerable<ObjectMetadata> ListObjectsAsync(Uri prefix, bool recursive, CancellationToken cancellationToken)
    {
        async IAsyncEnumerable<ObjectMetadata> GetObjectsAsync(string pre)
        {
            HashSet<string> subDirectories = new();

            foreach ((string path, File file) in _files)
            {
                if (!path.StartsWith(pre)) continue;

                int nextSeparatorIndex = path.IndexOf('/', pre.Length);
                if (nextSeparatorIndex == -1)
                {
                    yield return new ObjectMetadata(MakeUri(prefix, path), ObjectType.File, file.Content.Length, file.LastModifiedTimeUtc);
                }
                else
                {
                    string subDirectory = path.Substring(0, nextSeparatorIndex + 1);
                    subDirectories.Add(subDirectory);
                }
            }

            foreach (string subDirectory in subDirectories)
            {
                yield return new ObjectMetadata(MakeUri(prefix, subDirectory), ObjectType.Prefix, null, null);
                if (recursive)
                {
                    await foreach (ObjectMetadata objectMetadata in GetObjectsAsync(subDirectory))
                    {
                        yield return objectMetadata;
                    }
                }
            }
        }

        return GetObjectsAsync(prefix.AbsolutePath);
    }

    public Task<ObjectMetadata?> GetFileMetadataAsync(Uri path, CancellationToken cancellationToken)
    {
        return _files.TryGetValue(path.AbsolutePath, out File? file)
            ? Task.FromResult<ObjectMetadata?>(new ObjectMetadata(path, ObjectType.File, file.Content.Length, file.LastModifiedTimeUtc))
            : Task.FromResult<ObjectMetadata?>(null);
    }

    public Task<Stream> GetFileAsync(Uri path, CancellationToken cancellationToken)
    {
        return _files.TryGetValue(path.AbsolutePath, out File? file)
            ? Task.FromResult<Stream>(new MemoryStream(file.Content))
            : throw new ArgumentException($"File not found: {path}", nameof(path));
    }

    public async Task PutFileAsync(Uri path, Stream stream, bool overwrite, CancellationToken cancellationToken)
    {
        if (!overwrite && await this.DoesFileExistAsync(path, cancellationToken))
            throw new ArgumentException($"File already exists: {path}", nameof(path));

        await using MemoryStream memoryStream = new();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        _files[path.AbsolutePath] = new File(memoryStream.ToArray(), DateTime.UtcNow);
    }

    public Task<bool> DeleteFileAsync(Uri path, CancellationToken cancellationToken)
    {
        bool exists = _files.TryGetValue(path.AbsolutePath, out File? _);
        if (exists)
            _files.Remove(path.AbsolutePath);
        return Task.FromResult(exists);
    }

    public async Task MoveFileAsync(Uri oldPath, Uri newPath, bool overwrite, CancellationToken cancellationToken)
    {
        if (oldPath == newPath)
            throw new ArgumentException("Can't move object to itself");

        bool oldExists = _files.TryGetValue(oldPath.AbsolutePath, out File? file);
        if (!oldExists)
            throw new ArgumentException($"File not found: {oldPath}", nameof(oldPath));

        if (!overwrite && await this.DoesFileExistAsync(newPath, cancellationToken))
            throw new ArgumentException($"Destination file already exists: {newPath}");

        _files.Remove(oldPath.AbsolutePath);
        _files[newPath.AbsolutePath] = file!;
    }

    public async Task CopyFileAsync(Uri sourcePath, Uri destPath, bool overwrite, CancellationToken cancellationToken)
    {
        if (sourcePath == destPath)
            throw new ArgumentException("Can't copy object to itself");

        bool sourceExists = _files.TryGetValue(sourcePath.AbsolutePath, out File? file);
        if (!sourceExists)
            throw new ArgumentException($"Source file not found: {sourcePath}", nameof(sourcePath));

        if (!overwrite && await this.DoesFileExistAsync(destPath, cancellationToken))
            throw new ArgumentException($"Destination file already exists: {destPath}");

        _files[destPath.AbsolutePath] = new File(file!.Content, DateTime.UtcNow);
    }

    public Task<bool> DoesFileExistAsync(Uri path, CancellationToken cancellationToken) => Task.FromResult(_files.ContainsKey(path.AbsolutePath));

    #endregion

    private static Uri MakeUri(Uri seed, string path) => new(seed, path);
}