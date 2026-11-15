using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Zatyshok.Domain.Common;
using Zatyshok.Infrastructure.Persistence;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchUnitArchitecture = ArchUnitNET.Domain.Architecture;

namespace Zatyshok.Tests.Architecture;

/// <summary>
/// Rules of module boundaries from 9.4.1 that can be checked on compiled code. ArchUnitNET reads
/// the IL of the assemblies, so a dependency counts even if it is only inside a method body.
/// <list type="bullet">
/// <item>Domain depends neither on EF Core nor on Infrastructure, Api or Worker.</item>
/// <item>Rule 2: a module uses another module only through its Contracts and Events namespaces.
/// Exception: an EF Core configuration may name another module's entity for a foreign key without
/// a navigation (<c>HasOne&lt;Room&gt;().WithMany()</c>). A controller in Api/Modules/&lt;M&gt; uses
/// only its own module (rule 4).</item>
/// <item>Rule 3: the dependencies between modules have no cycles (events excluded).</item>
/// </list>
/// Rule 1 (only the module's code reads its tables) is covered as far as types go: other modules'
/// entities are not reachable outside EF configurations. SQL text is checked in code review.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(DomainException).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(ZatyshokDbContext).Assembly;
    private static readonly System.Reflection.Assembly ApiAssembly = System.Reflection.Assembly.Load("Zatyshok.Api");

    private static readonly ArchUnitArchitecture Code = new ArchLoader()
        .LoadAssemblies(DomainAssembly, InfrastructureAssembly, ApiAssembly)
        .Build();

    // Modules with code in T2 (Domain/Modules/*); FrontDesk and Reporting get theirs in T6/T7.
    private static readonly string[] ExpectedModules =
        ["Audit", "Billing", "Bookings", "Guests", "Housekeeping", "Identity", "Notifications", "Rooms"];

    [Fact]
    public void DomainDependsNeitherOnEfCoreNorOnOuterProjects()
    {
        IArchRule rule = Types().That().ResideInAssembly(DomainAssembly)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^(Microsoft\.EntityFrameworkCore|Npgsql|Zatyshok\.Infrastructure|Zatyshok\.Api|Zatyshok\.Worker)(\..+)?$")
            .Because("the domain model has no dependencies (9.4.1)");

        rule.Check(Code);
    }

    /// <summary>Control: the same kind of rule does see EF Core where it is used, so the rule above is not empty.</summary>
    [Fact]
    public void EfCoreDependencyIsDetectedInInfrastructure()
    {
        IArchRule rule = Types().That().ResideInAssembly(InfrastructureAssembly)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore(\..+)?$");

        Assert.False(rule.HasNoViolations(Code));
    }

    [Fact]
    public void InfrastructureDoesNotDependOnApiOrWorker()
    {
        IArchRule rule = Types().That().ResideInAssembly(InfrastructureAssembly)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Zatyshok\.(Api|Worker)(\..+)?$");

        rule.Check(Code);
    }

    [Fact]
    public void SharedKernelDoesNotDependOnModules()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Zatyshok\.Domain\.Common(\..+)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Zatyshok\.Domain\.Modules(\..+)?$")
            .Because("modules depend on the shared kernel, not the other way round (listing 9.1)");

        rule.Check(Code);
    }

    [Fact]
    public void DomainHasTheExpectedModules()
    {
        var modules = Code.Types
            .Where(t => t.Assembly.FullName == DomainAssembly.FullName)
            .Select(t => ModuleOf(t)?.Module)
            .OfType<string>()
            .Distinct()
            .Order(StringComparer.Ordinal);

        Assert.Equal(ExpectedModules, modules);
    }

    [Fact]
    public void ModulesUseOtherModulesOnlyThroughContractsAndEvents()
    {
        var violations = new List<string>();
        var foreignKeyReferences = 0;
        var contractReferences = 0;

        foreach (var (origin, target) in CrossModuleDependencies())
        {
            var from = ModuleOf(origin)!.Value;
            var to = ModuleOf(target)!.Value;

            if (from.Layer != "Api" && to.Part is ("Contracts" or "Events"))
            {
                contractReferences++;
                continue;
            }

            if (from.Layer == "Infrastructure" && to.Layer == "Domain" && IsEfConfiguration(origin))
            {
                foreignKeyReferences++;
                continue;
            }

            violations.Add($"{origin.FullName} → {target.FullName}");
        }

        Assert.True(violations.Count == 0, "Forbidden dependencies between modules:\n" + string.Join("\n", violations));

        // Control: cross-module references are found at all (e.g. BookingConfiguration → Room), and so are
        // uses of a contract (AvailabilityService of Bookings → IRoomCatalog of Rooms, table 9.6).
        Assert.True(foreignKeyReferences > 0);
        Assert.True(contractReferences > 0);
    }

    [Fact]
    public void ModuleDependenciesHaveNoCycles()
    {
        // Events may be used in any direction: the publisher does not know its subscribers (rule 3).
        var graph = CrossModuleDependencies()
            .Where(d => ModuleOf(d.Target)!.Value.Part != "Events")
            .Select(d => (From: ModuleOf(d.Origin)!.Value.Module, To: ModuleOf(d.Target)!.Value.Module))
            .Distinct()
            .GroupBy(e => e.From)
            .ToDictionary(g => g.Key, g => g.Select(e => e.To).ToList());

        var cycle = FindCycle(graph);
        Assert.True(cycle is null, $"Cycle between modules: {string.Join(" → ", cycle ?? [])}");
    }

    /// <summary>"Zatyshok.Infrastructure.Modules.Bookings.Events" → ("Infrastructure", "Bookings", "Events"); null outside modules.</summary>
    private static (string Layer, string Module, string Part)? ModuleOf(IType type)
    {
        var parts = type.Namespace.FullName.Split('.');
        if (parts.Length < 4 || parts[0] != "Zatyshok" || parts[2] != "Modules")
        {
            return null;
        }

        return (parts[1], parts[3], parts.Length > 4 ? parts[4] : string.Empty);
    }

    /// <summary>Every dependency of a type in one module on a type in another module.</summary>
    private static IEnumerable<(IType Origin, IType Target)> CrossModuleDependencies()
        => Code.Types
            .Where(t => ModuleOf(t) is not null)
            .SelectMany(t => t.Dependencies.Select(d => (Origin: t, d.Target)))
            .Where(d => ModuleOf(d.Target) is { } to && to.Module != ModuleOf(d.Origin)!.Value.Module)
            .Distinct();

    /// <summary>An IEntityTypeConfiguration&lt;T&gt; class, or a compiler-generated class nested in one (lambdas).</summary>
    private static bool IsEfConfiguration(IType type)
    {
        var outerName = type.FullName.Split('+', '/')[0];
        var outer = Code.Types.Single(t => t.FullName == outerName);
        return outer.ImplementedInterfaces.Any(i =>
            i.Namespace.FullName == "Microsoft.EntityFrameworkCore" && i.Name.StartsWith("IEntityTypeConfiguration", StringComparison.Ordinal));
    }

    /// <summary>A cycle in a directed graph as a list of nodes (first = last), or null (depth-first search).</summary>
    private static List<string>? FindCycle(Dictionary<string, List<string>> graph)
    {
        var done = new HashSet<string>();
        var path = new List<string>();

        List<string>? Visit(string node)
        {
            var index = path.IndexOf(node);
            if (index >= 0)
            {
                return [.. path.Skip(index), node];
            }

            if (!done.Add(node))
            {
                return null;
            }

            path.Add(node);
            foreach (var next in graph.GetValueOrDefault(node) ?? [])
            {
                if (Visit(next) is { } cycle)
                {
                    return cycle;
                }
            }

            path.RemoveAt(path.Count - 1);
            return null;
        }

        return graph.Keys.Select(Visit).FirstOrDefault(c => c is not null);
    }
}
