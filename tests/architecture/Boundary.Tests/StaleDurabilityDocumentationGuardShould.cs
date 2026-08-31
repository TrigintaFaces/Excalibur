// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests;

/// <summary>
/// The verdict for a single documented member.
/// </summary>
internal enum DocumentationVerdict
{
	/// <summary>The documentation makes no startup-consequence claim, or the claim is reachable.</summary>
	Pass,

	/// <summary>The documentation asserts a startup consequence that nothing can produce.</summary>
	Fail,

	/// <summary>The member's documentation cannot be judged from its own text.</summary>
	Refuse,
}

/// <summary>
/// A member examined by the guard, with the verdict and the reason it was reached.
/// </summary>
internal sealed record DocumentationFinding(string MemberName, DocumentationVerdict Verdict, string Reason);

/// <summary>
/// Structural guard against documentation asserting an enforcement the product cannot perform.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks.</b> Four public options properties shipped XML documentation stating that the
/// protective default "requires a durable store and fails startup when none is configured", while no
/// component capable of failing startup was installed anywhere. The text compiled into the NuGet
/// documentation file and reached consumers through IntelliSense, so a reader was told an unsafe default
/// was caught at boot when nothing would stop it. Two independent reviews and a full-suite run passed over
/// it; a person found it twenty-four minutes before close.
/// </para>
/// <para>
/// <b>Why a member-kind population.</b> A property that documents a startup consequence is claiming
/// something about the framework's behaviour on the consumer's behalf. A registration method documenting
/// the same consequence is describing what it does <i>if called</i>, which stays true whether or not
/// anyone calls it. The compiler distinguishes these — the two roles are different member kinds — so the
/// population is derived from the declaration rather than from file names, which drift.
/// </para>
/// <para>
/// <b>Why source and not the built documentation file.</b> The generated XML lives in build output, and
/// build output is not one artifact: dozens of copies of the same file exist across output directories at
/// differing ages, none of them tracked. A check reading one of them is right or wrong depending on which
/// copy it opened. Member kind is a property of the declaration, so reading source removes the question
/// entirely.
/// </para>
/// <para>
/// <b>The inversion, which is the whole design.</b> When the enforcing component is wired, the same
/// sentence becomes true, and a guard keyed only on the wording would then fire on correct documentation
/// — forcing whoever restores the feature to delete the guard. Installed state is therefore an
/// <i>input</i>: the same text passes or fails depending on whether the claim is reachable. Both
/// directions are asserted below, because a guard proven in one state only is the failure it exists to
/// prevent.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class StaleDurabilityDocumentationGuardShould
{
	/// <summary>
	/// Words placing a claim at process start. Deliberately the stem rather than whole words: "startup",
	/// "start-up" and "refuses to start" are all the same claim, and a list of whole words silently drops
	/// whichever spelling the next author picks. Matched against documentation text only, never against
	/// identifiers, so renaming a member cannot silence the guard.
	/// </summary>
	private static readonly string[] StartupWords = ["start", "boot"];

	/// <summary>
	/// Words by which documentation asserts that something is refused or checked rather than merely
	/// described.
	/// </summary>
	private static readonly string[] RefusalWords = ["fail", "refus", "reject", "block", "gate", "error"];

	/// <summary>
	/// Decides whether documentation claims a consequence at process start.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why co-occurrence and not a phrase list.</b> This began as four exact phrases — "fails startup",
	/// "fail startup", "refuses to start", "blocks startup" — taken from the wording the defect shipped
	/// with. The wording was later improved to "the framework fails fast at startup if a volatile store is
	/// registered", and the single word inserted between "fails" and "startup" made the guard blind. It
	/// then reported Pass over all four options files it exists to police, including one whose seam had no
	/// production call site at all, and its own fixtures kept passing because they were written to match
	/// the phrase list rather than the text the product ships.
	/// </para>
	/// <para>
	/// A phrase list tests whether an author chose a particular sentence. The property that matters is
	/// whether the documentation tells a reader something is enforced at startup. Co-occurrence is a closer
	/// approximation of that and, unlike a phrase list, does not silently narrow when prose is edited. It
	/// is still an approximation: it can call a claim where a sentence merely mentions both ideas. That
	/// direction is the safe one — a false claim is only ever reported when the seam is <i>also</i>
	/// unreachable, which is a fact worth surfacing however the sentence was phrased.
	/// </para>
	/// </remarks>
	/// <param name="documentation">The documentation text of a single member.</param>
	/// <returns><see langword="true"/> when the text asserts a consequence at process start.</returns>
	private static bool ClaimsStartupConsequence(string documentation) =>
		StartupWords.Any(word => documentation.Contains(word, StringComparison.OrdinalIgnoreCase))
		&& RefusalWords.Any(word => documentation.Contains(word, StringComparison.OrdinalIgnoreCase));

	// ---------- the injected-data core ----------

	/// <summary>
	/// Classifies every documented member declared in <paramref name="source"/>.
	/// </summary>
	/// <param name="source">C# source text.</param>
	/// <param name="enforcementIsReachable">
	/// Whether the component that performs the documented refusal is wired. Supplied by the caller so the
	/// same text can be judged in both states; the guard never assumes it.
	/// </param>
	/// <returns>One finding per member carrying documentation.</returns>
	internal static IReadOnlyList<DocumentationFinding> Classify(string source, bool enforcementIsReachable)
	{
		ArgumentNullException.ThrowIfNull(source);

		var root = CSharpSyntaxTree.ParseText(source).GetRoot();
		var findings = new List<DocumentationFinding>();

		foreach (var member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
		{
			// Population: PROPERTIES only. A method documenting the same consequence describes its own
			// behaviour when invoked and is out of population by construction, not by an exclusion list.
			if (member is not PropertyDeclarationSyntax property)
			{
				continue;
			}

			var documentation = DocumentationTextOf(property);
			if (documentation.Length == 0)
			{
				continue;
			}

			if (documentation.Contains("<inheritdoc", StringComparison.OrdinalIgnoreCase))
			{
				// The compiler copies this element through without resolving it, so neither source nor the
				// generated file carries the inherited wording. Not evaluated — and reported as such,
				// because a silent skip would let the guard report success over members it never read.
				findings.Add(new DocumentationFinding(
					property.Identifier.ValueText,
					DocumentationVerdict.Refuse,
					"documentation is inherited and cannot be judged from its own text"));
				continue;
			}

			if (!ClaimsStartupConsequence(documentation))
			{
				findings.Add(new DocumentationFinding(
					property.Identifier.ValueText,
					DocumentationVerdict.Pass,
					"documentation asserts no startup consequence"));
				continue;
			}

			findings.Add(enforcementIsReachable
				? new DocumentationFinding(
					property.Identifier.ValueText,
					DocumentationVerdict.Pass,
					"documentation asserts a startup consequence and the enforcement is reachable")
				: new DocumentationFinding(
					property.Identifier.ValueText,
					DocumentationVerdict.Fail,
					"documentation asserts a startup consequence that nothing can produce"));
		}

		return findings;
	}

	private static string DocumentationTextOf(SyntaxNode node) =>
		string.Concat(node.GetLeadingTrivia()
			.Where(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
				|| trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
			.Select(trivia => trivia.ToFullString()));

	// ---------- fixtures ----------

	private const string PropertyClaimingStartupEnforcement = """
		public sealed class SomeOptions
		{
			/// <summary>Whether a volatile store is permitted.</summary>
			/// <value>
			/// <see langword="false" /> — the default — requires a durable store and fails startup when
			/// none is configured.
			/// </value>
			public bool AllowVolatileStore { get; set; }
		}
		""";

	/// <summary>
	/// The sentence the options files actually ship, copied from them rather than composed here. A fixture
	/// written to match the predicate proves only that the predicate matches itself; this one fails if the
	/// predicate ever again stops seeing the product.
	/// </summary>
	private const string PropertyUsingShippedWording = """
		public sealed class SomeOptions
		{
			/// <summary>Whether a volatile store is permitted.</summary>
			/// <value>
			/// <see langword="false" /> — the default — states that this host expects a durable store, and
			/// the framework fails fast at startup if a volatile store is registered.
			/// </value>
			public bool AllowVolatileStore { get; set; }
		}
		""";

	/// <summary>
	/// A documented property asserting nothing about startup, so the predicate is shown to discriminate
	/// rather than to call everything a claim.
	/// </summary>
	private const string PropertyClaimingNothing = """
		public sealed class SomeOptions
		{
			/// <summary>Whether a volatile store is permitted.</summary>
			/// <value>Set this only for development hosts, where losing pending work is intended.</value>
			public bool AllowVolatileStore { get; set; }
		}
		""";

	private const string MethodClaimingStartupEnforcement = """
		public static class SomeRegistration
		{
			/// <summary>Adds the boot-time guard that fails startup when left on a volatile store.</summary>
			public static void AddGuard() { }
		}
		""";

	// ---------- SAFETY: the claim nothing can produce is refused ----------

	[Fact]
	public void Fail_a_property_claiming_startup_enforcement_when_nothing_can_produce_it()
	{
		var findings = Classify(PropertyClaimingStartupEnforcement, enforcementIsReachable: false);

		findings.ShouldHaveSingleItem().Verdict.ShouldBe(
			DocumentationVerdict.Fail,
			"the documentation promises a boot-time refusal that no installed component can perform");
	}

	// ---------- LIVENESS: the same text passes once the claim is reachable ----------

	[Fact]
	public void Pass_the_same_property_unchanged_once_the_enforcement_is_reachable()
	{
		// The identical text, judged in the other state. This is the arm that distinguishes a guard which
		// reads installed state from one that remembers an answer: no character of the source differs.
		var findings = Classify(PropertyClaimingStartupEnforcement, enforcementIsReachable: true);

		findings.ShouldHaveSingleItem().Verdict.ShouldBe(
			DocumentationVerdict.Pass,
			"once the enforcement is wired the sentence is true and must not be reported");
	}

	// ---------- SAFETY: the predicate must see the wording the product ships ----------

	[Fact]
	public void Recognise_the_wording_the_options_files_actually_ship()
	{
		var findings = Classify(PropertyUsingShippedWording, enforcementIsReachable: false);

		findings.ShouldHaveSingleItem().Verdict.ShouldBe(
			DocumentationVerdict.Fail,
			"this is the sentence the four options files carry; a predicate that cannot see it reports "
			+ "Pass over its entire real population while a seam sits wired to nothing");
	}

	// ---------- LIVENESS: the predicate discriminates ----------

	[Fact]
	public void Not_call_a_claim_where_documentation_asserts_no_startup_consequence()
	{
		var findings = Classify(PropertyClaimingNothing, enforcementIsReachable: false);

		findings.ShouldHaveSingleItem().Verdict.ShouldBe(
			DocumentationVerdict.Pass,
			"a guard that called every documented property a claim would fail the honest ones too");
	}

	// ---------- LIVENESS: the registration method is out of population ----------

	[Fact]
	public void Not_report_a_registration_method_documenting_the_same_consequence()
	{
		var findings = Classify(MethodClaimingStartupEnforcement, enforcementIsReachable: false);

		findings.ShouldBeEmpty(
			"a method documents what it does when called, which stays true whether or not it is called");
	}

	// ---------- REFUSE is not PASS ----------

	[Fact]
	public void Refuse_rather_than_pass_a_property_whose_documentation_is_inherited()
	{
		const string inherited = """
			public sealed class SomeOptions
			{
				/// <inheritdoc />
				public bool AllowVolatileStore { get; set; }
			}
			""";

		var finding = Classify(inherited, enforcementIsReachable: false).ShouldHaveSingleItem();

		finding.Verdict.ShouldBe(
			DocumentationVerdict.Refuse,
			"an unread member must be reported as unread; counting it as a pass is the silent-skip failure");
		finding.Verdict.ShouldNotBe(DocumentationVerdict.Pass);
	}

	// ---------- the population is derived, not enumerated ----------

	[Fact]
	public void Report_a_property_it_has_never_seen_before()
	{
		// A member at no known position, with a name shared by nothing in the repository. A guard carrying
		// a list of known sites cannot report this; a guard applying the rule reports it without alteration.
		const string unknownMember = """
			public sealed class AnUnrelatedOptions
			{
				/// <value>The default refuses to start when no provider is present.</value>
				public bool SomeEntirelyDifferentName { get; set; }
			}
			""";

		Classify(unknownMember, enforcementIsReachable: false)
			.ShouldHaveSingleItem().Verdict.ShouldBe(DocumentationVerdict.Fail);
	}
}
