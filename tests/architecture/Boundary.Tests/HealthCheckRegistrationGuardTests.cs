// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// rtuld0 (vts9ss follow-up) — anti-re-orphaning structural guard: every concrete <c>IHealthCheck</c>
/// implementation the framework ships MUST be referenced by at least one registration extension
/// (<c>*HealthChecksBuilderExtensions</c> / <c>*ServiceCollectionExtensions</c>) in its own module, so a
/// health check cannot silently lose its DI wiring and become advertised-yet-inert
/// (<c>enforce-invariants-structurally</c>). A deliberately consumer-opt-in health check (registered by
/// the app via <c>AddCheck&lt;T&gt;()</c>, not by a framework extension) is a reviewed allowlist entry with
/// a written reason — never a silent omission (<c>no-silent-caps</c>).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class HealthCheckRegistrationGuardTests
{
	/// <summary>
	/// Reviewed allowlist of <c>IHealthCheck</c> impls intentionally not referenced by a framework registration
	/// extension in their module, each with a written reason (<c>no-silent-caps</c> — an orphan is a reviewed
	/// entry, never a silent omission). Keyed by the impl's simple type name.
	/// <para>
	/// Intentionally EMPTY: every framework <c>IHealthCheck</c> impl is wired by a registration extension
	/// (bd-rtuld0 ruling — wire all, zero allowlist). A new orphan must be wired, not allowlisted; an entry here
	/// is a deliberate, justified exception, never the default escape hatch.
	/// </para>
	/// </summary>
	private static readonly IReadOnlyDictionary<string, string> KnownUnwiredAllowlist =
		new Dictionary<string, string>(StringComparer.Ordinal);

	[Fact]
	public void Every_framework_IHealthCheck_impl_is_registered_or_allowlisted()
	{
		var impls = FindHealthCheckImpls();

		// Sanity: the scan must actually find the known impls (guards against a broken scan = vacuous pass).
		impls.Count.ShouldBeGreaterThan(20,
			"the source scan should discover the framework's IHealthCheck implementations; a near-empty " +
			"result means the scan regex or repo-root resolution is broken (vacuous-guard protection).");

		var orphans = impls
			.Where(impl => !IsRegisteredInModule(impl) && !KnownUnwiredAllowlist.ContainsKey(impl.ClassName))
			.Select(impl => $"{impl.ClassName} ({impl.ProjectName})")
			.OrderBy(s => s, StringComparer.Ordinal)
			.ToList();

		orphans.ShouldBeEmpty(
			"every framework IHealthCheck impl must be referenced by a registration extension in its module " +
			"(or be a documented consumer-opt-in allowlist entry). Orphaned (advertised-yet-unwired) impls:\n  " +
			string.Join("\n  ", orphans));
	}

	[Fact]
	public void Allowlist_entries_still_exist_and_carry_a_reason()
	{
		var impls = FindHealthCheckImpls().Select(i => i.ClassName).ToHashSet(StringComparer.Ordinal);

		foreach (var (name, reason) in KnownUnwiredAllowlist)
		{
			impls.ShouldContain(name,
				$"allowlisted health check '{name}' no longer exists — remove the stale allowlist entry.");
			reason.ShouldNotBeNullOrWhiteSpace($"allowlist entry '{name}' must carry a written reason.");
		}
	}

	// ---- NFR-2 non-vacuity (deterministic, no source mutation) -------------------------------------------

	[Fact]
	public void Detector_flags_a_forced_orphan_impl_RED()
	{
		// A synthetic impl whose class name is referenced nowhere → must be classified orphan. This proves the
		// guard would go RED if any real impl were force-orphaned (lost its registration extension).
		var synthetic = new HealthCheckImpl(
			"Zzz_NonexistentHealthCheck_ForceOrphanControl",
			"Excalibur.Outbox",
			RepoRelativeProjectDir("src/Excalibur/Excalibur.Outbox"));

		IsRegisteredInModule(synthetic).ShouldBeFalse(
			"a class referenced by no registration extension in its module must be detected as an orphan " +
			"(if this is false the guard is vacuous and would never catch a real re-orphaning).");
	}

	[Fact]
	public void Detector_recognizes_a_known_wired_impl_GREEN()
	{
		// Positive control: RedisHealthCheck IS wired (RedisHealthChecksBuilderExtensions /
		// RedisProviderServiceCollectionExtensions) — the detector must classify it registered.
		var redis = FindHealthCheckImpls().SingleOrDefault(i => i.ClassName == "RedisHealthCheck");
		redis.ShouldNotBeNull("expected to discover RedisHealthCheck in the scan.");
		IsRegisteredInModule(redis!).ShouldBeTrue(
			"RedisHealthCheck is wired by a registration extension in Excalibur.Data.Redis; the detector must " +
			"see it (if false the detector under-reports and would false-RED real wired impls).");
	}

	// ---- detection -------------------------------------------------------------------------------------

	private sealed record HealthCheckImpl(string ClassName, string ProjectName, string ProjectDir);

	private static readonly Regex ImplDeclaration = new(
		@"\bclass\s+(?<name>[A-Za-z0-9_]+)\b[^;{]*:\s*[^={};]*\bIHealthCheck\b",
		RegexOptions.Compiled);

	private static readonly string[] RegistrationMarkers =
		["IHealthChecksBuilder", "IServiceCollection", "AddHealthChecks"];

	private static List<HealthCheckImpl> FindHealthCheckImpls()
	{
		var root = TestHelpers.GetRepositoryRoot();
		var srcDir = Path.Combine(root, "src");
		var result = new List<HealthCheckImpl>();

		foreach (var file in EnumerateSourceFiles(srcDir))
		{
			var text = File.ReadAllText(file);
			foreach (Match m in ImplDeclaration.Matches(text))
			{
				// Exclude abstract base classes — they are not registerable concrete impls.
				if (IsAbstractDeclaration(text, m.Index))
				{
					continue;
				}

				var projectDir = FindProjectDir(file);
				if (projectDir is null)
				{
					continue;
				}

				result.Add(new HealthCheckImpl(
					m.Groups["name"].Value,
					Path.GetFileName(projectDir),
					projectDir));
			}
		}

		// De-dupe by (class, project) — a class is declared once, but guard against partials.
		return result
			.GroupBy(i => (i.ClassName, i.ProjectName))
			.Select(g => g.First())
			.ToList();
	}

	private static bool IsRegisteredInModule(HealthCheckImpl impl)
	{
		var wordRef = new Regex($@"\b{Regex.Escape(impl.ClassName)}\b", RegexOptions.Compiled);

		foreach (var file in EnumerateSourceFiles(impl.ProjectDir))
		{
			var text = File.ReadAllText(file);

			// Skip the impl's own declaration file.
			if (ImplDeclaration.Matches(text).Any(m => m.Groups["name"].Value == impl.ClassName))
			{
				continue;
			}

			if (!RegistrationMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal)))
			{
				continue;
			}

			// A registration-extension file that references the impl by name (in a non-doc-comment line).
			foreach (var line in text.Split('\n'))
			{
				var trimmed = line.TrimStart();
				if (trimmed.StartsWith("///", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
				{
					continue;
				}

				if (wordRef.IsMatch(line))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static IEnumerable<string> EnumerateSourceFiles(string dir) =>
		Directory.Exists(dir)
			? Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
				.Where(p => !IsGenerated(p))
			: [];

	private static bool IsGenerated(string path)
	{
		var n = path.Replace('\\', '/');
		return n.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
			|| n.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsAbstractDeclaration(string text, int classMatchIndex)
	{
		// Look back to the start of the declaration line for an 'abstract' modifier.
		var lineStart = text.LastIndexOf('\n', classMatchIndex);
		var head = text.Substring(lineStart + 1, classMatchIndex - lineStart - 1);
		return head.Contains("abstract", StringComparison.Ordinal);
	}

	private static string? FindProjectDir(string filePath)
	{
		var dir = Path.GetDirectoryName(filePath);
		while (dir is not null)
		{
			if (Directory.EnumerateFiles(dir, "*.csproj").Any())
			{
				return dir;
			}

			dir = Path.GetDirectoryName(dir);
		}

		return null;
	}

	private static string RepoRelativeProjectDir(string relative) =>
		Path.Combine(TestHelpers.GetRepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
}
