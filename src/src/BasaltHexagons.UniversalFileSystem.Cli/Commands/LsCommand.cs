using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using BasaltHexagons.CommandLine;
using BasaltHexagons.CommandLine.Annotations;
using BasaltHexagons.UniversalFileSystem.Cli.Commands.FileSystem;
using BasaltHexagons.UniversalFileSystem.Cli.Output;
using BasaltHexagons.UniversalFileSystem.Core;

namespace BasaltHexagons.UniversalFileSystem.Cli.Commands;

partial class LsCommandOptions
{
    [CliCommandSymbol]
    public bool Recursive { get; init; }

    [CliCommandSymbol(CliCommandSymbolType.Argument)]
    public Uri Directory { get; init; }
}

[CliCommandBuilder("ls", typeof(AppCommandBuilder))]
partial class LsCommandBuilder : CliCommandBuilder<LsCommand, LsCommandOptions>
{
    public LsCommandBuilder(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        this.Description = "ls";

        this.RecursiveOption = new(["--recursive", "-r"], () => false, "Include subdirectories, default is false");
        this.DirectoryArgument = new("directory", "Directory");
    }
}

class LsCommandOutput
{
    public Uri? Uri { get; set; }
    public ObjectType ObjectType { get; set; }
    public DateTime? LastModifiedTimeUtc { get; set; }

    [TabularDatasetWriter(Alignment = TabularDatasetWriterAlignment.Right)]
    public long? ContentSize { get; set; }
}

class LsCommand : FileSystemCommand<LsCommandOptions>
{
    public LsCommand(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public override async ValueTask ExecuteAsync()
    {
        IAsyncEnumerable<LsCommandOutput> results = this.UniversalFileSystem
            .ListObjectsAsync(this.Options.Directory, this.Options.Recursive, this.CancellationToken)
            .Select(metadata => new LsCommandOutput
            {
                Uri = metadata.Uri,
                ObjectType = metadata.ObjectType,
                LastModifiedTimeUtc = metadata.LastModifiedTimeUtc,
                ContentSize = metadata.ContentSize
            });

        await this.OutputWriter.WriteDatasetAsync(results, this.CancellationToken);
    }
}