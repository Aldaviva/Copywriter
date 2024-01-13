using Bom.Squad;
using Copywriter;
using DiffPlex.Chunkers;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using McMaster.Extensions.CommandLineUtils;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

BomSquad.DefuseUtf8Bom();
Console.OutputEncoding = Encoding.UTF8; // make command prompt show "©" instead of "c"

Regex yearPattern = new(@"\b\d{4}\b", RegexOptions.RightToLeft);

int replacementCount = 0, fileCount = 0;

var optionsParser = new CommandLineApplication<Options> {
    UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.Throw,
    Description                  = "Update copyright years in project sources. Handles .NET SDK-style .csproj files and .NET AssemblyInfo.cs files."
};
optionsParser.Conventions.UseDefaultConventions();
optionsParser.ExtendedHelpText = $"""

                                  Examples:
                                    Update copyright year for the .csproj file in the current directory:
                                      {optionsParser.Name}
                                      
                                    Preview changes, but don't write them:
                                      {optionsParser.Name} --dry-run
                                  
                                    Update copyright year in the current directory and 2 levels of subdirectories:
                                      {optionsParser.Name} --max-depth 2
                                  
                                    Update all projects with a copyright owner of Ben Hutchison:
                                      {optionsParser.Name} -d 3 --include-name "Ben Hutchison" "C:\Users\Ben\Documents\Projects"
                                  """;
optionsParser.Parse(args);
Options options = optionsParser.Model;
if (optionsParser.OptionHelp?.HasValue() ?? false) {
    return;
}

CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, eventArgs) => {
    cts.Cancel();
    eventArgs.Cancel = true;
};

string enumerationRoot = Path.GetFullPath(options.parentDirectory ?? ".");
EnumerationOptions enumerationOptions = new() {
    MatchCasing           = MatchCasing.CaseInsensitive,
    RecurseSubdirectories = options.maxDepth > 0,
    MaxRecursionDepth     = options.maxDepth
};

try {
    await Task.WhenAll(new[] { "*.csproj", "AssemblyInfo.cs" }
        .SelectMany(pattern => Directory.EnumerateFiles(enumerationRoot, pattern, enumerationOptions))
        .Where(filename => !options.excludedSubdirectories.Any()
            || (Path.GetDirectoryName(filename)?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Intersect(options.excludedSubdirectories).Any() ?? true))
        .Select(handleFile));
} catch (OperationCanceledException) {
    return;
}

Console.WriteLine($"{(options.isDryRun ? "Would have made" : "Made")} {replacementCount:N0} replacements in {fileCount:N0} files.");

Task handleFile(string filename) {
    if (Path.GetExtension(filename).Equals(".csproj", StringComparison.OrdinalIgnoreCase)) {
        return handleCsprojFile(filename);
    } else if (Path.GetFileName(filename).Equals("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)) {
        return handleAssemblyInfoFile(filename);
    }

    return Task.CompletedTask;
}

async Task handleAssemblyInfoFile(string filename) {
    string oldFileText = await File.ReadAllTextAsync(filename, cts.Token);
    bool   fileChanged = false;

    string newFileText = Regex.Replace(oldFileText, """(?<prefix>\[\s*assembly\s*:\s*(?:System\s*\.\s*Reflection\s*\.\s*)?AssemblyCopyright\s*\(\s*")(?<attributeValue>.*?)(?<suffix>"\s*\)\s*\])""",
        match => {
            string oldAttributeValue = match.Groups["attributeValue"].Value;
            bool   editAllowed       = isAllowedToEdit(oldAttributeValue);
            string newAttributeValue = editAllowed ? replaceYear(oldAttributeValue) : oldAttributeValue;
            int    lineNumber        = oldFileText[..match.Index].Count(c => c == '\n') + 1;
            string replacement       = match.Groups["prefix"].Value + newAttributeValue + match.Groups["suffix"].Value;

            fileChanged |= editAllowed && !newAttributeValue.Equals(oldAttributeValue, StringComparison.Ordinal);

            if (fileChanged) {
                printDiff(filename, lineNumber, match.Value, replacement);
                replacementCount++;
            }

            return replacement;
        }, RegexOptions.Singleline);

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

        int    lineNumber        = ((IXmlLineInfo) copyrightEl).LineNumber;
        string newCopyrightValue = replaceYear(copyrightText.Value);

        if (!copyrightText.Value.Equals(newCopyrightValue, StringComparison.Ordinal)) {
            docChanged = true;
            replacementCount++;
            printDiff(filename, lineNumber, copyrightText.Value, newCopyrightValue);
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
                Indent             = false
            });
            await doc.SaveAsync(xmlWriter, cts.Token);
        }
    }
}

string replaceYear(string existingCopyrightLine) => yearPattern.Replace(existingCopyrightLine, (options.year ?? DateTime.Now.Year).ToString(), 1);

bool isAllowedToEdit(string copyrightLine) =>
    options.excludeCopyrightOwnerNames.All(excluded => !copyrightLine.Contains(excluded, StringComparison.CurrentCulture)) &&
    options.includeCopyrightOwnerNames.Any(included => copyrightLine.Contains(included, StringComparison.CurrentCulture));

[MethodImpl(MethodImplOptions.Synchronized)]
static void printDiff(string filename, int lineNumber, string oldText, string newText) {
    Console.WriteLine($"{filename}:{lineNumber:N0}");
    DiffPaneModel diffPaneModel = InlineDiffBuilder.Diff(oldText, newText, chunker: WordChunker.Instance);
    foreach (DiffPiece piece in diffPaneModel.Lines) {
        Color color = piece.Type switch {
            ChangeType.Deleted  => Color.Red,
            ChangeType.Inserted => Color.Green,
            _                   => Color.Gray
        };
        Colorful.Console.Write(piece.Text, color);
    }

    Console.WriteLine();
    Console.WriteLine();
}