using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BasaltHexagons.UniversalFileSystem.Core;
using BasaltHexagons.UniversalFileSystem.Core.Configuration;
using BasaltHexagons.UniversalFileSystem.Core.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BasaltHexagons.UniversalFileSystem;

interface IFileSystemStore
{
    IFileSystem Create(Uri uri);
}

[AsyncMethodBuilder(typeof(ContinueOnAnyAsyncMethodBuilder))]
class DefaultFileSystemStore : IFileSystemStore
{
    private readonly ConcurrentDictionary<string /* name */, Regex> _uriRegexes = new();
    private readonly ConcurrentDictionary<string /* name */, IFileSystem> _fileSystems = new();

    public DefaultFileSystemStore(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        this.ServiceProvider = serviceProvider;
        this.Configuration = configuration;
    }

    private IServiceProvider ServiceProvider { get; }
    private IConfiguration Configuration { get; }

    public IFileSystem Create(Uri uri)
    {
        IEnumerable<IConfigurationSection> fileSystemConfigurations = this.Configuration.GetSection("FileSystems").GetChildren();

        foreach (IConfigurationSection fileSystemConfiguration in fileSystemConfigurations)
        {
            Regex rxUri = this.GetUriRegexPattern(fileSystemConfiguration);
            Match rxUriMatch = rxUri.Match(uri.ToString());
            if (!rxUriMatch.Success) continue;

            IFileSystem fileSystem = this.GetFileSystem(fileSystemConfiguration);
            return fileSystem;
        }

        throw new NoMatchedFileSystemException(uri);
    }

    private Regex GetUriRegexPattern(IConfigurationSection fileSystemConfiguration)
    {
        string name = fileSystemConfiguration.Key;
        Regex rxUri = _uriRegexes.GetOrAdd(name, _ =>
        {
            string uriRegexPattern = fileSystemConfiguration.GetValue<string>("UriRegexPattern");
            return new Regex(uriRegexPattern, RegexOptions.Compiled);
        });
        return rxUri;
    }

    private IFileSystem GetFileSystem(IConfigurationSection fileSystemConfiguration)
    {
        string name = fileSystemConfiguration.Key;
        IFileSystem fileSystem = _fileSystems.GetOrAdd(name, _ =>
        {
            string factoryClass = fileSystemConfiguration.GetValue<string>("FileSystemFactoryClass");
            IFileSystemFactory factory = this.ServiceProvider.GetRequiredKeyedService<IFileSystemFactory>(factoryClass);
            IFileSystem fileSystem = factory.Create(fileSystemConfiguration);
            return new FileSystemWrapper(fileSystem);
        });
        return fileSystem;
    }
}