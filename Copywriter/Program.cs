using Bom.Squad;
using Copywriter;
using DiffPlex.Chunkers;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using McMaster.Extensions.CommandLineUtils;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Unfucked;
using static System.ConsoleColor;
using static Unfucked.ConsoleControl;

BomSquad.DefuseUtf8Bom();
Console.OutputEncoding = Encoding.UTF8; // make command prompt show "©" instead of "c"

const StringComparison CASE_INSENSITIVE = StringComparison.OrdinalIgnoreCase;

int replacementCount = 0, fileCount = 0;
int currentYear      = DateTime.Now.Year;

var optionsParser = new CommandLineApplication<Options> {
    UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.Throw,
    Description                  = "Automatically update copyright years in project sources. Handles .NET SDK-style .csproj, .NET AssemblyInfo.cs, and .NET Directory.Build.props files."
};
optionsParser.VersionOptionFromAssemblyAttributes(Assembly.GetEntryAssembly()!);
optionsParser.Conventions.UseDefaultConventions();
optionsParser.ExtendedHelpText = $"""

    Examples:
      Update copyright year for files in the current directory:
        {optionsParser.Name}
        
      Preview changes, but don't write them:
        {optionsParser.Name} --dry-run

      Update copyright year in the current directory and 2 levels of subdirectories:
        {optionsParser.Name} --max-depth 2

      Update all projects with copyright owners of either Ben or $(Authors):
        {optionsParser.Name} -d 3 --include-name 'Ben' --include-name '$(Authors)' "C:\Users\Ben\Documents\Projects"
    """;
optionsParser.Parse(args);
if (optionsParser.IsShowingInformation) return;
Options options = optionsParser.Model;

using CancellationTokenSource cts = new CancellationTokenSource().CancelOnCtrlC();

string searchRoot = Path.GetFullPath(options.parentDirectory ?? ".");
EnumerationOptions searchOptions = new() {
    MatchCasing           = MatchCasing.CaseInsensitive,
    RecurseSubdirectories = options.maxDepth > 0,
    MaxRecursionDepth     = options.maxDepth
};

try {
    await Task.WhenAll(((IEnumerable<string>) ["*.csproj", "AssemblyInfo.cs", "Directory.Build.props"])
        .SelectMany(pattern => Directory.EnumerateFiles(searchRoot, pattern, searchOptions))
        .Where(filename => !options.excludedSubdirectories.Any()
            || (Path.GetDirectoryName(filename)?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Intersect(options.excludedSubdirectories).Any() ?? true))
        .Select(handleFile));
} catch (OperationCanceledException) {
    return;
}

Console.WriteLine($"{(options.isDryRun ? "Would have made" : "Made")} {replacementCount:N0} replacements in {fileCount:N0} files.");

Task handleFile(string filename) {
    if (Path.GetExtension(filename).Equals(".csproj", CASE_INSENSITIVE) || Path.GetFileName(filename).Equals("Directory.Build.props", CASE_INSENSITIVE)) {
        return handleCsprojFile(filename);
    } else if (Path.GetFileName(filename).Equals("AssemblyInfo.cs", CASE_INSENSITIVE)) {
        return handleAssemblyInfoFile(filename);
    }

    return Task.CompletedTask;
}

async Task handleAssemblyInfoFile(string filename) {
    string oldFileText = await File.ReadAllTextAsync(filename, cts.Token);
    bool   fileChanged = false;

    string newFileText = assemblyCopyrightPattern.Replace(oldFileText, match => {
        string oldAttributeValue = match.Value;
        bool   editAllowed       = isAllowedToEdit(oldAttributeValue);
        string replacement       = editAllowed ? replaceYear(oldAttributeValue) : oldAttributeValue;
        int    lineNumber        = oldFileText[..match.Index].Count(c => c == '\n') + 1;
        int    columnNumber      = match.Index - oldFileText[..match.Index].LastIndexOf('\n');

        fileChanged |= editAllowed && !replacement.Equals(oldAttributeValue, StringComparison.Ordinal);

        if (fileChanged) {
            printDiff(filename, lineNumber, columnNumber, match.Value, replacement);
            replacementCount++;
        }

        return replacement;
    });

    if (fileChanged) {
        fileCount++;
        if (!options.isDryRun) {
            await File.WriteAllTextAsync(filename, newFileText, cts.Token);
        }
    }
}

async Task handleCsprojFile(string filename) {
    cts.Token.ThrowIfCancellationRequested();

    XDocument doc          = XDocument.Load(filename, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
    bool      docHasProlog = doc.Declaration != null;
    bool      docChanged   = false;

    IEnumerable<XElement> copyrightEls = doc.Descendants("Copyright");

    foreach (XElement copyrightEl in copyrightEls) {
        if (copyrightEl.FirstNode is not XText copyrightText || !isAllowedToEdit(copyrightText.Value)) {
            continue;
        }

        IXmlLineInfo matchPosition     = copyrightText;
        int          lineNumber        = matchPosition.LineNumber;
        int          columnNumber      = matchPosition.LinePosition;
        string       newCopyrightValue = replaceYear(copyrightText.Value);

        if (!copyrightText.Value.Equals(newCopyrightValue, StringComparison.Ordinal)) {
            docChanged = true;
            replacementCount++;
            printDiff(filename, lineNumber, columnNumber, copyrightText.Value, newCopyrightValue);
        }

        copyrightText.Value = newCopyrightValue;
    }

    if (docChanged) {
        fileCount++;
        if (!options.isDryRun) {
            await using XmlWriter xmlWriter = XmlWriter.Create(filename, new XmlWriterSettings {
                OmitXmlDeclaration = !docHasProlog,
                CloseOutput        = true,
                Async              = true,
                Indent             = false // use existing indentation with LoadOptions.PreserveWhitespace when file was loaded
            });
            await doc.SaveAsync(xmlWriter, cts.Token);
        }
    }
}

string replaceYear(string existingCopyrightLine) => yearPattern.Replace(existingCopyrightLine, (options.year ?? currentYear).ToString(), 1);

bool isAllowedToEdit(string copyrightLine) =>
    options.excludeCopyrightOwnerNames.All(excluded => !copyrightLine.Contains(excluded, StringComparison.CurrentCulture)) &&
    (!options.includeCopyrightOwnerNames.Any() || options.includeCopyrightOwnerNames.Any(included => copyrightLine.Contains(included, StringComparison.CurrentCulture)));

[MethodImpl(MethodImplOptions.Synchronized)]
static void printDiff(string filename, int lineNumber, int columnNumber, string oldText, string newText) {
    Console.WriteLine($"{Color(filename, Blue)}:{Color(lineNumber.ToString("N0"), Cyan)}:{Color(columnNumber.ToString("N0"), Cyan)}");
    DiffPaneModel diffPaneModel = InlineDiffBuilder.Diff(oldText, newText, chunker: WordChunker.Instance);
    foreach (DiffPiece piece in diffPaneModel.Lines) {
        (ConsoleColor? foreground, ConsoleColor? background) color = piece.Type switch {
            ChangeType.Deleted  => (White, DarkRed),
            ChangeType.Inserted => (White, DarkGreen),
            _                   => (Gray, null)
        };
        Write(piece.Text, color.foreground, color.background);
    }

    Console.WriteLine();
    Console.WriteLine();
}

internal sealed partial class Program {

    [GeneratedRegex(@"\b\d{4}\b", RegexOptions.RightToLeft)]
    private static partial Regex yearPattern { get; }

    [GeneratedRegex("""(?<=\[\s*assembly\s*:\s*(?:System\s*\.\s*Reflection\s*\.\s*)?AssemblyCopyright\s*\(\s*")(.*?)(?="\s*\)\s*\])""", RegexOptions.Singleline)]
    private static partial Regex assemblyCopyrightPattern { get; }

}