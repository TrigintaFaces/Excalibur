// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests;

/// <summary>
/// Structural guard that a suite deriving a conformance kit also wires the kit's completeness check.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks.</b> A conformance kit declares its arms as <c>public virtual async Task</c>
/// with no test attribute, so nothing discovers them. An arm runs only if a deriving suite hand-writes an
/// attributed wrapper that calls it. A suite that wraps thirty of a kit's thirty-four arms therefore
/// reports thirty passes and says nothing at all about the other four — an unwired arm is indistinguishable
/// in the results from an arm that passed. The kit already ships the answer,
/// <c>ConformanceSuite_ShouldWireEveryArm</c>, which reflects over declared members and names any arm left
/// unwired; the gap is that wiring that guard is itself optional, which is the same regress one level up.
/// </para>
/// <para>
/// <b>Why the arms are not simply attributed in the kit.</b> Putting <c>[Fact]</c> on the kit's arms and
/// letting derivers inherit them would delete every wrapper, and under reflection-based discovery it works.
/// It is refused because of how the other discovery mode behaves: xunit's AOT source generator drops
/// inherited test methods declared in a base class without reporting anything, and the run is green
/// (xunit/xunit#3627, fixed in xunit.v3 4.0.2-pre.5 — this repository pins 3.2.2). This framework
/// advertises Native AOT support, so inheriting the attribute would trade a loud gap for a silent one
/// aimed precisely at the consumers told to use AOT. The wrapper is declared in the deriving class's own
/// source, so the generator sees it. That is the reason the verbose shape is correct, and it is recorded
/// here because it is not visible from the code.
/// </para>
/// <para>
/// <b>Why a baseline rather than a sweep.</b> The unwired set is not one population: most of it is a
/// family tracked as its own work item, and a large part is kit <i>probes</i> that derive a kit in order to
/// test it and deliberately omit an arm so a guard has something to catch. Asserting completeness on a
/// probe would assert the opposite of its purpose. The baseline records which is which, so a reader can
/// tell a deliberate exemption from a forgotten one — which was the original defect, one level up again.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class ConformanceSuiteWiresEveryArmShould
{
	/// <summary>The completeness check every kit-deriving suite is expected to wire.</summary>
	private const string CompletenessGuard = "ConformanceSuite_ShouldWireEveryArm";

	/// <summary>The suffix identifying a conformance kit base type.</summary>
	private const string KitSuffix = "ConformanceTestKit";

	/// <summary>Repository-relative path of the shrink-only baseline.</summary>
	private const string BaselineRelativePath =
		"tests/architecture/Boundary.Tests/conformance-suite-wiring-baseline.txt";

	// ---------- the injected-data core ----------

	/// <summary>
	/// Returns the paths of sources that declare a kit-deriving type without wiring the completeness guard.
	/// </summary>
	/// <param name="sources">The files to inspect. Supplied by the caller, never discovered here.</param>
	/// <returns>Repository-relative paths, forward-slashed, ordered, distinct.</returns>
	/// <remarks>
	/// The base list is read from the syntax tree rather than matched textually: a C# base list routinely
	/// wraps onto the line after the class name, and a line-oriented pattern misses those declarations
	/// entirely while returning a confident count. Measured on this repository, a line-based form saw 143
	/// of 183 such declarations — the 40 it could not see were the wrapped ones.
	/// </remarks>
	internal static IReadOnlyList<string> SurveyUnwiredSuites(IEnumerable<SourceFile> sources)
	{
		ArgumentNullException.ThrowIfNull(sources);

		var unwired = new List<string>();

		foreach (var file in sources)
		{
			var root = CSharpSyntaxTree.ParseText(file.Text).GetRoot();

			var derivesAKit = root.DescendantNodes()
				.OfType<ClassDeclarationSyntax>()
				.Any(DerivesAConformanceKit);

			if (!derivesAKit)
			{
				continue;
			}

			// An invocation, not a mention. A declaration of the guard, a comment naming it, or a string
			// containing it are each the shape of a suite that looks wired and is not.
			var wiresTheGuard = root.DescendantNodes()
				.OfType<InvocationExpressionSyntax>()
				.Any(invocation => InvokedNameOf(invocation) == CompletenessGuard);

			if (!wiresTheGuard)
			{
				unwired.Add(Normalize(file.Path));
			}
		}

		return unwired.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList();
	}

	/// <summary>Decides whether a class declaration names a conformance kit in its base list.</summary>
	/// <param name="declaration">The class declaration to inspect.</param>
	/// <returns><see langword="true" /> when any base type's simple name ends with the kit suffix.</returns>
	private static bool DerivesAConformanceKit(ClassDeclarationSyntax declaration) =>
		declaration.BaseList is not null
		&& declaration.BaseList.Types.Any(baseType => SimpleNameOf(baseType.Type).EndsWith(KitSuffix, StringComparison.Ordinal));

	/// <summary>Reduces a possibly-qualified or generic type syntax to its simple identifier.</summary>
	/// <param name="type">The type syntax from a base list.</param>
	/// <returns>The rightmost identifier, without type arguments.</returns>
	private static string SimpleNameOf(TypeSyntax type) => type switch
	{
		SimpleNameSyntax simple => simple.Identifier.ValueText,
		QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
		AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
		_ => string.Empty,
	};

	/// <summary>Returns the method name an invocation targets, ignoring any receiver.</summary>
	/// <param name="invocation">The invocation to inspect.</param>
	/// <returns>The invoked method's simple name, or <see langword="null" /> when it cannot be determined.</returns>
	private static string? InvokedNameOf(InvocationExpressionSyntax invocation) => invocation.Expression switch
	{
		IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
		MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
		_ => null,
	};

	/// <summary>Normalizes a path to the forward-slashed form the baseline stores.</summary>
	/// <param name="path">The path to normalize.</param>
	/// <returns>The path with forward slashes.</returns>
	private static string Normalize(string path) => path.Replace('\\', '/');

	// ---------- non-vacuity: the core must FAIL on the shape it exists to catch ----------

	private static readonly SourceFile SuiteWithoutTheGuard = new(
		"tests/unit/Probe/UnwiredSuite.cs",
		"""
		namespace Probe;

		private sealed class UnwiredSuite : InboxStoreConformanceTestKit
		{
			[Fact]
			public Task OneArm_Test() => CreateEntryAsync_NewEntry_ShouldSucceed();
		}
		""");

	private static readonly SourceFile SuiteWithTheGuard = new(
		"tests/unit/Probe/WiredSuite.cs",
		"""
		namespace Probe;

		private sealed class WiredSuite : InboxStoreConformanceTestKit
		{
			[Fact]
			public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
		}
		""");

	private static readonly SourceFile SuiteWithAWrappedBaseList = new(
		"tests/unit/Probe/WrappedBaseListSuite.cs",
		"""
		namespace Probe;

		private sealed class WrappedBaseListSuite
			: Excalibur.Testing.Conformance.InboxStoreConformanceTestKit
		{
			[Fact]
			public Task OneArm_Test() => CreateEntryAsync_NewEntry_ShouldSucceed();
		}
		""");

	private static readonly SourceFile SuiteThatOnlyMentionsTheGuard = new(
		"tests/unit/Probe/MentionOnlySuite.cs",
		"""
		namespace Probe;

		private sealed class MentionOnlySuite : InboxStoreConformanceTestKit
		{
			// ConformanceSuite_ShouldWireEveryArm is wired in a sibling partial. It is not.
			private const string Note = "ConformanceSuite_ShouldWireEveryArm";
		}
		""");

	[Fact]
	public void Report_a_kit_deriving_suite_that_does_not_wire_the_guard()
	{
		var unwired = SurveyUnwiredSuites([SuiteWithoutTheGuard]);

		unwired.ShouldBe(["tests/unit/Probe/UnwiredSuite.cs"]);
	}

	[Fact]
	public void Clear_a_kit_deriving_suite_that_wires_the_guard()
	{
		var unwired = SurveyUnwiredSuites([SuiteWithTheGuard]);

		unwired.ShouldBeEmpty("a suite invoking the completeness guard is wired and must not be reported");
	}

	[Fact]
	public void See_a_declaration_whose_base_list_wraps_onto_the_next_line()
	{
		// The failure this arm exists for is not hypothetical: the line-oriented survey that preceded this
		// guard could not see 40 of the repository's 183 kit-deriving declarations, and reported a count.
		var unwired = SurveyUnwiredSuites([SuiteWithAWrappedBaseList]);

		unwired.ShouldBe(["tests/unit/Probe/WrappedBaseListSuite.cs"]);
	}

	[Fact]
	public void Refuse_to_count_a_mention_of_the_guard_as_wiring_it()
	{
		var unwired = SurveyUnwiredSuites([SuiteThatOnlyMentionsTheGuard]);

		unwired.ShouldBe(
			["tests/unit/Probe/MentionOnlySuite.cs"],
			"a comment or string naming the guard is exactly how a suite looks wired while running nothing");
	}

	// ---------- the ratchet, against the real tree ----------

	[Fact]
	public void Admit_no_kit_deriving_suite_outside_the_baseline()
	{
		var unwired = SurveyUnwiredSuites(ReadTestSources(out var population));

		population.ShouldNotBeEmpty("the source population must be non-empty or every verdict here is vacuous");

		var baseline = ReadBaseline();

		var unlisted = unwired.Where(path => !baseline.Contains(path)).ToList();

		unlisted.ShouldBeEmpty(
			$"these suites derive a conformance kit and never invoke {CompletenessGuard}, so any arm nobody "
			+ "wrapped is silently not run. Wire the guard, or add the file to "
			+ $"{BaselineRelativePath} under the group that says why it is exempt:{Environment.NewLine}"
			+ string.Join(Environment.NewLine, unlisted));
	}

	[Fact]
	public void Require_a_baselined_suite_that_is_now_wired_to_be_removed_from_the_baseline()
	{
		var unwired = SurveyUnwiredSuites(ReadTestSources(out _)).ToHashSet(StringComparer.Ordinal);

		var baseline = ReadBaseline();

		var nowWired = baseline.Where(path => !unwired.Contains(path)).ToList();

		nowWired.ShouldBeEmpty(
			$"these are listed in {BaselineRelativePath} but now either wire the guard or no longer derive a "
			+ $"kit. Remove them: the baseline shrinks and never grows.{Environment.NewLine}"
			+ string.Join(Environment.NewLine, nowWired));
	}

	/// <summary>Reads every non-generated C# source under <c>tests/</c>.</summary>
	/// <param name="population">Receives the files read, for the non-vacuity assertion.</param>
	/// <returns>The sources, repository-relative.</returns>
	private static IReadOnlyList<SourceFile> ReadTestSources(out IReadOnlyList<SourceFile> population)
	{
		var repositoryRoot = TestHelpers.GetRepositoryRoot();

		var sources = Directory
			.EnumerateFiles(Path.Combine(repositoryRoot, "tests"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Select(path => new SourceFile(Path.GetRelativePath(repositoryRoot, path), File.ReadAllText(path)))
			.ToList();

		population = sources;

		return sources;
	}

	/// <summary>Reads the baseline, ignoring comment and blank lines.</summary>
	/// <returns>The baselined repository-relative paths.</returns>
	private static HashSet<string> ReadBaseline()
	{
		var path = Path.Combine(
			TestHelpers.GetRepositoryRoot(),
			BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));

		return File.ReadAllLines(path)
			.Select(line => line.Trim())
			.Where(line => line.Length > 0 && !line.StartsWith('#'))
			.ToHashSet(StringComparer.Ordinal);
	}
}
