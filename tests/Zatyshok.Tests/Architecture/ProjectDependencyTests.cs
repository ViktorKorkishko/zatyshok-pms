using System.Xml.Linq;

namespace Zatyshok.Tests.Architecture;

/// <summary>
/// Dependencies between the projects of Zatyshok.sln point one way: Api and Worker →
/// Infrastructure → Domain (9.4.1). Read from the .csproj files, so the Worker, which the test
/// project does not reference, is checked too.
/// </summary>
public class ProjectDependencyTests
{
    public static TheoryData<string, string[]> AllowedProjectReferences => new()
    {
        { "Zatyshok.Domain", [] },
        { "Zatyshok.Infrastructure", ["Zatyshok.Domain"] },
        { "Zatyshok.Api", ["Zatyshok.Domain", "Zatyshok.Infrastructure"] },
        { "Zatyshok.Worker", ["Zatyshok.Domain", "Zatyshok.Infrastructure"] },
    };

    [Theory]
    [MemberData(nameof(AllowedProjectReferences))]
    public void ProjectReferencesPointTowardsDomain(string project, string[] allowed)
    {
        var references = LoadProject(project)
            .Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order(StringComparer.Ordinal);

        Assert.Equal(allowed, references);
    }

    [Fact]
    public void DomainHasNoPackageReferences()
    {
        // Not EF Core, not even abstractions: the domain model is plain C# (9.4.1).
        Assert.Empty(LoadProject("Zatyshok.Domain").Descendants("PackageReference"));
    }

    private static XDocument LoadProject(string project)
        => XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", project, $"{project}.csproj"));

    /// <summary>The folder with Zatyshok.sln, found upwards from the test output folder.</summary>
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Zatyshok.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Zatyshok.sln not found above " + AppContext.BaseDirectory);
    }
}
