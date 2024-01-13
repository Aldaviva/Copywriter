using McMaster.Extensions.CommandLineUtils;

namespace Copywriter;

public class Options {

    [Option("-n|--dry-run", "Preview but don't write changes to any files.", CommandOptionType.NoValue)]
    public bool isDryRun { get; set; }

    [Argument(0, "parentDirectory", "Directory in which to look for project files. Defaults to current working directory.")]
    public string? parentDirectory { get; set; }

    [Option("-d|--max-depth <N>", "Levels of recursion for subdirectories. Defaults to only using parentDirectory and not its subdirectories.", CommandOptionType.SingleValue)]
    public int maxDepth { get; set; }

    [Option("-y|--year <Y>", "New year to set. Defaults to current year.", CommandOptionType.SingleValue)]
    public int? year { get; set; }

    [Option("--exclude-dir <D>", "Subdirectories to ignore. Can be passed multiple times.", CommandOptionType.MultipleValue)]
    public IEnumerable<string> excludedSubdirectories { get; } = new List<string>();

    [Option("--exclude-name <N>", "If this name appears in the copyright line, don't update the year. Can be passed multiple times.", CommandOptionType.MultipleValue)]
    public IEnumerable<string> excludeCopyrightOwnerNames { get; } = new List<string>();

    [Option("--include-name <N>", "Only update the year if the copyright line contains one of these strings. Can be passed multiple times.", CommandOptionType.MultipleValue)]
    public IEnumerable<string> includeCopyrightOwnerNames { get; } = new List<string>();

    public override string ToString() =>
        $"{nameof(isDryRun)}: {isDryRun}, {nameof(parentDirectory)}: {parentDirectory}, {nameof(maxDepth)}: {maxDepth}, {nameof(year)}: {year}, {nameof(excludedSubdirectories)}: {string.Join(',', excludedSubdirectories)}, {nameof(excludeCopyrightOwnerNames)}: {string.Join(',', excludeCopyrightOwnerNames)}, {nameof(includeCopyrightOwnerNames)}: {string.Join(',', includeCopyrightOwnerNames)}";

}