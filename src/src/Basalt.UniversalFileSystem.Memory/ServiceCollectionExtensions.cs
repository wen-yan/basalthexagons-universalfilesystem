using Basalt.UniversalFileSystem.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Basalt.UniversalFileSystem.Memory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryFileSystem(this IServiceCollection services)
    {
        return services
            .AddKeyedSingleton<IFileSystemFactory, MemoryFileSystemFactory>(typeof(MemoryFileSystemFactory).FullName)
            .AddSingleton<IFileSystemFactory>(serviceProvider => serviceProvider.GetRequiredKeyedService<IFileSystemFactory>(typeof(MemoryFileSystemFactory).FullName));
    }
}