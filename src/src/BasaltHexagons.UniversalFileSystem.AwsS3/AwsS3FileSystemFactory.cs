using System;
using System.Runtime.CompilerServices;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Configuration;
using BasaltHexagons.UniversalFileSystem.Core.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BasaltHexagons.UniversalFileSystem.AwsS3;

enum ClientCredentialType
{
    Basic, // BasicAWSCredentials
    EnvironmentVariables, // EnvironmentVariablesAWSCredentials
    Profile, // StoredProfileAWSCredentials
}

/// <summary>
/// Implementation:
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
///     Settings:
///         CreateBucketIfNotExists: false
/// </summary>
[AsyncMethodBuilder(typeof(ContinueOnAnyAsyncMethodBuilder))]
class AwsS3FileSystemFactory : IFileSystemFactory
{
    public static readonly string CustomClientServiceKey = $"{typeof(AwsS3FileSystemFactory).FullName}.CustomAmazonS3Client";

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

        return new AwsS3FileSystem(client, implementationConfiguration.GetSection("Settings"));
    }

    private IAmazonS3 CreateAmazonS3ClientFromConfiguration(IConfiguration implementationConfiguration)
    {
        // credentials
        ClientCredentialType clientCredentialType = implementationConfiguration.GetEnumValue<ClientCredentialType>("Credentials:Type");

        AWSCredentials credentials = clientCredentialType switch
        {
            ClientCredentialType.Basic => CreateBasicAWSCredentials(implementationConfiguration),
            ClientCredentialType.EnvironmentVariables => new EnvironmentVariablesAWSCredentials(),
            ClientCredentialType.Profile => CreateStoredProfileAWSCredentials(implementationConfiguration),
            _ => throw new InvalidEnumConfigurationValueException<ClientCredentialType>("Credentials:Type", clientCredentialType),
        };

        // config
        AmazonS3Config config = new();
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
            throw new InvalidConfigurationValueException("Credentials:ProfileName", profileName);

        return credentialProfile.GetAWSCredentials(credentialsFile);
    }
}