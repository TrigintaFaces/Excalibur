using System.Xml.Linq;

namespace Boundary.Tests;

/// <summary>
/// Binds the rule that a migration script a consumer may have to run by hand is actually IN the package.
/// </summary>
/// <remarks>
/// <para>
/// Several subsystems reconcile their own schema at runtime, but that path is only reachable by a host
/// that lets the package create tables in its own database. A deployment whose schema is owned centrally —
/// provisioned by a migration tool, or reviewed before the application ever connects — never reaches it,
/// and for that deployment re-running a create script is a no-op that leaves the old shape in place. Those
/// deployments need the numbered scripts, so the scripts have to ship.
/// </para>
/// <para>
/// The failure this prevents is not a build break. A project that omits the pack item compiles, tests,
/// publishes and installs perfectly; the scripts are simply absent from the consumer's package, and the
/// only symptom appears on their database. One event-store package shipped that way while its own
/// architecture document named the missing script as the remedy — the guarantee and the package had
/// drifted apart, and nothing could notice.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class ScriptPackagingShould
{
	/// <summary>
	/// Projects that carry <c>Scripts/*.sql</c> and deliberately do NOT pack them, with the reason.
	/// </summary>
	/// <remarks>
	/// Each of these provisions its schema in code on every start, so the script is a development artifact
	/// rather than something a consumer is ever asked to run. The entries are asserted in BOTH directions
	/// below: an exemption that no longer applies fails, so this list cannot quietly rot into a place where
	/// omissions are parked.
	/// </remarks>
	/// Empty: the three projects once listed here (Cdc.Postgres, LeaderElection.Postgres,
	/// LeaderElection.SqlServer) now pack their scripts, so the liveness arm below retired them.
	private static readonly Dictionary<string, string> SelfProvisioning = new(StringComparer.Ordinal);

	private readonly string _repoRoot = TestHelpers.GetRepositoryRoot();

	private sealed record ScriptProject(string Name, string CsprojPath, int ScriptCount, bool PacksScripts);

	/// <summary>
	/// SAFETY. Every packable project carrying migration scripts puts them in its package.
	/// </summary>
	[Fact]
	public void PackEveryMigrationScriptAConsumerMayNeedToRunByHand()
	{
		var offenders = Discover()
			.Where(p => !p.PacksScripts && !SelfProvisioning.ContainsKey(p.Name))
			.Select(p => $"{p.Name} carries {p.ScriptCount} script(s) under Scripts/ and packs none of them")
			.ToList();

		offenders.ShouldBeEmpty(
			"a consumer whose schema is managed centrally cannot obtain these scripts, so they cannot apply "
			+ "the change the package's own documentation tells them to apply. Add "
			+ "<None Include=\"Scripts\\*.sql\" Pack=\"true\" PackagePath=\"scripts\\\" /> to each project "
			+ "listed, or — if it provisions in code and no consumer ever runs the script by hand — add it to "
			+ $"{nameof(SelfProvisioning)} with the reason.{Environment.NewLine}"
			+ string.Join(Environment.NewLine, offenders));
	}

	/// <summary>
	/// LIVENESS. The enumeration actually finds the projects it is meant to police.
	/// </summary>
	/// <remarks>
	/// Without this, the arm above passes perfectly when the discovery predicate breaks — a renamed folder, a
	/// changed glob, a path separator — and an empty offender list is indistinguishable from a clean tree.
	/// That is the shape this suite exists to catch, so it must not be the shape this suite has.
	/// </remarks>
	[Fact]
	public void FindTheScriptCarryingProjectsAtAll()
	{
		var discovered = Discover();

		discovered.Count.ShouldBeGreaterThanOrEqualTo(
			20,
			"the discovery found almost nothing, so the packing assertion is passing vacuously rather than "
			+ $"because the tree is clean (found {discovered.Count}).");

		discovered.ShouldContain(
			p => p.Name == "Excalibur.EventSourcing.Postgres" && p.PacksScripts,
			"a project known to pack its scripts was not detected as packing them, so the detector cannot "
			+ "tell the two states apart and every result above is meaningless.");
	}

	/// <summary>
	/// SAFETY. An exemption that no longer applies is removed rather than left standing.
	/// </summary>
	/// <remarks>
	/// A list of permitted omissions is only honest while every entry is still an omission. An entry naming a
	/// project that has since been deleted, renamed, stripped of its scripts, or fixed to pack them is a
	/// standing permission for something that is not happening — and the next real offender can be waved
	/// through by inheriting it.
	/// </remarks>
	[Fact]
	public void KeepTheSelfProvisioningExemptionsHonest()
	{
		var discovered = Discover().ToDictionary(p => p.Name, StringComparer.Ordinal);

		var stale = new List<string>();
		foreach (var (name, reason) in SelfProvisioning)
		{
			if (!discovered.TryGetValue(name, out var project))
			{
				stale.Add($"{name} is exempt ({reason}) but no longer carries Scripts/*.sql — remove the entry");
				continue;
			}

			if (project.PacksScripts)
			{
				stale.Add($"{name} is exempt ({reason}) but now packs its scripts — remove the entry");
			}
		}

		stale.ShouldBeEmpty(string.Join(Environment.NewLine, stale));
	}

	/// <summary>
	/// Enumerates packable projects under <c>src/</c> that carry at least one <c>Scripts/*.sql</c>, recording
	/// whether the project packs them.
	/// </summary>
	private IReadOnlyList<ScriptProject> Discover()
	{
		var projects = new List<ScriptProject>();

		foreach (var csproj in TestHelpers.GetCsprojFiles(_repoRoot, "src"))
		{
			var scriptsDir = Path.Combine(Path.GetDirectoryName(csproj)!, "Scripts");
			if (!Directory.Exists(scriptsDir))
			{
				continue;
			}

			var scripts = Directory.GetFiles(scriptsDir, "*.sql", SearchOption.TopDirectoryOnly);
			if (scripts.Length == 0)
			{
				continue;
			}

			var document = XDocument.Load(csproj);

			// A non-packable project produces no NuGet at all, so "its scripts are missing from the package"
			// is not a statement about it.
			var packable = document.Descendants("IsPackable").Select(e => e.Value).FirstOrDefault();
			if (string.Equals(packable?.Trim(), "false", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			projects.Add(new ScriptProject(
				Path.GetFileNameWithoutExtension(csproj),
				csproj,
				scripts.Length,
				PacksScripts(document)));
		}

		return projects;
	}

	/// <summary>
	/// Whether the project declares a pack item covering <c>Scripts\*.sql</c>.
	/// </summary>
	/// <remarks>
	/// Matched on the Include glob rather than on the literal element text, so a project that packs its
	/// scripts as <c>Content</c> instead of <c>None</c>, or orders its attributes differently, still counts.
	/// The predicate that matters is "does a pack item cover these files", not "does this file contain a
	/// particular string".
	/// </remarks>
	private static bool PacksScripts(XDocument document) =>
		document.Descendants()
			.Where(e => e.Name.LocalName is "None" or "Content")
			.Any(e =>
				(e.Attribute("Include")?.Value ?? string.Empty)
					.Replace('/', '\\')
					.Contains("Scripts\\", StringComparison.OrdinalIgnoreCase)
				&& string.Equals(e.Attribute("Pack")?.Value?.Trim(), "true", StringComparison.OrdinalIgnoreCase));
}
