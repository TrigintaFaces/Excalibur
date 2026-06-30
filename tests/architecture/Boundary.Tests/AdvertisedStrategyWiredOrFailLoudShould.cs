// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

namespace Boundary.Tests;

/// <summary>
/// The "advertised implementation-selecting strategy" meta-guard (ADR-336 clause 2): every framework-shipped value of
/// every included strategy enum must be covered (implementor / documented-fallback / fail-loud guard / allowlist), per
/// the curated <see cref="AdvertisedStrategyRegistry"/>.
/// </summary>
/// <remarks>
/// This project carries no project references — it reasons over the BUILT framework assemblies by reflection
/// (<see cref="Assembly.LoadFrom(string)"/> against the located output DLLs). For each included enum it enumerates
/// <c>Enum.GetValues</c> and asserts each value has a registry coverage entry, and that the entry's evidence type
/// actually exists in the loaded assemblies (NFR-2 non-vacuity: deleting a guard/implementor type, or un-wiring a value
/// from the registry, makes this guard go RED naming the uncovered <c>(enum, value)</c>).
/// </remarks>
public sealed class AdvertisedStrategyWiredOrFailLoudShould
{
	private static readonly ConcurrentDictionary<string, Assembly?> AssemblyCache = new(StringComparer.OrdinalIgnoreCase);

	[Fact]
	public void Cover_every_framework_value_of_every_included_strategy_enum()
	{
		var uncovered = new List<string>();

		foreach (var entry in AdvertisedStrategyRegistry.Included)
		{
			var enumType = ResolveType(entry.AssemblyName, entry.EnumFullName);
			enumType.ShouldNotBeNull(
				$"Included enum {entry.EnumFullName} must exist in {entry.AssemblyName} (registry drift?)");
			enumType!.IsEnum.ShouldBeTrue($"{entry.EnumFullName} must be an enum");

			var coveredValues = entry.Coverage.Select(c => c.ValueName).ToHashSet(StringComparer.Ordinal);

			// EC-1: framework-shipped values only — Enum.GetNames reflects the declared (framework) members.
			foreach (var memberName in Enum.GetNames(enumType))
			{
				if (!coveredValues.Contains(memberName))
				{
					uncovered.Add($"{entry.EnumFullName}.{memberName}");
				}
			}
		}

		uncovered.ShouldBeEmpty(
			"Every advertised implementation-selecting enum value must be covered (implementor / documented-fallback / "
			+ "fail-loud guard / allowlist). Uncovered: " + string.Join(", ", uncovered));
	}

	[Fact]
	public void Confirm_each_coverage_evidence_type_exists_in_the_loaded_assemblies()
	{
		// NFR-2 non-vacuity: the registry's evidence types are real. Removing a guard/implementor type makes this RED.
		var missing = new List<string>();

		foreach (var entry in AdvertisedStrategyRegistry.Included)
		{
			foreach (var coverage in entry.Coverage)
			{
				if (coverage.Kind == AdvertisedStrategyRegistry.CoverageKind.Allowlist)
				{
					continue; // allowlist entries carry no evidence type (none today)
				}

				coverage.EvidenceTypeFullName.ShouldNotBeNullOrWhiteSpace(
					$"{entry.EnumFullName}.{coverage.ValueName} must name an evidence type");

				var evidence = ResolveType(coverage.EvidenceAssemblyName, coverage.EvidenceTypeFullName);
				if (evidence is null)
				{
					missing.Add($"{entry.EnumFullName}.{coverage.ValueName} -> {coverage.EvidenceTypeFullName} "
						+ $"({coverage.EvidenceAssemblyName})");
				}
			}
		}

		missing.ShouldBeEmpty(
			"Every coverage evidence type must exist in the built assemblies. Missing: " + string.Join("; ", missing));
	}

	[Fact]
	public void Carry_zero_allowlist_entries_today()
	{
		// An unwired value is a reviewed registry entry, never a silent allowlist omission.
		var allowlisted = AdvertisedStrategyRegistry.Included
			.SelectMany(e => e.Coverage.Select(c => (e.EnumFullName, c)))
			.Where(x => x.c.Kind == AdvertisedStrategyRegistry.CoverageKind.Allowlist)
			.Select(x => $"{x.EnumFullName}.{x.c.ValueName}")
			.ToList();

		allowlisted.ShouldBeEmpty("There must be zero allowlist entries today: " + string.Join(", ", allowlisted));
	}

	[Fact]
	public void Document_a_written_reason_for_every_excluded_enum()
	{
		// Each exclusion is a reviewed decision (selects no implementor), never a silent omission.
		foreach (var excluded in AdvertisedStrategyRegistry.Excluded)
		{
			excluded.Reason.ShouldNotBeNullOrWhiteSpace(
				$"Excluded enum {excluded.EnumFullName} must carry a written exclusion reason");
		}
	}

	[Fact]
	public void Classify_every_discovered_advertised_strategy_enum()
	{
		// EXHAUSTIVENESS (F2): the meta-guard only checks enums the registry already lists. A NEW or forgotten
		// advertised `*Strategy` enum would escape entirely (never Included, never Excluded). This closes the hole
		// structurally: discover every public `*Strategy` enum in the built framework assemblies and assert each is
		// classified (Included with coverage, or Excluded with a reason). Adding a `*Strategy` enum without
		// classifying it makes this guard RED — "advertised strategy enum is unclassified" becomes inexpressible.
		var classified = AdvertisedStrategyRegistry.Included.Select(e => e.EnumFullName)
			.Concat(AdvertisedStrategyRegistry.Excluded.Select(e => e.EnumFullName))
			.ToHashSet(StringComparer.Ordinal);

		var discovered = DiscoverFrameworkStrategyEnums();

		// Discovery must not silently find nothing (would make the guard vacuous): the curated registry already
		// names several `*Strategy` enums, so discovery must locate at least those.
		discovered.ShouldNotBeEmpty(
			"Discovery found no `*Strategy` enums in the built framework assemblies — discovery is broken/vacuous "
			+ "(assembly set or suffix predicate), not a real 'no strategies' result.");

		var unclassified = discovered
			.Where(d => !classified.Contains(d.FullName))
			.Select(d => $"{d.FullName} [{d.AssemblyName}]")
			.OrderBy(n => n, StringComparer.Ordinal)
			.ToList();

		unclassified.ShouldBeEmpty(
			"Every advertised `*Strategy` enum must be classified in AdvertisedStrategyRegistry (Included with coverage, "
			+ "or Excluded with a reason) — a new/forgotten one escapes the meta-guard. Unclassified: "
			+ string.Join(", ", unclassified));
	}

	// ---- reflection assembly/type resolution over built output ------------------------------------------------------

	/// <summary>
	/// Discovers every public enum whose simple name ends with <c>Strategy</c> across the built framework assemblies
	/// (<c>Excalibur*</c>). Deterministic: pinned to the framework assembly prefix + the <c>*Strategy</c> suffix so
	/// discovery itself cannot silently miss an advertised strategy enum.
	/// </summary>
	private static IReadOnlyList<(string FullName, string AssemblyName)> DiscoverFrameworkStrategyEnums()
	{
		var seen = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (var assembly in LoadAllFrameworkAssemblies())
		{
			var asmName = assembly.GetName().Name ?? "(unknown)";
			foreach (var type in SafeGetTypes(assembly))
			{
				if (type is { IsEnum: true, IsPublic: true }
					&& type.Name.EndsWith("Strategy", StringComparison.Ordinal)
					&& type.FullName is { } fullName)
				{
					seen[fullName] = asmName;
				}
			}
		}

		return [.. seen.Select(kvp => (kvp.Key, kvp.Value))];
	}

	/// <summary>
	/// Loads every built framework assembly (<c>Excalibur*.dll</c> under <c>src/**/bin</c>), one per simple name,
	/// preferring this test's build configuration + TFM. Mirrors <see cref="LocateAssemblyDll"/>'s selection.
	/// </summary>
	private static IReadOnlyList<Assembly> LoadAllFrameworkAssemblies()
	{
		var repoRoot = TestHelpers.GetRepositoryRoot();
		var baseDir = AppContext.BaseDirectory.Replace('\\', '/');
		var preferConfig = baseDir.Contains("/release/", StringComparison.OrdinalIgnoreCase) ? "/Release/" : "/Debug/";

		// One DLL per assembly simple name, preferring the matching config + TFM (avoids loading both Debug & Release).
		var byName = Directory
			.EnumerateFiles(Path.Combine(repoRoot, "src"), "Excalibur*.dll", SearchOption.AllDirectories)
			.Select(p => p.Replace('\\', '/'))
			.Where(p => p.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
			.GroupBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase)
			.Select(g => g
				.OrderByDescending(p => p.Contains(preferConfig, StringComparison.OrdinalIgnoreCase))
				.ThenByDescending(p => p.Contains("/net10.0/", StringComparison.OrdinalIgnoreCase))
				.First());

		var assemblies = new List<Assembly>();
		foreach (var dll in byName)
		{
			var simpleName = Path.GetFileNameWithoutExtension(dll);
			var assembly = AssemblyCache.GetOrAdd(simpleName, _ => TryLoad(dll));
			if (assembly is not null)
			{
				assemblies.Add(assembly);
			}
		}

		return assemblies;
	}

	private static Assembly? TryLoad(string dll)
	{
		var simpleName = Path.GetFileNameWithoutExtension(dll);
		var loaded = AppDomain.CurrentDomain.GetAssemblies()
			.FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
		if (loaded is not null)
		{
			return loaded;
		}

		try
		{
			return Assembly.LoadFrom(dll);
		}
		catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or IOException)
		{
			return null;
		}
	}

	private static Type? ResolveType(string assemblyName, string typeFullName)
	{
		var assembly = AssemblyCache.GetOrAdd(assemblyName, LoadFrameworkAssembly);
		if (assembly is null)
		{
			return null;
		}

		var exact = assembly.GetType(typeFullName, throwOnError: false);
		if (exact is not null)
		{
			return exact;
		}

		// Tolerate generic types: the registry names the representative type (e.g. EventSourcedRepository), while the
		// reflection full name carries arity (EventSourcedRepository`2). Match on full name sans the backtick arity.
		return SafeGetTypes(assembly).FirstOrDefault(t =>
			StripArity(t.FullName) == typeFullName);
	}

	private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
	{
		// LoadFrom does not eagerly resolve transitive dependencies, so GetTypes() can throw on a few unloadable
		// types — the loadable ones are still returned and are sufficient for an existence check.
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(t => t is not null)!;
		}
	}

	private static string? StripArity(string? fullName)
	{
		if (fullName is null)
		{
			return null;
		}

		var backtick = fullName.IndexOf('`', StringComparison.Ordinal);
		return backtick < 0 ? fullName : fullName[..backtick];
	}

	private static Assembly? LoadFrameworkAssembly(string assemblyName)
	{
		// Already loaded into the test AppDomain?
		var loaded = AppDomain.CurrentDomain.GetAssemblies()
			.FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
		if (loaded is not null)
		{
			return loaded;
		}

		var dll = LocateAssemblyDll(assemblyName);
		return dll is null ? null : Assembly.LoadFrom(dll);
	}

	private static string? LocateAssemblyDll(string assemblyName)
	{
		var repoRoot = TestHelpers.GetRepositoryRoot();
		var fileName = assemblyName + ".dll";

		// Prefer the configuration this test was built under (Release/Debug), and the test TFM, then any.
		var baseDir = AppContext.BaseDirectory.Replace('\\', '/');
		var preferConfig = baseDir.Contains("/release/", StringComparison.OrdinalIgnoreCase) ? "/Release/" : "/Debug/";

		var candidates = Directory
			.EnumerateFiles(Path.Combine(repoRoot, "src"), fileName, SearchOption.AllDirectories)
			.Where(p => p.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
			.ToList();

		if (candidates.Count == 0)
		{
			return null;
		}

		return candidates
			.OrderByDescending(p => p.Replace('\\', '/').Contains(preferConfig, StringComparison.OrdinalIgnoreCase))
			.ThenByDescending(p => p.Replace('\\', '/').Contains("/net10.0/", StringComparison.OrdinalIgnoreCase))
			.First();
	}
}
