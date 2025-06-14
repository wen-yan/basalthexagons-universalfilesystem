﻿using System;
using Azure.Storage.Blobs;
using Basalt.UniversalFileSystem.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Basalt.UniversalFileSystem.AzureBlob;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureBlobFileSystem(this IServiceCollection services)
    {
        return services
            .AddKeyedSingleton<IFileSystemFactory, AzureBlobFileSystemFactory>(typeof(AzureBlobFileSystemFactory).FullName)
            .AddSingleton<IFileSystemFactory>(serviceProvider => serviceProvider.GetRequiredKeyedService<IFileSystemFactory>(typeof(AzureBlobFileSystemFactory).FullName));
    }

    public static IServiceCollection AddAzureBlobCustomClient(this IServiceCollection services, string name, Func<IServiceProvider, BlobServiceClient> implementationFactory)
    {
        string key = AzureBlobFileSystemFactory.GetCustomClientServiceKey(name);
        return services
            .AddKeyedTransient<BlobServiceClient>(key, (serviceProvider, serviceKey) => implementationFactory(serviceProvider));
    }
}