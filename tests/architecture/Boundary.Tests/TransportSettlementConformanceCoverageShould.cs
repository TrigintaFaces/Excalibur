// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests;

/// <summary>
/// Structural guard that a suite deriving the transport receiver conformance kit invokes every arm the
/// kit declares, or records the gap where a reviewer reads it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks.</b> <c>TransportReceiverConformanceTests</c> is a library of callable arms,
/// not a discoverable suite: every arm is <c>protected</c>, takes transport-specific closures, and carries
/// no test attribute. An arm runs only where a deriving suite hand-writes a call to it. A transport that
/// calls one of the kit's arms therefore reports a pass and says nothing whatever about the others — an
/// uncalled arm is indistinguishable, in every runner, from an arm that ran and passed. Measured on this
/// repository when this guard was written: of the eight transports with a settlement suite, <b>one</b>
/// (RabbitMQ) called all six settlement arms and one (Pulsar) called one of six.
/// </para>
/// <para>
/// <b>Why the sibling guard does not already cover this.</b>
/// <see cref="ConformanceSuiteWiresEveryArmShould"/> asks whether a suite deriving a
/// <c>*ConformanceTestKit</c> wires that kit's own <c>ConformanceSuite_ShouldWireEveryArm</c> member. Both
/// halves miss here. The transport kit's name does not end in <c>ConformanceTestKit</c>, and the kit-side
/// check it delegates to enumerates <i>public, virtual, parameterless</i> arms — a shape no arm on this kit
/// has, because each takes the transport's own settlement closures. So that check cannot be reused: it
/// would enumerate nothing and pass every suite it was pointed at, which is the defect it exists to detect.
/// The obligation is therefore asked here, of the call sites, rather than delegated to the kit.
/// </para>
/// <para>
/// <b>Why the arm list is read from the kit rather than written here.</b> A hardcoded list measures the
/// moment it was typed. Reading the kit's own source means a seventh arm added to the kit obligates every
/// suite on the next run, and cannot arrive uncalled and unnoticed.
/// </para>
/// <para>
/// <b>What this does NOT establish.</b> That a called arm asserts anything useful for that transport, or
/// that the six settlement arms are the right six. This is a coverage ratchet over call sites: it reports
/// that an arm is never reached, never that reaching it was sufficient. The substrate question — that a
/// conformance kit should drive its own arms rather than let each consumer hand-pick — is real and is
/// deliberately not answered here; see the baseline header.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class TransportSettlementConformanceCoverageShould
{
	/// <summary>The conformance kit whose arms every deriving suite owes a call to.</summary>
	private const string KitName = "TransportReceiverConformanceTests";

	/// <summary>
	/// Repository-relative path of the kit partial declaring the settlement arms.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Scoped to this partial on purpose, and the discriminator is structural rather than a taste
	/// judgement.</b> The kit is split across two files: this one declares the six settlement arms, and
	/// <c>TransportReceiverConformanceTests.cs</c> declares fourteen generic receive/settle arms exercised
	/// by the kit's own reference suite. Obliging a settlement suite to call the generic fourteen would add
	/// roughly 140 baseline entries that are not defects, and bury the seven that are — a count whose
	/// denominator says nothing. Reading the arm list from this file means a seventh settlement arm
	/// obligates every suite automatically, while nothing here is hand-listed.
	/// </para>
	/// <para>
	/// <b>The generic fourteen are consequently NOT covered by this guard, and that gap is real.</b> Only
	/// <c>InMemoryTransportReceiverConformanceShould</c> calls them, so a provider receiver can diverge on
	/// basic receive and acknowledge semantics without any arm going red. That is a separate obligation
	/// with a separate population, deliberately not folded in here where it would be invisible.
	/// </para>
	/// </remarks>
	private const string KitSourcePath =
		"src/Dispatch/Excalibur.Dispatch.Testing/Transport/Conformance/TransportReceiverSettlementConformance.cs";

	/// <summary>Repository-relative path of the shrink-only baseline.</summary>
	private const string BaselineRelativePath =
		"tests/architecture/Boundary.Tests/transport-settlement-coverage-baseline.txt";

	// ---------- the injected-data core ----------

	/// <summary>One arm a suite derives the kit without ever calling.</summary>
	internal sealed record ArmDebt(string Identity, string SourcePath);

	/// <summary>
	/// Returns the arm names the kit declares, read from the kit's own source.
	/// </summary>
	/// <param name="kitSources">The kit's partial declarations. Supplied by the caller, never discovered here.</param>
	/// <returns>The distinct arm names, ordered.</returns>
	/// <remarks>
	/// An arm is a <c>protected</c> instance method named <c>Verify*</c>. Protected is load-bearing: the
	/// kit's public and private members are its fixture plumbing, not arms a suite owes a call to.
	/// </remarks>
	internal static IReadOnlyList<string> SurveyKitArms(IEnumerable<SourceFile> kitSources)
	{
		ArgumentNullException.ThrowIfNull(kitSources);

		return [.. kitSources
			.SelectMany(file => CSharpSyntaxTree.ParseText(file.Text, path: Normalize(file.Path))
				.GetCompilationUnitRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
				.Where(declaration => string.Equals(declaration.Identifier.ValueText, KitName, StringComparison.Ordinal))
				.SelectMany(declaration => declaration.Members.OfType<MethodDeclarationSyntax>()))
			.Where(method =>
				method.Modifiers.Any(token => token.IsKind(SyntaxKind.ProtectedKeyword))
				&& !method.Modifiers.Any(token => token.IsKind(SyntaxKind.StaticKeyword))
				&& method.Identifier.ValueText.StartsWith("Verify", StringComparison.Ordinal))
			.Select(method => method.Identifier.ValueText)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(name => name, StringComparer.Ordinal)];
	}

	/// <summary>
	/// Returns each kit arm that a kit-deriving suite never calls.
	/// </summary>
	/// <param name="sources">The files to inspect. Supplied by the caller, never discovered here.</param>
	/// <param name="arms">The kit arms every deriving suite owes a call to.</param>
	/// <param name="owningProjects">Real source ownership by normalized path; omitted for synthetic controls.</param>
	/// <returns>One entry per uncalled arm, ordered by identity.</returns>
	/// <remarks>
	/// <para>
	/// The obligation is held by the <b>outermost</b> containing type, and a call anywhere inside it
	/// discharges that arm. Both shapes in this repository are then covered by one rule: a suite that
	/// derives the kit directly, and a suite that nests a private <c>Fixture</c> deriving it — which is the
	/// commoner shape, and the reason an inherited-<c>[Fact]</c> approach was never available here.
	/// </para>
	/// <para>
	/// The base list is read from the syntax tree, never matched textually: a C# base list routinely wraps
	/// onto the line after the class name, and a line-oriented pattern misses those declarations while
	/// returning a confident count.
	/// </para>
	/// </remarks>
	internal static IReadOnlyList<ArmDebt> SurveyUncalledArms(
		IEnumerable<SourceFile> sources,
		IReadOnlyList<string> arms,
		IReadOnlyDictionary<string, string>? owningProjects = null)
	{
		ArgumentNullException.ThrowIfNull(sources);
		ArgumentNullException.ThrowIfNull(arms);

		var debts = new List<ArmDebt>();

		foreach (var file in sources)
		{
			var root = CSharpSyntaxTree.ParseText(file.Text, path: Normalize(file.Path)).GetCompilationUnitRoot();
			var project = owningProjects is not null
				? owningProjects[Normalize(file.Path)]
				: SyntheticProjectOf(file.Path);

			foreach (var outer in root.DescendantNodes().OfType<ClassDeclarationSyntax>()
				.Where(declaration => !declaration.Ancestors().OfType<TypeDeclarationSyntax>().Any()))
			{
				var derives = outer.DescendantNodesAndSelf().OfType<ClassDeclarationSyntax>()
					.Any(declaration => DerivesTheKit(declaration)
						&& !declaration.Modifiers.Any(token => token.IsKind(SyntaxKind.AbstractKeyword)));

				if (!derives)
				{
					continue;
				}

				var called = CalledNames(outer);
				debts.AddRange(arms
					.Where(arm => !called.Contains(arm))
					.Select(arm => new ArmDebt(
						$"{project}|{QualifiedTypeOf(outer)}|{arm}",
						Normalize(file.Path))));
			}
		}

		return [.. debts.OrderBy(debt => debt.Identity, StringComparer.Ordinal)];
	}

	private static IReadOnlyList<ArmDebt> UnlistedDebts(IEnumerable<ArmDebt> uncalled, ISet<string> baseline) =>
		[.. uncalled.Where(debt => !baseline.Contains(debt.Identity))];

	private static IReadOnlyList<string> ObsoleteBaselineEntries(IEnumerable<ArmDebt> uncalled, ISet<string> baseline)
	{
		var current = uncalled.Select(debt => debt.Identity).ToHashSet(StringComparer.Ordinal);
		return [.. baseline.Where(identity => !current.Contains(identity))];
	}

	/// <summary>
	/// Returns every name INVOKED anywhere inside a type, bare or through a receiver.
	/// </summary>
	/// <remarks>
	/// Invocation, not mention. A name in a string literal, a doc comment, or a <c>nameof</c> must not
	/// discharge an arm — that is the shape by which a suite could satisfy this guard while calling
	/// nothing, and it is asserted by a synthetic control below.
	/// </remarks>
	private static HashSet<string> CalledNames(SyntaxNode scope) =>
		[.. scope.DescendantNodes().OfType<InvocationExpressionSyntax>()
			.Select(invocation => invocation.Expression switch
			{
				IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
				MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
				MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
				_ => null,
			})
			.Where(name => name is not null)
			.Select(name => name!)];

	private static bool DerivesTheKit(ClassDeclarationSyntax declaration) =>
		declaration.BaseList?.Types.Any(type => string.Equals(SimpleNameOf(type.Type), KitName, StringComparison.Ordinal))
		?? false;

	private static string SimpleNameOf(TypeSyntax type) => type switch
	{
		IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
		GenericNameSyntax generic => generic.Identifier.ValueText,
		QualifiedNameSyntax qualified => SimpleNameOf(qualified.Right),
		AliasQualifiedNameSyntax aliased => SimpleNameOf(aliased.Name),
		_ => string.Empty,
	};

	// The namespace/type delimiter and the nested-type delimiter are distinct, so a namespace cannot
	// collide with a containing type of the same name.
	private static string QualifiedTypeOf(ClassDeclarationSyntax declaration)
	{
		var namespaceName = declaration.Ancestors()
			.OfType<BaseNamespaceDeclarationSyntax>()
			.Select(node => node.Name.ToString())
			.FirstOrDefault() ?? string.Empty;

		return $"{namespaceName}::{declaration.Identifier.ValueText}";
	}

	private static string SyntheticProjectOf(string path) => string.Join('/', Normalize(path).Split('/').Take(3));

	private static string Normalize(string path) => path.Replace('\\', '/');

	// ---------- synthetic controls ----------

	private static readonly SourceFile Kit = new(
		KitSourcePath,
		"""
		namespace Excalibur.Dispatch.Testing.Transport;
		public abstract partial class TransportReceiverConformanceTests
		{
			protected Task VerifyAlpha(int x) => Task.CompletedTask;
			protected Task VerifyBeta(int x) => Task.CompletedTask;
			protected static Task VerifyStaticHelper() => Task.CompletedTask;
			private Task VerifyPrivatePlumbing() => Task.CompletedTask;
			public Task NotAnArm() => Task.CompletedTask;
		}
		""");

	private static SourceFile Suite(string body) => new(
		"tests/unit/Probe/SomeTransportSettlementConformanceShould.cs",
		$"namespace Probe; public sealed class SomeShould {{ {body} }}");

	[Fact]
	public void Enumerate_only_protected_instance_verify_members_as_arms() =>
		SurveyKitArms([Kit]).ShouldBe(["VerifyAlpha", "VerifyBeta"]);

	[Fact]
	public void Report_an_arm_a_nested_fixture_never_calls()
	{
		var debts = SurveyUncalledArms(
			[Suite("private sealed class Fixture : TransportReceiverConformanceTests { public Task Go() => VerifyAlpha(1); }")],
			["VerifyAlpha", "VerifyBeta"]);

		debts.Select(debt => debt.Identity).ShouldBe(["tests/unit/Probe|Probe::SomeShould|VerifyBeta"]);
	}

	[Fact]
	public void Clear_a_suite_that_calls_every_arm()
	{
		var debts = SurveyUncalledArms(
			[Suite("private sealed class Fixture : TransportReceiverConformanceTests { public Task Go() { VerifyAlpha(1); return this.VerifyBeta(2); } }")],
			["VerifyAlpha", "VerifyBeta"]);

		debts.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("private const string Note = \"VerifyBeta\";")]
	[InlineData("private static readonly string Note = nameof(TransportReceiverConformanceTests);")]
	[InlineData("/// <summary>VerifyBeta</summary>\n\tprivate int Note => 1;")]
	public void Refuse_to_count_a_mention_of_an_arm_as_calling_it(string mention)
	{
		var debts = SurveyUncalledArms(
			[Suite($"{mention} private sealed class Fixture : TransportReceiverConformanceTests {{ public Task Go() => VerifyAlpha(1); }}")],
			["VerifyAlpha", "VerifyBeta"]);

		debts.Select(debt => debt.Identity).ShouldBe(["tests/unit/Probe|Probe::SomeShould|VerifyBeta"]);
	}

	[Fact]
	public void See_a_declaration_whose_base_list_wraps_onto_the_next_line()
	{
		var debts = SurveyUncalledArms(
			[Suite("private sealed class Fixture\n\t\t: TransportReceiverConformanceTests { public Task Go() => VerifyAlpha(1); }")],
			["VerifyAlpha", "VerifyBeta"]);

		debts.Select(debt => debt.Identity).ShouldBe(["tests/unit/Probe|Probe::SomeShould|VerifyBeta"]);
	}

	[Fact]
	public void Ignore_a_type_that_does_not_derive_the_kit() =>
		SurveyUncalledArms([Suite("private sealed class Fixture { public int Go() => 1; }")], ["VerifyAlpha"]).ShouldBeEmpty();

	[Fact]
	public void Ignore_an_abstract_intermediate_that_derives_the_kit() =>
		SurveyUncalledArms(
			[Suite("private abstract class Base : TransportReceiverConformanceTests { }")],
			["VerifyAlpha"]).ShouldBeEmpty();

	// ---------- the ratchet, against the real tree ----------

	[Fact]
	public void Admit_no_uncalled_kit_arm_outside_the_baseline()
	{
		var arms = SurveyKitArms(ReadKitSources());
		var sources = ReadTestSources(out var population, out var owningProjects);
		var uncalled = SurveyUncalledArms(sources, arms, owningProjects);

		arms.ShouldNotBeEmpty("the kit arm population must be non-empty or every verdict here is vacuous");
		population.ShouldNotBeEmpty("the source population must be non-empty or every verdict here is vacuous");

		var unlisted = UnlistedDebts(uncalled, ReadBaseline());

		unlisted.ShouldBeEmpty(
			$"these suites derive {KitName} and never call the listed arm, so it cannot fail for that "
			+ "transport and its result says nothing. Call the arm from the suite, or add the entry with a "
			+ $"reason. {BaselineRelativePath} must never grow:{Environment.NewLine}"
			+ string.Join(Environment.NewLine, unlisted.Select(debt => $"{debt.Identity} ({debt.SourcePath})")));
	}

	[Fact]
	public void Require_a_baselined_arm_that_is_now_called_to_be_removed_from_the_baseline()
	{
		var arms = SurveyKitArms(ReadKitSources());
		var sources = ReadTestSources(out _, out var owningProjects);
		var uncalled = SurveyUncalledArms(sources, arms, owningProjects);

		var nowCalled = ObsoleteBaselineEntries(uncalled, ReadBaseline());

		nowCalled.ShouldBeEmpty(
			$"these are listed in {BaselineRelativePath} but the suite now calls the arm, or no longer "
			+ $"derives the kit. Remove them: the baseline shrinks and never grows.{Environment.NewLine}"
			+ string.Join(Environment.NewLine, nowCalled));
	}

	private static IReadOnlyList<SourceFile> ReadKitSources() =>
	[
		new SourceFile(
			KitSourcePath,
			File.ReadAllText(Path.Combine(
				TestHelpers.GetRepositoryRoot(),
				KitSourcePath.Replace('/', Path.DirectorySeparatorChar)))),
	];

	/// <summary>Reads every non-generated C# source under <c>tests/</c>.</summary>
	private static IReadOnlyList<SourceFile> ReadTestSources(
		out IReadOnlyList<SourceFile> population,
		out IReadOnlyDictionary<string, string> owningProjects)
	{
		var repositoryRoot = TestHelpers.GetRepositoryRoot();

		var sources = Directory
			.EnumerateFiles(Path.Combine(repositoryRoot, "tests"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Select(path => new SourceFile(Path.GetRelativePath(repositoryRoot, path), File.ReadAllText(path)))
			.ToList();

		population = sources;
		var projects = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "tests"), "*.csproj", SearchOption.AllDirectories)
			.Select(path => Normalize(Path.GetRelativePath(repositoryRoot, path)))
			.OrderByDescending(path => path.LastIndexOf('/')).ToList();

		owningProjects = sources.ToDictionary(file => Normalize(file.Path), file => projects
			.FirstOrDefault(project => Normalize(file.Path).StartsWith(project[..(project.LastIndexOf('/') + 1)], StringComparison.Ordinal))
			?? $"unowned:{Normalize(file.Path)}", StringComparer.Ordinal);

		return sources;
	}

	/// <summary>Reads the baseline, ignoring comment and blank lines.</summary>
	private static HashSet<string> ReadBaseline()
	{
		var path = Path.Combine(
			TestHelpers.GetRepositoryRoot(),
			BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));

		return [.. File.ReadAllLines(path)
			.Select(line => line.Trim())
			.Where(line => line.Length > 0 && !line.StartsWith('#'))];
	}
}
