using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.TestUtils;

namespace BasaltHexagons.UniversalFileSystem.IntegrationTests;

static class UniversalFileSystemTestExtensions
{
    public static ObjectMetadata MakeObjectMetadata(this IUniversalFileSystem ufs, Uri uri, ObjectType objectType, long? contentSize, DateTime? lastModifiedTimeUtc)
        => new(uri, objectType, contentSize, lastModifiedTimeUtc);

    public static ObjectMetadata MakeObjectMetadata(this IUniversalFileSystem ufs, Uri uri, ObjectType objectType, long? contentSize)
        => ufs.MakeObjectMetadata(uri, objectType, contentSize, DateTime.UtcNow);

    public static void VerifyObject(this IUniversalFileSystem ufs, Uri uri, ObjectType objectType, string? content, ObjectMetadata? actualMetadata)
    {
        ObjectMetadata expectedMetadata = objectType == ObjectType.File
            ? new ObjectMetadata(uri, objectType, content?.Length, DateTime.UtcNow)
            : new ObjectMetadata(uri, objectType, content?.Length, null);
        Assert.AreEqual(expectedMetadata, actualMetadata, new ObjectMetadataLastModifiedTimeUtcRangeEqualityComparer());

        if (objectType == ObjectType.File)
            Assert.AreEqual(content, ufs.GetFileStringAsync(uri).Result);
    }

    public static void VerifyObject(this IUniversalFileSystem ufs, Uri uri, ObjectType objectType, string? content)
    {
        ObjectMetadata actualMetadata = ufs.GetFileMetadataAsync(uri).Result;
        Assert.IsNotNull(actualMetadata);

        ufs.VerifyObject(uri, objectType, content, actualMetadata);
    }
}