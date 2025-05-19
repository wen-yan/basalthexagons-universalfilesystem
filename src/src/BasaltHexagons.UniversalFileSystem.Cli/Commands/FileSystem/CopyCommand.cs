using System;
using System.CommandLine;
using System.Threading.Tasks;
using BasaltHexagons.CommandLine;
using BasaltHexagons.CommandLine.Annotations;

namespace BasaltHexagons.UniversalFileSystem.Cli.Commands.FileSystem;

partial class CopyCommandOptions
{
    [CliCommandSymbol] public bool Overwrite { get; }

    [CliCommandSymbol(CliCommandSymbolType.Argument)]
    public Uri Source { get; }

    [CliCommandSymbol(CliCommandSymbolType.Argument)]
    public Uri Destination { get; }
}

[CliCommandBuilder("cp", typeof(AppCommandBuilder))]
partial class CopyCommandBuilder : CliCommandBuilder<CopyCommand, CopyCommandOptions>
{
    public CopyCommandBuilder(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        Description = "Copy files.";

        this.OverwriteOption = new(["--overwrite"], () => false, "Overwrite existing files if exist.")
        {
            IsRequired = false,
        };
        this.SourceArgument = new("Source file", "Source file uri.")
        {
            Arity = ArgumentArity.ExactlyOne,
        };
        this.DestinationArgument = new("Destination", "Destination uri.")
        {
            Arity = ArgumentArity.ExactlyOne,
        };
    }
}

class CopyCommand : FileSystemCommand<CopyCommandOptions>
{
    public CopyCommand(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public override async ValueTask ExecuteAsync()
    {
        await this.UniversalFileSystem.CopyFilesRecursivelyAsync(this.Options.Source, this.Options.Destination, this.Options.Overwrite, this.CancellationToken);
    }
}