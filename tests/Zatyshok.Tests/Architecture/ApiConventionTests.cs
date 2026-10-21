using System.Reflection;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Zatyshok.Api.Common.Authorization;
using Zatyshok.Infrastructure.Persistence;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchUnitArchitecture = ArchUnitNET.Domain.Architecture;
using Type = System.Type;

namespace Zatyshok.Tests.Architecture;

/// <summary>
/// Conventions every controller of the API follows (9.4.1), so that a new controller of
/// a later stage cannot quietly break them:
/// <list type="bullet">
/// <item>a controller lives in Api/Modules/&lt;module&gt; and its route starts with api/v1/ (NFR-21);</item>
/// <item>every endpoint has the planned roles of table 6.2 (<see cref="AllowedRolesAttribute"/>), which T4 turns into checks;</item>
/// <item>controllers work through the services of their module: no DbContext, no EF Core;</item>
/// <item>endpoints take and return DTOs, never entities of the domain model.</item>
/// </list>
/// </summary>
public class ApiConventionTests
{
    private static readonly Assembly ApiAssembly = typeof(AllowedRolesAttribute).Assembly;

    private static readonly ArchUnitArchitecture Code = new ArchLoader()
        .LoadAssemblies(ApiAssembly, typeof(ZatyshokDbContext).Assembly, typeof(Zatyshok.Domain.Common.DomainException).Assembly)
        .Build();

    private static readonly Type[] Controllers = ApiAssembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
        .ToArray();

    /// <summary>
    /// Namespaces of modules that the shared code may not use: everything in Api/Modules, and a module of
    /// Domain or Infrastructure except its Contracts and Events ("…Modules.Rooms" and "…Modules.Rooms.X" match,
    /// "…Modules.Rooms.Contracts" does not).
    /// </summary>
    private const string ModuleInternals =
        @"^Zatyshok\.(Api\.Modules(\..+)?|(Domain|Infrastructure)\.Modules\.[A-Za-z]+(\.(?!(Contracts|Events)\b).+)?)$";

    private static readonly HashSet<string> KnownRoles = typeof(Roles)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet();

    public static TheoryData<string> Actions()
    {
        var data = new TheoryData<string>();
        foreach (var action in Controllers.SelectMany(ActionsOf))
        {
            data.Add($"{action.DeclaringType!.Name}.{action.Name}");
        }

        return data;
    }

    [Fact]
    public void ApiHasControllers()
    {
        // Control for the rules below: they are not checked on an empty set.
        Assert.True(Controllers.Length >= 6, $"found {Controllers.Length} controllers");
    }

    [Fact]
    public void ControllersLiveInModulesAndUseVersionedRoutes()
    {
        foreach (var controller in Controllers)
        {
            Assert.Matches(@"^Zatyshok\.Api\.Modules\.[A-Za-z]+$", controller.Namespace);
            Assert.NotNull(controller.GetCustomAttribute<ApiControllerAttribute>());
            Assert.StartsWith("api/v1/", controller.GetCustomAttribute<RouteAttribute>()?.Template);
        }
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public void EveryEndpointHasPlannedRolesOfTable6_2(string actionName)
    {
        var action = FindAction(actionName);
        var attribute = action.GetCustomAttribute<AllowedRolesAttribute>()
            ?? action.DeclaringType!.GetCustomAttribute<AllowedRolesAttribute>();

        Assert.NotNull(attribute);
        Assert.NotEmpty(attribute.Codes);
        Assert.All(attribute.Codes, code => Assert.Contains(code, KnownRoles));
        Assert.Equal(attribute.Codes.Count, attribute.Codes.Distinct().Count());
        if (attribute.Codes.Contains(Roles.Anonymous))
        {
            // "Anyone" together with named roles would hide a mistake.
            Assert.Single(attribute.Codes);
        }
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public void EndpointsTakeAndReturnDtosNotDomainEntities(string actionName)
    {
        var action = FindAction(actionName);
        var types = action.GetParameters().Select(p => p.ParameterType).Append(action.ReturnType);

        foreach (var type in types.SelectMany(Flatten))
        {
            Assert.False(
                type.Namespace?.StartsWith("Zatyshok.Domain.Modules", StringComparison.Ordinal) == true && !type.IsEnum,
                $"{actionName} exposes the entity {type.FullName}");
        }
    }

    [Fact]
    public void ControllersAndDtosDoNotUseTheDbContextOrEfCore()
    {
        var dependencies = Code.Types
            .Where(t => t.Namespace.FullName.StartsWith("Zatyshok.Api.Modules", StringComparison.Ordinal))
            .SelectMany(t => t.Dependencies.Select(d => (Origin: t, d.Target)))
            .ToList();

        var violations = dependencies
            .Where(d => d.Target.Namespace.FullName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                        || d.Target.FullName == typeof(ZatyshokDbContext).FullName)
            .Select(d => $"{d.Origin.FullName} → {d.Target.FullName}")
            .Distinct()
            .ToList();

        Assert.True(violations.Count == 0, "Controllers must use the services of their module:\n" + string.Join("\n", violations));

        // Control: the dependencies on the services are seen, so the scan is not empty.
        Assert.Contains(dependencies, d => d.Target.FullName == typeof(Zatyshok.Infrastructure.Modules.Rooms.RoomService).FullName);
    }

    [Fact]
    public void SharedApiCodeDoesNotDependOnModules()
    {
        // Api/Common may use a module of Domain or Infrastructure only through its Contracts and Events (rule 2),
        // like the modules themselves. Constants (Roles = RoleCodes of Identity.Contracts) are copied into
        // the IL by the compiler, so a "using" of a constant is not seen here; code review covers it.
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Zatyshok\.Api\.Common(\..+)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ModuleInternals)
            .Because("Api/Common is the infrastructure of every module, not a module (9.4.1)");

        rule.Check(Code);

        // Control: the pattern does match where module internals are used (controllers → services of their module).
        IArchRule control = Types().That().ResideInNamespaceMatching(@"^Zatyshok\.Api\.Modules(\..+)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ModuleInternals);
        Assert.False(control.HasNoViolations(Code));
    }

    private static IEnumerable<MethodInfo> ActionsOf(Type controller)
        => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static MethodInfo FindAction(string name)
        => Controllers.SelectMany(ActionsOf).Single(a => $"{a.DeclaringType!.Name}.{a.Name}" == name);

    /// <summary>The type and its generic arguments, recursively: Task&lt;PagedResponse&lt;RoomResponse&gt;&gt; → all three.</summary>
    private static IEnumerable<Type> Flatten(Type type)
        => type.IsGenericType ? type.GetGenericArguments().SelectMany(Flatten).Prepend(type) : [type];
}
