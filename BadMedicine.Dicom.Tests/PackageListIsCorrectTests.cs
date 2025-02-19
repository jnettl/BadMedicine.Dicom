using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BadMedicine.Dicom.Tests;

/// <summary>
/// Tests to confirm that the dependencies in csproj files (NuGet packages) match those in the .nuspec files and that packages.md 
/// lists the correct versions (in documentation)
/// </summary>
public sealed class PackageListIsCorrectTests
{
    private static readonly EnumerationOptions EnumerationOptions = new() { RecurseSubdirectories = true,MatchCasing = MatchCasing.CaseInsensitive,IgnoreInaccessible = true};

    //<PackageReference Include="NUnit3TestAdapter" Version="3.13.0" />
    private static readonly Regex RPackageRef = new(@"<PackageReference\s+Include=""(.*)""\s+Version=""([^""]*)""", RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);

    // | Org.SomePackage |
    //
    private static readonly Regex RMarkdownEntry = new(@"^\|\s*\[?([^ |\]]+)(\]\([^)]+\))?\s*\|", RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);


    /// <summary>
    /// Enumerate non-test packages, check that they are listed in PACKAGES.md
    /// </summary>
    /// <param name="rootPath"></param>
    [TestCase]
    public void TestPackagesDocumentCorrect(string rootPath=null)
    {
        var root= FindRoot(rootPath);
        var undocumented = new StringBuilder();

        // Extract the named packages from PACKAGES.md
        var packagesMarkdown = File.ReadAllLines(GetPackagesMarkdown(root))
            .Select(static line => RMarkdownEntry.Match(line))
            .Where(static m=>m.Success)
            .Select(static m => m.Groups[1].Value)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

        // Extract the named packages from csproj files, then subtract those listed in PACKAGES.md (should be empty)
        var undocumentedPackages = GetCsprojFiles(root).Select(File.ReadAllText)
            .SelectMany(static s => RPackageRef.Matches(s))
            .Select(static m=>m.Groups[1].Value).Except(packagesMarkdown).Select(BuildRecommendedMarkdownLine);
        undocumented.AppendJoin(Environment.NewLine, undocumentedPackages);

        Assert.That(undocumented.ToString(), Is.Empty);
    }

    /// <summary>
    /// Generate the report entry for an undocumented package
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    private static object BuildRecommendedMarkdownLine(string package) =>
        $"Package {package} is not documented in PACKAGES.md. Recommended line is:\r\n| {package} | [GitHub]() | LICENCE GOES HERE | |";

    /// <summary>
    /// Find the root of this repo, which is usually the directory containing the .sln file
    /// If the .sln file lives elsewhere, you can override this by passing in a path explicitly.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static DirectoryInfo FindRoot(string path = null)
    {
        if (path != null)
        {
            if (!Path.IsPathRooted(path)) path = Path.Combine(TestContext.CurrentContext.TestDirectory, path);
            return new DirectoryInfo(path);
        }
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (!root.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly).Any() && root.Parent != null)
            root = root.Parent;
        Assert.That(root.Parent, Is.Not.Null, "Could not find root of repository");
        return root;
    }

    /// <summary>
    /// Returns all csproj files in the repository, except those containing the string 'tests'
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    private static IEnumerable<string> GetCsprojFiles(DirectoryInfo root)
    {
        return root.EnumerateFiles("*.csproj", EnumerationOptions).Select(static f => f.FullName)
            .Where(static f => !f.Contains("tests", StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    /// Find the sole packages.md file wherever in the repo it lives. Error if multiple or none.
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    private static string GetPackagesMarkdown(DirectoryInfo root)
    {
        var path = root.EnumerateFiles("packages.md", EnumerationOptions).Select(static f => f.FullName)
            .SingleOrDefault();
        Assert.That(path, Is.Not.Null, "Could not find packages.md");
        return path;
    }

}
