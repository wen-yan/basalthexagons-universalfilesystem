using System;
using System.Runtime.CompilerServices;
using Aliyun.OSS;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BasaltHexagons.UniversalFileSystem.AliyunOss;

enum ClientCredentialType
{
    Default,
}

/// <summary>
/// Implementation:
///     Client:
///         Endpoint:
///         Credentials
///             Type: Default
///             AccessKey:     # Type = Default
///             SecretKey:     # Type = Default
///             SecurityToken: # Type = Default
///     Settings:
///         CreateBucketIfNotExists: false
/// </summary>
[AsyncMethodBuilder(typeof(ContinueOnAnyAsyncMethodBuilder))]
class AliyunOssFileSystemFactory : IFileSystemFactory
{
    public static readonly string CustomClientServiceKey = $"{typeof(AliyunOssFileSystemFactory).FullName}.CustomOssClient";

    public AliyunOssFileSystemFactory(IServiceProvider serviceProvider)
    {
        this.ServiceProvider = serviceProvider;
    }

    private IServiceProvider ServiceProvider { get; }

    public IFileSystem Create(IConfiguration implementationConfiguration)
    {
        IConfigurationSection clientConfig = implementationConfiguration.GetSection("Client");

        IOss client = clientConfig.Exists()
            ? this.CreateOssClientFromConfiguration(clientConfig)
            : this.ServiceProvider.GetRequiredKeyedService<IOss>(CustomClientServiceKey);

        return new AliyunOssFileSystem(client, implementationConfiguration.GetSection("Settings"));
    }

    private IOss CreateOssClientFromConfiguration(IConfiguration implementationConfiguration)
    {
        ClientCredentialType clientCredentialType = implementationConfiguration.GetEnumValue<ClientCredentialType>("Credentials:Type");

        IOss client = clientCredentialType switch
        {
            ClientCredentialType.Default => this.CreateDefaultCredentialClient(implementationConfiguration),
            _ => throw new ConfigurationException($"Unknown client credential type [{clientCredentialType}]"),
        };
        return client;
    }

    private IOss CreateDefaultCredentialClient(IConfiguration implementationConfiguration)
    {
        string endpoint = implementationConfiguration.GetValue<string>("Endpoint");
        string accessKey = implementationConfiguration.GetValue<string>("Credentials:AccessKey");
        string secretKey = implementationConfiguration.GetValue<string>("Credentials:SecretKey");
        string? securityToken = implementationConfiguration.GetValue<string>("Credentials:SecurityToken", () => null);

        return new OssClient(endpoint, accessKey, secretKey, securityToken);
    }
}