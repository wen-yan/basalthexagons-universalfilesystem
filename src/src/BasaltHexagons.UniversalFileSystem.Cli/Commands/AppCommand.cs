using System;
using BasaltHexagons.CommandLine;
using BasaltHexagons.CommandLine.Annotations;

namespace BasaltHexagons.UniversalFileSystem.Cli.Commands;

partial class AppCommandOptions
{
}

[CliCommandBuilder(CliCommandBuilderAttribute.DefaultRootCommandName, null)]
partial class AppCommandBuilder : RootCliCommandBuilder<AppCommandOptions>
{
    public AppCommandBuilder(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        this.Description = "BasaltHexagons Universal File System Command Line Tool";
    }
}