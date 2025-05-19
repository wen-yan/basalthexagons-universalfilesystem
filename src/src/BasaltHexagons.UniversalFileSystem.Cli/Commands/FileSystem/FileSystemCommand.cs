using System;
using Microsoft.Extensions.DependencyInjection;

namespace BasaltHexagons.UniversalFileSystem.Cli.Commands.FileSystem;

abstract class FileSystemCommand<TOptions> : CommandBase<TOptions>
{
    protected FileSystemCommand(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        this.UniversalFileSystem = serviceProvider.GetRequiredService<IUniversalFileSystem>();
    }

    protected IUniversalFileSystem UniversalFileSystem { get; }
}