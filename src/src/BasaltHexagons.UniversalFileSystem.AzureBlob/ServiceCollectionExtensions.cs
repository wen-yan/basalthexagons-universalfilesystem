using System;
using Azure.Storage.Blobs;
using BasaltHexagons.UniversalFileSystem.Core;
using Microsoft.Extensions.DependencyInjection;

namespace BasaltHexagons.UniversalFileSystem.AzureBlob;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureBlobFileSystem(this IServiceCollection services)
    {
        return services
            .AddKeyedTransient<IFileSystemFactory, AzureBlobFileSystemFactory>(typeof(AzureBlobFileSystemFactory).FullName);
    }

    public static IServiceCollection AddAzureBlobServiceClient(this IServiceCollection services,
        Func<IServiceProvider, BlobServiceClient> implementationFactory)
    {
        return services
            .AddKeyedTransient<BlobServiceClient>(AzureBlobFileSystemFactory.CustomClientServiceKey,
                (serviceProvider, serviceKey) => implementationFactory(serviceProvider));
    }
}