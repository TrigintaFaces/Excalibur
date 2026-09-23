// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// Fails when a tenant term is composed into a string key anywhere under <c>src/</c> without going
/// through an approved injective composer.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because hand enumeration failed at this exact class three times.</b> Three separate
/// sweeps each concluded they had found every site; each was measured short afterwards, and the count
/// of independent encodings reached FIVE before a derived scan found them all — two of which advertised
/// injectivity in their own doc comments while leaving every term after the first bare-joined. A rule in
/// a document does not prevent the fourth author from writing the sixth; a scan whose population is
/// re-derived on every run does, because it does not depend on anyone remembering this happened.
/// </para>
/// <para>
/// <b>What it looks for.</b> An interpolated string that (a) contains an interpolation hole whose
/// expression names a tenant, and (b) places the separator next to a hole. That is the shape of a
/// composed key rather than a log line: a tenant term being joined to something else. A hit is
/// forgiven only when EVERY hole in the string is wrapped in an approved escaper, which is what makes
/// the composition injective.
/// </para>
/// <para>
/// <b>Why the tenant term and not every colon.</b> Scanning for every interpolated colon returns 147
/// occurrences across 87 files — ARNs, topic:partition:offset, log messages, doc prose. A population
/// that wide cannot be acted on, and a correction pass applied to it turns true statements false. The
/// tenant term is the discriminator because a tenant crossing a key boundary is the harm: cross-tenant
/// read, write, erasure, cache service and authorization.
/// </para>
/// <para>
/// <b>What it cannot see.</b> It reads <c>src/</c> as text, so a key assembled across several statements,
/// or composed in a consumer's own code, is outside it. That residual belongs to the provider conformance
/// arms. It is a closing condition on NEW instances, which is what it was asked to be.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class KeyCompositionRoutesThroughTheCanonicalComposerShould
{
	/// <summary>An interpolation hole: the expression between braces.</summary>
	private static readonly Regex Hole = new(@"\{([^{}]*)\}", RegexOptions.Compiled);

	/// <summary>A hole sitting immediately either side of the separator.</summary>
	private static readonly Regex SeparatorNextToHole = new(@"\}:|:\{", RegexOptions.Compiled);

	/// <summary>A hole naming a tenant term.</summary>
	private static readonly Regex TenantTerm = new("tenant", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	/// <summary>
	/// The composers that make a join injective. A hit whose every hole routes through one of these is
	/// composing correctly and is not a finding.
	/// </summary>
	/// <remarks>
	/// <c>SegmentedKey</c> is the canonical one. <c>FirestoreDocumentId.Escape</c> is deliberately separate
	/// and NOT a fourth scheme: Firestore document ids may not contain '/' and use '_' as the separator, so
	/// the canonical composer is not a drop-in for that store's alphabet. <c>Uri.EscapeDataString</c> is
	/// accepted where a store already persists keys in that encoding — converging it would rewrite stored
	/// identifiers rather than merely change how new ones are built.
	/// </remarks>
	private static readonly string[] ApprovedEscapers =
	[
		"SegmentedKey.Escape",
		"SegmentedKey.Compose",
		"EscapeSegment",
		"Uri.EscapeDataString",
		"FirestoreDocumentId.Escape",
	];

	/// <summary>
	/// Sites that compose a tenant term next to a separator and are NOT key compositions: a log tag, a
	/// diagnostic message, a literal prefix in front of a single term. Each is listed with its reason, so
	/// an entry cannot be added without saying why.
	/// </summary>
	/// <remarks>
	/// A baseline, not an allowlist of shame: every entry here has been read. The list is keyed by
	/// <c>path:line</c> deliberately — a moved or edited line stops matching and comes back as a finding
	/// to be re-read, which is the behaviour we want from a line that changed.
	/// </remarks>
	private static readonly Dictionary<string, string> Accepted = new(StringComparer.Ordinal)
	{
		["src/Dispatch/Excalibur.Dispatch/Middleware/Resilience/ThrottlingMiddleware.cs"] =
			"A rate-limit partition of the form 'tenant:{id}': one variable term behind a literal prefix, "
			+ "so there is no second term for it to shift across.",
		["src/Excalibur/Excalibur.A3.Core/Authorization/Grants/GrantKey.cs"] =
			"A diagnostic message describing the key FORMAT with escaped braces; it composes nothing.",
		["src/Excalibur/Excalibur.AuditLogging.Datadog/DatadogAuditExporter.cs"] =
			"A Datadog tag 'tenant:{id}', not a key: one variable term behind a literal prefix.",
		["src/Excalibur/Excalibur.Security/Encryption/DataProtectionMessageEncryptionService.cs"] =
			"A human-readable label in an additional-authenticated-data list, one term behind a literal.",
		["src/Excalibur/Excalibur.Compliance/Erasure/InMemoryDataInventoryStore.cs"] =
			"The tenant term is separated by a newline, not the key separator; the colon joins a table and "
			+ "a column name, which are schema identifiers and cannot contain one.",
	};

	private static readonly IReadOnlyList<Finding> Findings = Scan();

	// ---- CONTROL ------------------------------------------------------------------------------------

	/// <summary>
	/// The scan must find composed tenant keys that ARE routed correctly.
	/// </summary>
	/// <remarks>
	/// Without this, the arm below is satisfied by finding nothing — and finding nothing is exactly what a
	/// broken path, a renamed composer or a changed source layout produces. A search that can only return
	/// zero always passes.
	/// </remarks>
	[Fact]
	public void Find_the_canonical_composer_in_use_so_an_empty_scan_cannot_pass_every_arm()
	{
		var callSites = CanonicalComposerCallSites();

		callSites.ShouldBeGreaterThan(
			20,
			$"Only {callSites} call site(s) of the canonical composer found under src/. The consolidation "
			+ "left well over twenty. A number far below that means the scan no longer reaches the source "
			+ "tree, or the composer was renamed and this class is now searching for a dead name — either "
			+ "way the arm below has stopped testing anything, because a scan that can only return zero "
			+ "always passes. Point CanonicalComposers at the live name; do not leave it green over an "
			+ "empty set.");
	}

	/// <summary>The composer names whose presence proves the scan is reading a live source tree.</summary>
	private static readonly string[] CanonicalComposers =
	[
		"SegmentedKey.Compose",
		"SegmentedKey.Escape",
		"TenantScopedKey.Compose",
	];

	private static int CanonicalComposerCallSites()
	{
		var root = RepositoryRoot();
		var src = Path.Combine(root, "src");

		return !Directory.Exists(src)
			? 0
			: Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
				.SelectMany(File.ReadAllLines)
				.Count(line =>
					!line.TrimStart().StartsWith("///", StringComparison.Ordinal)
					&& Array.Exists(CanonicalComposers, c => line.Contains(c, StringComparison.Ordinal)));
	}

	/// <summary>
	/// The scan must be able to RECOGNISE a raw composition, proven against a constructed one rather than
	/// against whatever the tree happens to contain.
	/// </summary>
	[Fact]
	public void Reject_a_raw_composition_so_the_arm_below_is_known_to_be_able_to_fail()
	{
		const string Raw = """	var key = $"{tenantId}:{grantType}:{qualifier}";""";
		const string Routed = """	var key = SegmentedKey.Compose(tenantId, grantType, qualifier);""";

		IsUnroutedComposition(Raw).ShouldBeTrue(
			"the scan must flag a bare interpolated join of a tenant term; if it does not, every other arm "
			+ "in this class is vacuous.");

		IsUnroutedComposition(Routed).ShouldBeFalse(
			"the scan must not flag a composition that routes through the canonical composer, or the "
			+ "baseline would grow until the arm is switched off.");
	}

	// ---- THE ARM ------------------------------------------------------------------------------------

	/// <summary>
	/// No tenant term is composed into a key outside an approved composer.
	/// </summary>
	[Fact]
	public void Compose_every_tenant_key_through_an_approved_composer()
	{
		var unrouted = Findings
			.Where(f => !f.IsRouted && !Accepted.ContainsKey(f.Path))
			.ToList();

		unrouted.ShouldBeEmpty(
			"A tenant term joined to another term with a bare separator is not injective: neither term is "
			+ "validated against any charset, so one term can absorb the separator and two distinct "
			+ "identities address one key. Depending on what the key addresses that is a cross-tenant read, "
			+ "an overwrite, an irreversible erasure, one tenant served another's cached response, or one "
			+ "tenant's authorization grant answering for another's request.\n\n"
			+ "Route it through Excalibur.Dispatch.SegmentedKey.Compose. If the site is NOT a key — a log "
			+ "line, a tag, a diagnostic — add it to Accepted with the reason.\n\n"
			+ "Unrouted sites:\n"
			+ string.Join('\n', unrouted.Select(f => $"  {f.Path}:{f.Line}  {f.Text}")));
	}

	// ---- SCAN ---------------------------------------------------------------------------------------

	private static bool IsUnroutedComposition(string line)
	{
		var trimmed = line.TrimStart();

		if (trimmed.StartsWith("///", StringComparison.Ordinal)
			|| trimmed.StartsWith("//", StringComparison.Ordinal)
			|| !line.Contains("$\"", StringComparison.Ordinal))
		{
			return false;
		}

		var holes = Hole.Matches(line).Select(m => m.Groups[1].Value).ToList();

		if (holes.Count == 0
			|| !holes.Exists(h => TenantTerm.IsMatch(h))
			|| !SeparatorNextToHole.IsMatch(line))
		{
			return false;
		}

		// Routed only when EVERY hole is escaped. One bare hole beside an escaped one is the exact shape
		// that shipped: a self-delimiting tenant term followed by an ambiguous tail.
		return !holes.TrueForAll(
			h => Array.Exists(ApprovedEscapers, e => h.Contains(e, StringComparison.Ordinal)));
	}

	private static bool IsRoutedComposition(string line)
	{
		var holes = Hole.Matches(line).Select(m => m.Groups[1].Value).ToList();

		return line.Contains("$\"", StringComparison.Ordinal)
			&& holes.Count > 0
			&& holes.Exists(h => TenantTerm.IsMatch(h))
			&& SeparatorNextToHole.IsMatch(line)
			&& holes.TrueForAll(
				h => Array.Exists(ApprovedEscapers, e => h.Contains(e, StringComparison.Ordinal)));
	}

	private static IReadOnlyList<Finding> Scan()
	{
		var root = RepositoryRoot();
		var src = Path.Combine(root, "src");
		var findings = new List<Finding>();

		if (!Directory.Exists(src))
		{
			return findings;
		}

		foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
		{
			var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
			var lines = File.ReadAllLines(file);

			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];

				if (IsUnroutedComposition(line))
				{
					findings.Add(new Finding(relative, i + 1, line.Trim(), IsRouted: false));
				}
				else if (IsRoutedComposition(line))
				{
					findings.Add(new Finding(relative, i + 1, line.Trim(), IsRouted: true));
				}
			}
		}

		return findings;
	}

	private static string RepositoryRoot()
	{
		var dir = AppContext.BaseDirectory;

		while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
		{
			dir = Path.GetDirectoryName(dir);
		}

		return dir ?? AppContext.BaseDirectory;
	}

	private sealed record Finding(string Path, int Line, string Text, bool IsRouted);
}
