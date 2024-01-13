<img src="https://raw.githubusercontent.com/Aldaviva/Copywriter/master/Copywriter/%C2%A9.ico" height="24" alt="©"> Copywriter
===

[![GitHub Actions](https://img.shields.io/github/actions/workflow/status/Aldaviva/Copywriter/dotnet.yml?branch=master&logo=github)](https://github.com/Aldaviva/Copywriter/actions/workflows/dotnet.yml)

Automatically update copyright years in project sources.

<!-- MarkdownTOC autolink="true" bracket="round" autoanchor="false" levels="1,2" -->

- [Quick Start](#quick-start)
- [Behavior](#behavior)
- [Supported files](#supported-files)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Usage](#usage)

<!-- /MarkdownTOC -->

## Quick Start
```ps1
Copywriter --max-depth 3
```

This will change all of the copyright years to the current year in supported files in the current working directory, as well as three levels of subdirectories.

## Behavior

For each supported file found in the given parent directory, all copyright strings will be searched for four-digit numbers. The **last occurrence** in each copyright string will be replaced with the new year. This means that ranges and lists should correctly update the latest year only. For example, in 2024 the following copyright string would only update the year 2023.

```diff
- © 1994-2009 Sun, 2010-2019 Oracle, 2019-2023 Apache
+ © 1994-2009 Sun, 2010-2019 Oracle, 2019-2024 Apache
```

The rest of the string will be left unmodified, including names, punctutation, and symbols like ©. This program does not rely on any formats in the copyright string besides a four-digit year appearing somewhere.

## Supported files

### .NET SDK-style project

.NET projects with the `<Project Sdk="Microsoft.NET.Sdk">` (or the `.Web` variant for ASP.NET Core) can use the `<Copyright>` property.

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Copyright>© 2023 $(Authors)</Copyright>
    </PropertyGroup>
</Project>
```

If the current year is 2024, you can use this program to automatically update the property to

```xml
<Copyright>© 2024 $(Authors)</Copyright>
```

### .NET AssemblyInfo file

.NET projects with the `AssemblyInfo.cs` file (such as .NET Framework projects built with an MSBuild-style project, or any .NET SDK-style project built with `<GenerateAssemblyInfo>` set to `false`) can use the `[assembly: AssemblyCopyright]` assembly-level attribute.

```cs
using System.Reflection;

[assembly: AssemblyCopyright("© 2023 Ben Hutchison")]
```

If the current year is 2024, you can use this program to automatically update the attribute to

```cs
[assembly: AssemblyCopyright("© 2024 Ben Hutchison")]
```

## Prerequisites
- [.NET Runtime 8 for Windows x64 or later](https://dotnet.microsoft.com/en-us/download)

## Installation
1. Download [`Copywriter.exe`](https://github.com/Aldaviva/Copywriter/releases/latest/download/Copywriter.exe) from the [latest release](https://github.com/Aldaviva/Copywriter/releases/latest).

## Usage

```ps1
Copywriter [options] <parentDirectory>
```

### `parentDirectory`
The directory in which to search for `.csproj` and `AssemblyInfo.cs` files.

Optional. If omitted, it uses the current working directory.

```ps1
# update files in current working directory
Copywriter

# update files in a specific directory
Copywriter "C:\Users\Ben\Documents\Projects\Copywriter\Copywriter"
```

### `-n`, `--dry-run`
Preview the changes that would be made, but don't actually save them.

```ps1
Copywriter -n
```

### `-d <N>`, `--max-depth <N>`
Recursively search for files in at most `<N>` levels of subdirectories.

Optional. If omitted, defaults to `0`, which only searches for files in the current working directory, but not in any of its subdirectories.

```ps1
Copywriter -d 3 "C:\Users\Ben\Documents\Projects"
```

### `-y <Y>`, `--year <Y>`
Set the copyright year to be this custom year, instead of the current year.

Useful if you're preparing a future release on December 31, for example.

```ps1
Copywriter -y 2024
```

### `--exclude-dir <D>`
Specify a directory name that should not be searched for files.

You can pass this multiple times to exclude multiple directories.

```ps1
Copywriter --exclude-dir "lib" --exclude-dir "vendor" --exclude-dir "thirdparty"
```

### `-exclude-name <N>`
Do not update the copyright text if this string appears within it.

You can pass this multiple times to skip copyright lines that contain **any** of the excluded strings.

```ps1
Copywriter --exclude-name "Microsoft"
```

### `-include-name <N>`
Only update the copyright text if this string appears within it.

You can pass this multiple times to skip copyright lines that do not contain **any** of the included strings.

```ps1
Copywriter --include-name "Ben Hutchison" --include-name "Benjamin Hutchison"
```
