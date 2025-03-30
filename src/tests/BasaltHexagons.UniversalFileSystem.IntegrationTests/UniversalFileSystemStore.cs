using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BasaltHexagons.UniversalFileSystem.AliyunOss;
using BasaltHexagons.UniversalFileSystem.AwsS3;
using BasaltHexagons.UniversalFileSystem.AzureBlob;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.File;
using BasaltHexagons.UniversalFileSystem.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BasaltHexagons.UniversalFileSystem.IntegrationTests;

public abstract class UniversalFileSystemStore
{
    public static IEnumerable<object[]> GetAllUniversalFileSystems()
    {
        yield return [CreateMemoryUniversalFileSystem()];
        yield return [CreateFileUniversalFileSystem()];
        yield return [CreateAwsS3UniversalFileSystem()];
        yield return [CreateAzureBlobUniversalFileSystem()];
        // yield return [CreateAliyunOssUniversalFileSystem()];
    }

    private static UniversalFileSystemTestWrapper CreateMemoryUniversalFileSystem()
    {
        return CreateUniversalFileSystem(
            builder => { builder.AddInMemoryCollection(new Dictionary<string, string?> { ["Schemes:memory:ImplementationFactoryClass"] = "BasaltHexagons.UniversalFileSystem.Memory.MemoryFileSystemFactory" }); },
            services => services.AddMemoryFileSystem(),
            $"memory://");
    }

    private static UniversalFileSystemTestWrapper CreateFileUniversalFileSystem()
    {
        string root = $"{Environment.CurrentDirectory}/ufs-integration-test-file";

        // Delete all files
        if (Directory.Exists(root))
        {
            foreach (string file in Directory.GetFiles(root))
            {
                System.IO.File.Delete(file);
            }

            // Delete all subdirectories and their contents
            foreach (string subDirectory in Directory.GetDirectories(root))
            {
                Directory.Delete(subDirectory, true); // true for recursive deletion
            }
        }

        return CreateUniversalFileSystem(
            builder => { builder.AddInMemoryCollection(new Dictionary<string, string?> { ["Schemes:file:ImplementationFactoryClass"] = "BasaltHexagons.UniversalFileSystem.File.FileFileSystemFactory" }); },
            services => services.AddFileFileSystem(),
            $"file://{root}/");
    }

    private static UniversalFileSystemTestWrapper CreateAwsS3UniversalFileSystem()
    {
        return CreateUniversalFileSystem(
            builder =>
            {
                builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Schemes:s3:ImplementationFactoryClass"] = "BasaltHexagons.UniversalFileSystem.AwsS3.AwsS3FileSystemFactory",
                    ["Schemes:s3:Implementation:Client:Credentials:Type"] = "Basic",
                    ["Schemes:s3:Implementation:Client:Credentials:AccessKey"] = "test",
                    ["Schemes:s3:Implementation:Client:Credentials:SecretKey"] = "test",
                    ["Schemes:s3:Implementation:Client:Options:ServiceURL"] = "http://localhost:4566",
                    ["Schemes:s3:Implementation:Client:Options:ForcePathStyle"] = "true",
                    ["Schemes:s3:Implementation:Settings:CreateBucketIfNotExists"] = "true",
                });
            },
            services => services.AddAwsS3FileSystem(),
            $"s3://ufs-integration-test-s3");
    }
    
    private static UniversalFileSystemTestWrapper CreateAzureBlobUniversalFileSystem()
    {
        return CreateUniversalFileSystem(
            builder =>
            {
                builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Schemes:abfss:ImplementationFactoryClass"] = "BasaltHexagons.UniversalFileSystem.AzureBlob.AzureBlobFileSystemFactory",
                    ["Schemes:abfss:Implementation:Client:ServiceUri"] = "http://localhost:10000/devstoreaccount1",
                    ["Schemes:abfss:Implementation:Client:Credentials:Type"] = "SharedKey",
                    ["Schemes:abfss:Implementation:Client:Credentials:AccountName"] = "devstoreaccount1",
                    ["Schemes:abfss:Implementation:Client:Credentials:AccountKey"] = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==",
                    ["Schemes:abfss:Implementation:Settings:CreateBlobContainerIfNotExists"] = "true",
                });
            },
            services => services.AddAzureBlobFileSystem(),
            $"abfss://ufs-integration-test-abfss");
    }

    private static UniversalFileSystemTestWrapper CreateAliyunOssUniversalFileSystem()
    {
        return CreateUniversalFileSystem(
            builder =>
            {
                builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Schemes:oss:ImplementationFactoryClass"] = "BasaltHexagons.UniversalFileSystem.AliyunOss.AliyunOssFileSystemFactory",
                    ["Schemes:oss:Implementation:Client:Endpoint"] = "127.0.0.1:8280",
                    ["Schemes:oss:Implementation:Client:Credentials:Type"] = "Default",
                    ["Schemes:oss:Implementation:Client:Credentials:AccessKey"] = "AccessKeyId",
                    ["Schemes:oss:Implementation:Client:Credentials:SecretKey"] = "AccessKeySecret",
                    ["Schemes:oss:Implementation:Settings:CreateBucketIfNotExists"] = "true",
                });
            },
            services => services.AddAliyunOssFileSystem(),
            $"oss://ufs-integration-test-oss");
    }

    private static UniversalFileSystemTestWrapper CreateUniversalFileSystem(Action<IConfigurationBuilder> configurationBuilder, Func<IServiceCollection, IServiceCollection> servicesBuilder, string baseUri)
    {
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, builder) => configurationBuilder(builder))
            .ConfigureServices((context, services) =>
            {
                servicesBuilder(services)
                    .AddTransient<IUniversalFileSystem>(serviceProvider =>
                    {
                        IConfiguration config = serviceProvider.GetRequiredService<IConfiguration>();
                        return UniversalFileSystemFactory.Create(serviceProvider, config);
                    });
            })
            .Build();

        UniversalFileSystemTestWrapper ufs = new(host, new Uri(baseUri), host.Services.GetRequiredService<IUniversalFileSystem>());

        // delete all files
        List<ObjectMetadata> allFiles = ufs.ListObjectsAsync("", true).ToListAsync().Result;
        foreach (ObjectMetadata file in allFiles)
            ufs.DeleteFileAsync(file.Uri, default).Wait();
        return ufs;
    }
}