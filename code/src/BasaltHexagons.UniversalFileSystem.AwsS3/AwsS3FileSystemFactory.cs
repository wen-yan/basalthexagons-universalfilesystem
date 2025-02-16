﻿using System;

using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;

using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BasaltHexagons.UniversalFileSystem.AwsS3;

enum clientCredentialType
{
    Basic,                      // BasicAWSCredentials
    EnvironmentVariables,       // EnvironmentVariablesAWSCredentials
    Profile,                    // StoredProfileAWSCredentials
}

/// <summary>
/// ImplementationConfiguration
///     Client:
///         Credentials
///             Type: Anonymous/Basic/EnvironmentVariables/Profile
///             AccessKey:     # Type = Basic
///             SecretKey:     # Type = Basic
///             ProfileName:   # Type = Profile
///         Options
///             RegionEndpoint:
///             ServiceURL:
///             ForcePathStyle: true/false
/// </summary>
class AwsS3FileSystemFactory : IFileSystemFactory
{
    public const string CustomClientServiceKey = "BasaltHexagons.UniversalFileSystem.AwsS3.AwsS3FileSystemFactory.CustomAmazonS3Client";

    public AwsS3FileSystemFactory(IServiceProvider serviceProvider)
    {
        this.ServiceProvider = serviceProvider;
    }

    private IServiceProvider ServiceProvider { get; }

    public IFileSystem Create(IConfiguration implementationConfiguration)
    {
        IConfigurationSection clientConfig = implementationConfiguration.GetSection("Client");

        IAmazonS3 client = clientConfig.Exists()
            ? this.CreateAmazonS3ClientFromConfiguration(clientConfig)
            : this.ServiceProvider.GetRequiredKeyedService<IAmazonS3>(CustomClientServiceKey);

        return new AwsS3FileSystem(client);
    }

    private IAmazonS3 CreateAmazonS3ClientFromConfiguration(IConfiguration implementationConfiguration)
    {
        // credentials
        clientCredentialType clientCredentialType = implementationConfiguration.GetEnumValue<clientCredentialType>("Credentials:Type");

        AWSCredentials credentials = clientCredentialType switch
        {
            clientCredentialType.Basic => CreateBasicAWSCredentials(implementationConfiguration),
            clientCredentialType.EnvironmentVariables => new EnvironmentVariablesAWSCredentials(),
            clientCredentialType.Profile => CreateStoredProfileAWSCredentials(implementationConfiguration),
            _ => throw new ConfigurationException($"Unknown client credential type [{clientCredentialType}]"),
        };

        // config
        AmazonS3Config config = new AmazonS3Config();
        string? regionEndpoint = implementationConfiguration.GetValue<string>("Options:RegionEndpoint", () => null);
        string? serviceUrl = implementationConfiguration.GetValue<string>("Options:ServiceURL", () => null);
        bool? forcePathStyle = implementationConfiguration.GetBoolValue("Options:ForcePathStyle", () => null);

        if (regionEndpoint != null) config.RegionEndpoint = RegionEndpoint.GetBySystemName(regionEndpoint);
        if (serviceUrl != null) config.ServiceURL = serviceUrl;
        if (forcePathStyle != null) config.ForcePathStyle = forcePathStyle.Value;

        return new AmazonS3Client(credentials, config);
    }

    // Create client
    private AWSCredentials CreateBasicAWSCredentials(IConfiguration implementationConfiguration)
    {
        string accessKey = implementationConfiguration.GetValue<string>("Credentials:AccessKey");
        string secretKey = implementationConfiguration.GetValue<string>("Credentials:SecretKey");
        return new BasicAWSCredentials(accessKey, secretKey);
    }

    private AWSCredentials CreateStoredProfileAWSCredentials(IConfiguration implementationConfiguration)
    {
        string profileName = implementationConfiguration.GetValue<string>("Credentials:ProfileName");

        SharedCredentialsFile credentialsFile = new SharedCredentialsFile();
        CredentialProfile? credentialProfile = credentialsFile.TryGetProfile(profileName, out CredentialProfile value) ? value : null;

        if (credentialProfile == null)
            throw new ConfigurationException($"Unknown profile name [{profileName}]");

        return credentialProfile.GetAWSCredentials(credentialsFile);
    }
}
