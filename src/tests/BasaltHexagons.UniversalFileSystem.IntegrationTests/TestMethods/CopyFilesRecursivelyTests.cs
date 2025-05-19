using BasaltHexagons.UniversalFileSystem.Core;

namespace BasaltHexagons.UniversalFileSystem.IntegrationTests.TestMethods;

[TestClass]
public class CopyFilesRecursivelyTests
{
    [TestMethod]
    [DynamicData(nameof(UniversalFileSystemStore.GetTwoUniversalFileSystem), typeof(UniversalFileSystemStore), DynamicDataSourceType.Method)]
    public async Task CopyFilesRecursively_ToDiffDirectory(IUniversalFileSystem ufs, UriWrapper u1, UriWrapper u2)
    {
        // setup
        await ufs.PutFileAsync(u1.GetFullUri("src/test1.txt"), "test content1", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/test2.txt"), "test content2", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/folder1/test3.txt"), "test content3", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/folder1/folder11/test4.txt"), "test content4", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/folder2/test5.txt"), "test content5", false);

        // test
        await ufs.CopyFilesRecursivelyAsync(u1.GetFullUri("src/"), u2.GetFullUri("dest/"), false);

        // verify
        ufs.VerifyObject(u2.GetFullUri("dest/test1.txt"), ObjectType.File, "test content1");
        ufs.VerifyObject(u2.GetFullUri("dest/test2.txt"), ObjectType.File, "test content2");
        ufs.VerifyObject(u2.GetFullUri("dest/folder1/test3.txt"), ObjectType.File, "test content3");
        ufs.VerifyObject(u2.GetFullUri("dest/folder1/folder11/test4.txt"), ObjectType.File, "test content4");
        ufs.VerifyObject(u2.GetFullUri("dest/folder2/test5.txt"), ObjectType.File, "test content5");
    }

    [DataTestMethod]
    [DynamicData(nameof(UniversalFileSystemStore.GetTwoUniversalFileSystem), typeof(UniversalFileSystemStore), DynamicDataSourceType.Method)]
    public async Task CopyFilesRecursively_Overwrite(IUniversalFileSystem ufs, UriWrapper u1, UriWrapper u2)
    {
        // setup
        await ufs.PutFileAsync(u1.GetFullUri("src/test1.txt"), "test content1", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/test2.txt"), "test content2", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/folder1/test3.txt"), "test content3", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/folder1/folder11/test4.txt"), "test content4", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/folder2/test5.txt"), "test content5", false);


        await ufs.PutFileAsync(u2.GetFullUri("dest/test2.txt"), "test content dest 2", false);
        await ufs.PutFileAsync(u2.GetFullUri("dest/folder1/folder11/test4.txt"), "test content dest 4", false);

        // test
        await ufs.CopyFilesRecursivelyAsync(u1.GetFullUri("src/"), u2.GetFullUri("dest/"), true);

        // verify
        ufs.VerifyObject(u2.GetFullUri("dest/test1.txt"), ObjectType.File, "test content1");
        ufs.VerifyObject(u2.GetFullUri("dest/test2.txt"), ObjectType.File, "test content2");
        ufs.VerifyObject(u2.GetFullUri("dest/folder1/test3.txt"), ObjectType.File, "test content3");
        ufs.VerifyObject(u2.GetFullUri("dest/folder1/folder11/test4.txt"), ObjectType.File, "test content4");
        ufs.VerifyObject(u2.GetFullUri("dest/folder2/test5.txt"), ObjectType.File, "test content5");
    }

    [DataTestMethod]
    [DynamicData(nameof(UniversalFileSystemStore.GetTwoUniversalFileSystem), typeof(UniversalFileSystemStore), DynamicDataSourceType.Method)]
    public async Task CopyFilesRecursively_NotOverwrite(IUniversalFileSystem ufs, UriWrapper u1, UriWrapper u2)
    {
        // setup
        await ufs.PutFileAsync(u1.GetFullUri("src/test1.txt"), "test content1", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/test2.txt"), "test content2", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/folder1/test3.txt"), "test content3", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/folder1/folder11/test4.txt"), "test content4", false);
        await ufs.PutFileAsync(u1.GetFullUri("src/folder2/test5.txt"), "test content5", false);


        await ufs.PutFileAsync(u2.GetFullUri("dest/test2.txt"), "test content dest 2", false);
        await ufs.PutFileAsync(u2.GetFullUri("dest/folder1/folder11/test4.txt"), "test content dest 4", false);

        // test
        await ufs.CopyFilesRecursivelyAsync(u1.GetFullUri("src/"), u2.GetFullUri("dest/"), false);

        // verify
        ufs.VerifyObject(u2.GetFullUri("dest/test1.txt"), ObjectType.File, "test content1");
        ufs.VerifyObject(u2.GetFullUri("dest/test2.txt"), ObjectType.File, "test content dest 2");
        ufs.VerifyObject(u2.GetFullUri("dest/folder1/test3.txt"), ObjectType.File, "test content3");
        ufs.VerifyObject(u2.GetFullUri("dest/folder1/folder11/test4.txt"), ObjectType.File, "test content dest 4");
        ufs.VerifyObject(u2.GetFullUri("dest/folder2/test5.txt"), ObjectType.File, "test content5");
    }
}