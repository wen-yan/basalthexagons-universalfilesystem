using Amazon.Runtime;
using Amazon.S3;
using Azure.Storage;
using Azure.Storage.Blobs;
using BasaltHexagons.UniversalFileSystem.AliyunOss;
using BasaltHexagons.UniversalFileSystem.AwsS3;
using BasaltHexagons.UniversalFileSystem.AzureBlob;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.File;
using BasaltHexagons.UniversalFileSystem.Memory;
using Microsoft.Extensions.Configuration.Yaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BasaltHexagons.UniversalFileSystem.IntegrationTests;

public static class UniversalFileSystemStore
{
    public static IEnumerable<object[]> GetSingleUniversalFileSystem()
    {
        IUniversalFileSystem ufs = CreateUniversalFileSystem();
        IEnumerable<UriWrapper> uriWrappers = CreateUriWrappers(ufs);

        return uriWrappers
            .Select(x =>
            {
                InitializeUfsWrapperAsync(ufs, x).Wait();
                return new object[] { ufs, x };
            });
    }

    public static IEnumerable<object[]> GetTwoUniversalFileSystem()
    {
        IUniversalFileSystem ufs = CreateUniversalFileSystem();
        IEnumerable<UriWrapper> uriWrappers = CreateUriWrappers(ufs);

        return uriWrappers.Join(uriWrappers, _ => true, _ => true, (x, y) =>
            {
                // This is used for debugging
                // if (x.ToString() != "abfss" || y.ToString() != "abfss2")
                //     return null;
                InitializeUfsWrappersAsync(ufs, x, y).Wait();
                return new object[] { ufs, x, y };
            })
            .Where(x => x != null)
            .Cast<object[]>();
    }

    private static IEnumerable<UriWrapper> CreateUriWrappers(IUniversalFileSystem ufs)
    {
        UriWrapper CreateUriWrapper(IUniversalFileSystem ufs, string name, string baseUri) => new(name, baseUri);

        // UriWrapper CreateFileUriWrapper(IUniversalFileSystem ufs, string name)
        // {
        //     string root = $"{Environment.CurrentDirectory}/ufs-it-file";
        //     return new(name, $"file://{root}/");
        // }

        List<UriWrapper> wrappers =
        [
            CreateUriWrapper(ufs, "memory", "memory://"),
            // CreateFileUriWrapper(ufs, "file"),       // TODO: #38
            CreateUriWrapper(ufs, "s3", "s3://ufs-it-s3"),
            CreateUriWrapper(ufs, "s3-custom-client", "s3://ufs-it-s3-custom-client"),
            CreateUriWrapper(ufs, "abfss", "abfss://ufs-it-abfss"),
            CreateUriWrapper(ufs, "abfss-custom-client", "abfss://ufs-it-abfss-custom-client"),
            // CreateUriWrapper(ufs, "oss", "oss://ufs-it-oss"),   # Can't find a oss emulator which works
        ];
        return wrappers;
    }

    private static IUniversalFileSystem CreateUniversalFileSystem()
    {
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, builder) => { builder.AddYamlFile("it-settings.yaml", false, false); })
            .ConfigureServices((context, services) =>
            {
                services
                    .AddUniversalFileSystem("UniversalFileSystemStore")
                    .AddMemoryFileSystem()
                    .AddFileFileSystem()
                    .AddAwsS3FileSystem()
                    .AddAwsS3CustomClient("S3CustomClient", _ =>
                        new AmazonS3Client(new BasicAWSCredentials("test", "test"),
                            new AmazonS3Config()
                            {
                                ServiceURL = "http://localhost:4566",
                                ForcePathStyle = true,
                            }))
                    .AddAzureBlobFileSystem()
                    .AddAzureBlobCustomClient("AzureBlobCustomClient", _ =>
                        new BlobServiceClient(new Uri("http://localhost:10000/account2"),
                            new StorageSharedKeyCredential("account2", "a2V5Mg==")))
                    .AddAliyunOssFileSystem();
            })
            .Build();
        return host.Services.GetRequiredService<IUniversalFileSystem>();
    }

    private static async Task InitializeUfsWrappersAsync(IUniversalFileSystem ufs, params UriWrapper[] uriWrappers)
    {
        foreach (UriWrapper uriWrapper in uriWrappers)
        {
            await InitializeUfsWrapperAsync(ufs, uriWrapper);
        }
    }

    private static async Task InitializeUfsWrapperAsync(IUniversalFileSystem ufs, UriWrapper uriWrapper)
    {
        if (uriWrapper.BaseUri.Scheme.StartsWith("file"))
        {
            string root = uriWrapper.BaseUri.LocalPath;

            // Delete all files
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
        else
        {
            // delete all files
            IAsyncEnumerable<ObjectMetadata> allFiles = ufs.ListObjectsAsync(uriWrapper.GetFullUri(""), true);
            await foreach (ObjectMetadata file in allFiles)
                await ufs.DeleteFileAsync(file.Uri);
        }
    }
}