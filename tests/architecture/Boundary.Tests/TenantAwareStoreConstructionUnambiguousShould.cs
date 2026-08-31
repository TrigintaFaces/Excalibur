// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.RegularExpressions;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

namespace Boundary.Tests;

/// <summary>
/// Structural guard on the constructing <c>AddTenantAwareStore</c> overload: every store registered through
/// it must be constructible deterministically.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> The constructing overload builds the store with
/// <c>ActivatorUtilities.CreateInstance(sp, tenantContext)</c>. Handing the context as an explicit argument
/// is what makes "built through a constructor that omits the ambient tenant" inexpressible on that path — but
/// it only identifies a single constructor while exactly one accepts an <see cref="ITenantContext"/>. Every
/// affected store also offers an advanced constructor taking a connection factory or a pre-built client, and
/// those accept the context too, so the argument stopped discriminating.
/// </para>
/// <para>
/// <b>Why it presented as an intermittent, mislabelled failure.</b> With two applicable constructors,
/// <c>ActivatorUtilities</c> first tries to pick the one whose remaining parameters are all registered
/// services. When that succeeds the store resolves and nothing looks wrong. When any one of those parameters
/// is absent, no constructor wins on that basis, selection falls back to matching on the supplied argument
/// alone, both constructors match, and the consumer is told
/// <c>"Multiple constructors accepting all given argument types have been found"</c> — a message naming the
/// constructors rather than the dependency that is actually missing. The same store therefore resolves in one
/// host and throws in another, and the exception points away from the cause.
/// </para>
/// <para>
/// <b>Scope, deliberately narrow.</b> The property under test is "is constructed BY THE SEAM", not "has two
/// tenant-aware constructors". Thirty-six shipped types have two or more such constructors; most are
/// registered through the FACTORY overload, where the call site constructs the store itself and ambiguity is
/// irrelevant. Asserting over constructor shape alone would fail on twenty-five types that have nothing wrong
/// with them. So the population comes from the call sites — which overload was used — and the verdict comes
/// from reflection on the type.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class TenantAwareStoreConstructionUnambiguousShould
{
    /// <summary>
    /// Matches a call to the CONSTRUCTING overload — an empty argument list — capturing the store type
    /// argument. Deliberately does not match the factory overload, whose ambiguity is harmless.
    /// </summary>
    private static readonly Regex ConstructingCallSite = new(
        @"AddTenantAwareStore\s*<\s*(?<contract>[\w.]+)\s*,\s*(?<store>[\w.]+)\s*>\s*\(\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// SAFETY ARM. Every store the seam constructs itself resolves to exactly one constructor.
    /// </summary>
    /// <remarks>
    /// RED before the fix for all eleven affected stores, and RED again the moment a new provider registers an
    /// ambiguous store through the constructing overload.
    /// </remarks>
    [Fact]
    public void EveryStoreTheSeamConstructs_HasAnUnambiguousConstructor()
    {
        var ambiguous = new List<string>();

        foreach (var storeType in ResolveConstructedStoreTypes().Values)
        {
            var tenantAware = TenantAwareConstructors(storeType);
            if (tenantAware.Length <= 1)
            {
                continue;
            }

            var preferred = tenantAware.Count(
                c => c.IsDefined(typeof(ActivatorUtilitiesConstructorAttribute), inherit: false));

            if (preferred != 1)
            {
                ambiguous.Add($"{storeType.FullName} ({tenantAware.Length} tenant-aware constructors, {preferred} marked)");
            }
        }

        ambiguous.ShouldBeEmpty(
            "These types are registered through the CONSTRUCTING AddTenantAwareStore overload, which builds "
            + "them by handing ActivatorUtilities the resolved ITenantContext. Each declares more than one "
            + "public constructor accepting an ITenantContext, so that argument no longer selects one of them, "
            + "and which constructor wins - or whether construction fails at all - depends on which unrelated "
            + "services the consumer's host happens to have registered. Mark the constructor intended for "
            + "dependency injection with [ActivatorUtilitiesConstructor], or register the store through the "
            + "factory overload and construct it explicitly. Ambiguous: " + string.Join("; ", ambiguous) + ".");
    }

    /// <summary>
    /// LIVENESS ARM. The scan actually finds the shipped call sites and resolves their store types.
    /// </summary>
    /// <remarks>
    /// Without this, a regex that matched nothing - or a type lookup that silently resolved nothing - would
    /// leave the safety arm iterating an empty set and passing over a repository full of the defect.
    /// </remarks>
    [Fact]
    public void TheScan_FindsTheShippedConstructingCallSites()
    {
        var resolved = ResolveConstructedStoreTypes();

        resolved.Count.ShouldBeGreaterThanOrEqualTo(
            15,
            "The constructing overload is used by roughly twenty distinct store types across the shipped "
            + "providers. Finding far fewer means the call-site regex or the type resolution stopped seeing "
            + "them, which would make the safety arm above vacuous.");

        resolved.ShouldContainKey(
            "PostgresSagaStore",
            "PostgresSagaStore is registered through the constructing overload by AddPostgresSagaStore. If the "
            + "scan cannot see it, it cannot see the defect this guard exists to catch.");
        resolved.ShouldContainKey(
            "OracleInboxStore",
            "OracleInboxStore is registered through the constructing overload by AddOracleInboxStore.");
    }

    /// <summary>
    /// GUARD SAFETY ARM. The seam refuses an ambiguous store at REGISTRATION, not on first resolve.
    /// </summary>
    /// <remarks>
    /// This is what moves the failure off the consumer's first injection and onto the <c>AddX</c> call, and it
    /// is what catches a future store the scan above cannot see - one registered from outside this repository.
    /// </remarks>
    [Fact]
    public void TheSeam_RejectsAnAmbiguousStore_WhileTheRegistrationIsBeingBuilt()
    {
        var services = new ServiceCollection();

        var thrown = Should.Throw<InvalidOperationException>(
            () => services.AddTenantAwareStore<IAmbiguityFixtureStore, AmbiguousFixtureStore>(),
            "A store with two constructors accepting an ITenantContext cannot be constructed deterministically "
            + "by the seam, so the registration itself must fail. Deferring this to first resolve puts the "
            + "failure in the consumer's application, with a message that names constructors instead of the "
            + "missing dependency.");

        thrown.Message.ShouldContain(
            "ActivatorUtilitiesConstructor",
            Case.Sensitive,
            "The message must name the fix, not merely report the ambiguity.");
    }

    /// <summary>
    /// GUARD LIVENESS ARM. The guard admits a correctly marked store and the seam still builds it.
    /// </summary>
    /// <remarks>
    /// A guard that rejected everything would satisfy the safety arm above perfectly. This arm resolves the
    /// store from a real container through the real registration, so a guard that over-rejects is RED.
    /// </remarks>
    [Fact]
    public void TheSeam_ConstructsAStore_WhoseInjectionConstructorIsMarked()
    {
        var services = new ServiceCollection();
        _ = services.AddDefaultTenantContext();

        _ = services.AddTenantAwareStore<IAmbiguityFixtureStore, MarkedFixtureStore>();

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<MarkedFixtureStore>();

        store.TenantContext.ShouldNotBeNull(
            "The seam hands the resolved ITenantContext to the marked constructor, so the store must have "
            + "received it.");
        store.BuiltByInjectionConstructor.ShouldBeTrue(
            "[ActivatorUtilitiesConstructor] marks the injection constructor, so selection must land on it and "
            + "not on the advanced constructor that takes a pre-built dependency.");
    }

    /// <summary>
    /// Maps simple type name to the resolved <see cref="Type"/> for every store registered through the
    /// constructing overload anywhere under <c>src/</c>.
    /// </summary>
    private static Dictionary<string, Type> ResolveConstructedStoreTypes()
    {
        var byName = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var simpleName in ScanConstructingCallSites())
        {
            if (byName.ContainsKey(simpleName))
            {
                continue;
            }

            var resolved = FindShippedType(simpleName);
            if (resolved is not null)
            {
                byName[simpleName] = resolved;
            }
        }

        return byName;
    }

    /// <summary>Simple names of every store type passed to the constructing overload under <c>src/</c>.</summary>
    private static HashSet<string> ScanConstructingCallSites()
    {
        var srcRoot = Path.Combine(RepositoryRoot(), "src");
        Directory.Exists(srcRoot).ShouldBeTrue($"Expected source root at '{srcRoot}'.");

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(path))
            {
                continue;
            }

            foreach (Match match in ConstructingCallSite.Matches(File.ReadAllText(path)))
            {
                var store = match.Groups["store"].Value;
                _ = names.Add(store[(store.LastIndexOf('.') + 1)..]);
            }
        }

        return names;
    }

    /// <summary>Public constructors of <paramref name="storeType"/> that accept an ambient tenant context.</summary>
    private static ConstructorInfo[] TenantAwareConstructors(Type storeType) =>
        Array.FindAll(
            storeType.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
            static c => Array.Exists(
                c.GetParameters(),
                static p => typeof(ITenantContext).IsAssignableFrom(p.ParameterType)));

    /// <summary>Locates a shipped type by simple name across the loaded framework assemblies.</summary>
    private static Type? FindShippedType(string simpleName)
    {
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "Excalibur.*.dll"))
        {
            Type?[] types;
            try
            {
                types = Assembly.LoadFrom(path).GetTypes();
            }
            catch (ReflectionTypeLoadException loadFailure)
            {
                types = loadFailure.Types;
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type is { IsAbstract: false, IsInterface: false } && string.Equals(type.Name, simpleName, StringComparison.Ordinal))
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static bool IsBuildOutput(string path)
    {
        var segments = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return Array.Exists(segments, s => string.Equals(s, "bin", StringComparison.Ordinal) || string.Equals(s, "obj", StringComparison.Ordinal));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("Could not locate the repository root from the test output directory.");
        return directory.FullName;
    }
}

/// <summary>Contract for the ambiguity fixtures below.</summary>
public interface IAmbiguityFixtureStore;

/// <summary>
/// Two public constructors accepting an <see cref="ITenantContext"/> and neither marked — the shape the seam
/// must refuse at registration.
/// </summary>
public sealed class AmbiguousFixtureStore : IAmbiguityFixtureStore
{
    /// <summary>Initializes a new instance intended for dependency injection.</summary>
    /// <param name="tenantContext">The ambient tenant context.</param>
    public AmbiguousFixtureStore(ITenantContext tenantContext) => TenantContext = tenantContext;

    /// <summary>Initializes a new instance from a pre-built dependency.</summary>
    /// <param name="dependency">A dependency the container cannot supply.</param>
    /// <param name="tenantContext">The ambient tenant context.</param>
    public AmbiguousFixtureStore(Func<int> dependency, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        TenantContext = tenantContext;
    }

    /// <summary>Gets the ambient tenant context the store was built with.</summary>
    public ITenantContext TenantContext { get; }
}

/// <summary>The same shape, with the injection constructor marked — the fix the seam requires.</summary>
public sealed class MarkedFixtureStore : IAmbiguityFixtureStore
{
    /// <summary>Initializes a new instance intended for dependency injection.</summary>
    /// <param name="tenantContext">The ambient tenant context.</param>
    [ActivatorUtilitiesConstructor]
    public MarkedFixtureStore(ITenantContext tenantContext)
    {
        TenantContext = tenantContext;
        BuiltByInjectionConstructor = true;
    }

    /// <summary>Initializes a new instance from a pre-built dependency.</summary>
    /// <param name="dependency">A dependency the container cannot supply.</param>
    /// <param name="tenantContext">The ambient tenant context.</param>
    public MarkedFixtureStore(Func<int> dependency, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        TenantContext = tenantContext;
    }

    /// <summary>Gets the ambient tenant context the store was built with.</summary>
    public ITenantContext TenantContext { get; }

    /// <summary>Gets a value indicating whether the marked injection constructor was selected.</summary>
    /// <value><see langword="true"/> when built through the marked constructor; otherwise, <see langword="false"/>.</value>
    public bool BuiltByInjectionConstructor { get; }
}
