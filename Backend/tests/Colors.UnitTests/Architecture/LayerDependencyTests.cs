using System.Xml.Linq;
using Colors.Application;
using Colors.Domain;

namespace Colors.UnitTests.Architecture;

/// <summary>
/// Guards the separation of concerns described in the specification, section 0.1.
///
/// The allowed direction is one way only:
///
///     Api  →  Infrastructure  →  Application  →  Domain
///
/// If someone adds a reference that points the wrong way, or pulls a database
/// or web package into a layer that must not know about it, these tests fail.
/// A rule nobody checks is not a rule.
///
/// Two kinds of check are needed:
///
///   * <see cref="ProjectReferences"/> reads the .csproj files. This catches a bad
///     reference the moment it is added, even before any code uses it — the compiler
///     drops unused project references from the built assembly, so checking the DLL
///     alone would let a wrong reference sit unnoticed until someone used it.
///
///   * The assembly checks catch NuGet packages arriving indirectly through another
///     package, which the .csproj files do not show.
/// </summary>
public class LayerDependencyTests
{
    // ---------- project references, read from the .csproj files ----------

    /// <summary>Which projects each layer is allowed to reference. Anything else fails.</summary>
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["Colors.Domain"] = [],
        ["Colors.Application"] = ["Colors.Domain"],
        ["Colors.Infrastructure"] = ["Colors.Application", "Colors.Domain"],
        ["Colors.Api"] = ["Colors.Application", "Colors.Infrastructure", "Colors.Domain"],
    };

    public static TheoryData<string> LayerNames()
    {
        var data = new TheoryData<string>();
        foreach (var layer in Allowed.Keys)
        {
            data.Add(layer);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LayerNames))]
    public void Layer_only_references_projects_it_is_allowed_to(string layer)
    {
        var actual = ProjectReferences(layer);
        var allowed = Allowed[layer];

        var forbidden = actual.Except(allowed).ToList();

        Assert.True(
            forbidden.Count == 0,
            $"{layer} references {string.Join(", ", forbidden)}, which breaks the layer direction. " +
            $"It may only reference: {(allowed.Length == 0 ? "nothing" : string.Join(", ", allowed))}.");
    }

    [Fact]
    public void Application_references_domain()
    {
        // The direction that IS allowed — proves these tests read real files.
        Assert.Contains("Colors.Domain", ProjectReferences("Colors.Application"));
    }

    // ---------- NuGet packages ----------

    /// <summary>
    /// Packages that must never appear in Domain or Application.
    /// Storage and transport are Infrastructure and Api concerns.
    /// </summary>
    private static readonly string[] ForbiddenPackagePrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.AspNetCore",
        "Swashbuckle",
    ];

    public static TheoryData<string, string> InnerLayersAndForbiddenPackages()
    {
        var data = new TheoryData<string, string>();
        foreach (var layer in new[] { "Colors.Domain", "Colors.Application" })
        {
            foreach (var package in ForbiddenPackagePrefixes)
            {
                data.Add(layer, package);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(InnerLayersAndForbiddenPackages))]
    public void Inner_layer_does_not_declare_database_or_web_packages(string layer, string forbiddenPrefix)
    {
        // Business rules must not know how data is stored, nor that HTTP exists.
        // Application declares interfaces; Infrastructure implements them.
        //
        // This reads the .csproj rather than the built assembly on purpose: an unused
        // package reference is stripped from assembly metadata, so a DLL check would
        // stay green until the day someone actually used it.
        var offenders = PackageReferences(layer)
            .Where(name => name.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{layer} declares the package(s) {string.Join(", ", offenders)}. " +
            $"This layer must not know about {forbiddenPrefix}.");
    }

    [Theory]
    [MemberData(nameof(InnerLayersAndForbiddenPackages))]
    public void Inner_layer_does_not_pull_in_database_or_web_packages_indirectly(string layer, string forbiddenPrefix)
    {
        // The .csproj check above cannot see a package arriving through another package.
        // This one reads the built assembly, which does show what was really linked in.
        var assembly = layer switch
        {
            "Colors.Domain" => DomainAssembly.Reference,
            "Colors.Application" => ApplicationAssembly.Reference,
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown layer."),
        };

        var offenders = assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{layer} links {string.Join(", ", offenders)} indirectly. " +
            $"This layer must not know about {forbiddenPrefix}.");
    }

    // ---------- helpers ----------

    private static IReadOnlyCollection<string> ProjectReferences(string projectName) =>
        Includes(projectName, "ProjectReference")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', Path.DirectorySeparatorChar)))
            .ToList();

    private static IReadOnlyCollection<string> PackageReferences(string projectName) =>
        Includes(projectName, "PackageReference").ToList();

    private static IEnumerable<string> Includes(string projectName, string elementName) =>
        XDocument.Load(FindProjectFile(projectName).FullName)
            .Descendants(elementName)
            .Select(e => (string?)e.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!);

    private static FileInfo FindProjectFile(string projectName)
    {
        var root = SolutionRoot();
        var file = root.GetFiles($"{projectName}.csproj", SearchOption.AllDirectories).FirstOrDefault();

        Assert.True(file is not null, $"Could not find {projectName}.csproj under {root.FullName}.");
        return file!;
    }

    private static DirectoryInfo SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && directory.GetFiles("Colors.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not find Colors.slnx walking up from the test output folder.");
        return directory!;
    }
}
