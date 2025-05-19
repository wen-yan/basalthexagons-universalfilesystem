using System;
using System.IO;
using System.Threading.Tasks;
using BasaltHexagons.CommandLine;
using BasaltHexagons.CommandLine.Annotations;
using BasaltHexagons.UniversalFileSystem.Cli.Commands.FileSystem;
using BasaltHexagons.UniversalFileSystem.Cli.Output;

namespace BasaltHexagons.UniversalFileSystem.Cli.Commands;

partial class CatCommandOptions
{
    [CliCommandSymbol(CliCommandSymbolType.Argument)]
    public Uri Uri { get; init; }
}

[CliCommandBuilder("cat", typeof(AppCommandBuilder))]
partial class CatCommandBuilder : CliCommandBuilder<CatCommand, CatCommandOptions>
{
    public CatCommandBuilder(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        this.Description = "cat";
        this.UriArgument = new("uri", "File uri");
    }
}

class CatCommand : FileSystemCommand<CatCommandOptions>
{
    public CatCommand(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public override async ValueTask ExecuteAsync()
    {
        await using Stream stream = await this.UniversalFileSystem.GetFileAsync(this.Options.Uri, this.CancellationToken);
        using StreamReader reader = new(stream, leaveOpen: true);
        while (true)
        {
            string? line = await reader.ReadLineAsync(this.CancellationToken);
            if (line == null) break;

            await this.OutputWriter.WriteLineAsync(line, this.CancellationToken);
        }
    }
}