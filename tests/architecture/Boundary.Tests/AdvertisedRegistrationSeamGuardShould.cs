// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests;

/// <summary>
/// A source file presented to the seam guard: its repository-relative path and its text.
/// </summary>
internal sealed record SourceFile(string Path, string Text);

/// <summary>
/// Whether a registration seam can be reached, and the evidence for the answer.
/// </summary>
internal sealed record SeamReachability(string SeamName, int ProductionCallSites, int TestCallSites)
{
	/// <summary>
	/// Gets a value indicating whether a consumer of the shipped packages can reach this seam's effect.
	/// </summary>
	/// <value>
	/// <see langword="true" /> when at least one call site exists outside test code. Test call sites are
	/// counted separately and deliberately excluded: a seam invoked only by its own tests is exercised,
	/// not wired, and the distinction is the entire defect this guard addresses.
	/// </value>
	public bool IsReachable => ProductionCallSites > 0;
}

/// <summary>
/// Structural guard for registration seams that are advertised but never invoked.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks.</b> A public registration method can exist, compile, carry documentation,
/// be covered by its own passing tests, and be called by nothing that ships. Every signal available to a
/// reader says the feature is present. The feature is not present. Four such seams shipped in this
/// repository simultaneously, each with green tests, and the absence was found by counting call sites
/// rather than by any test failing.
/// </para>
/// <para>
/// <b>Why test call sites are counted but excluded.</b> A seam exercised only by its own tests is the
/// precise shape of the defect: the tests prove the method works when called, and nothing calls it. So
/// the count is reported rather than discarded — a seam with many test call sites and no production ones
/// is more suspicious than one with neither, because someone believed in it enough to test it.
/// </para>
/// <para>
/// <b>Relationship to the documentation guard.</b> This answers whether a claim is <i>reachable</i>; the
/// documentation guard answers <i>who may make the claim</i>. Neither is sufficient alone: a reachable
/// seam may still be documented by the wrong member kind, and a correctly-documented property may claim
/// an effect nothing can produce. Composed, they decide whether a documented consequence is honest.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class AdvertisedRegistrationSeamGuardShould
{
	// ---------- the injected-data core ----------

	/// <summary>
	/// Counts invocations of <paramref name="seamName"/> across the supplied sources, separating
	/// production call sites from test ones.
	/// </summary>
	/// <param name="seamName">The method name to count invocations of.</param>
	/// <param name="sources">The files to search. Supplied by the caller, never discovered here.</param>
	/// <returns>The reachability of the seam.</returns>
	internal static SeamReachability MeasureReachability(string seamName, IEnumerable<SourceFile> sources)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(seamName);
		ArgumentNullException.ThrowIfNull(sources);

		var production = 0;
		var test = 0;

		foreach (var file in sources)
		{
			var root = CSharpSyntaxTree.ParseText(file.Text).GetRoot();

			// Invocations only. A declaration of the method is not a call site, and neither is a mention
			// in a comment or a string — counting either is how a seam appears wired when it is not.
			var invocations = root.DescendantNodes()
				.OfType<InvocationExpressionSyntax>()
				.Count(invocation => InvokedNameOf(invocation) == seamName);

			if (invocations == 0)
			{
				continue;
			}

			if (IsTestPath(file.Path))
			{
				test += invocations;
			}
			else
			{
				production += invocations;
			}
		}

		return new SeamReachability(seamName, production, test);
	}

	private static string? InvokedNameOf(InvocationExpressionSyntax invocation) => invocation.Expression switch
	{
		MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
		IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
		GenericNameSyntax generic => generic.Identifier.ValueText,
		MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
		_ => null,
	};

	private static bool IsTestPath(string path) =>
		path.Replace('\\', '/').Contains("/tests/", StringComparison.OrdinalIgnoreCase)
		|| path.Replace('\\', '/').StartsWith("tests/", StringComparison.OrdinalIgnoreCase);

	// ---------- fixtures ----------

	private static readonly SourceFile Declaration = new(
		"src/Some.Package/SomeRegistration.cs",
		"""
		public static class SomeRegistration
		{
			public static IServiceCollection AddSomeGuard(this IServiceCollection services) => services;
		}
		""");

	private static readonly SourceFile ProductionCaller = new(
		"src/Some.Package/SomeComposition.cs",
		"""
		public static class SomeComposition
		{
			public static void Compose(IServiceCollection services) => services.AddSomeGuard();
		}
		""");

	private static readonly SourceFile TestCaller = new(
		"tests/unit/Some.Tests/SomeGuardShould.cs",
		"""
		public sealed class SomeGuardShould
		{
			public void Work() { var services = new ServiceCollection(); services.AddSomeGuard(); }
		}
		""");

	// ---------- SAFETY: a seam nobody calls is unreachable ----------

	[Fact]
	public void Report_a_seam_with_only_a_declaration_as_unreachable()
	{
		var reachability = MeasureReachability("AddSomeGuard", [Declaration]);

		reachability.IsReachable.ShouldBeFalse("a declaration is not a call site");
		reachability.ProductionCallSites.ShouldBe(0);
	}

	[Fact]
	public void Report_a_seam_called_only_by_its_own_tests_as_unreachable()
	{
		var reachability = MeasureReachability("AddSomeGuard", [Declaration, TestCaller]);

		reachability.IsReachable.ShouldBeFalse(
			"a seam exercised only by its tests is proven to work and still ships wired to nothing");
		reachability.TestCallSites.ShouldBe(1, "the test call site is counted, not discarded");
	}

	// ---------- LIVENESS: a wired seam is reachable ----------

	[Fact]
	public void Report_a_seam_with_a_production_call_site_as_reachable()
	{
		var reachability = MeasureReachability("AddSomeGuard", [Declaration, ProductionCaller, TestCaller]);

		reachability.IsReachable.ShouldBeTrue("a production call site makes the seam's effect reachable");
		reachability.ProductionCallSites.ShouldBe(1);
	}

	// ---------- LIVENESS: the count is of invocations, not mentions ----------

	[Fact]
	public void Not_count_a_mention_in_a_comment_or_string_as_a_call_site()
	{
		var mentions = new SourceFile(
			"src/Some.Package/SomeDocs.cs",
			"""
			public static class SomeDocs
			{
				// Call AddSomeGuard to install the guard.
				public const string Advice = "services.AddSomeGuard();";
			}
			""");

		MeasureReachability("AddSomeGuard", [Declaration, mentions])
			.IsReachable.ShouldBeFalse("prose and string literals do not invoke anything");
	}

	// ---------- the two guards compose ----------

	// ---------- the real repository ----------

	/// <summary>
	/// The durability seams and the options file whose documentation depends on each being reachable.
	/// Paths are repository-relative; the population is small and explicit because these four seams are
	/// the subject, not a sample of it.
	/// </summary>
	public static TheoryData<string, string> DurabilitySeams => new()
	{
		{ "AddAuditDurabilityGate", "src/Excalibur/Excalibur.AuditLogging/AuditLoggingOptions.cs" },
		{ "AddKeyDurabilityGate", "src/Excalibur/Excalibur.Compliance/Encryption/KeyDurability/KeyDurabilityOptions.cs" },
		{ "AddGrantDurabilityGate", "src/Excalibur/Excalibur.A3.Core/GrantDurability/GrantDurabilityOptions.cs" },
		{ "AddScheduleDurabilityGate", "src/Dispatch/Excalibur.Dispatch/Delivery/ScheduleDurability/ScheduleDurabilityOptions.cs" },
	};

	[Theory]
	[MemberData(nameof(DurabilitySeams))]
	public void Hold_the_documentation_of_every_durability_option_honest_against_its_real_seam(
		string seamName,
		string optionsPath)
	{
		var repositoryRoot = TestHelpers.GetRepositoryRoot();

		var sources = Directory
			.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Select(path => new SourceFile(Path.GetRelativePath(repositoryRoot, path), File.ReadAllText(path)))
			.ToList();

		sources.ShouldNotBeEmpty("the source population must be non-empty or every verdict below is vacuous");

		var reachability = MeasureReachability(seamName, sources);

		var optionsSource = File.ReadAllText(
			Path.Combine(repositoryRoot, optionsPath.Replace('/', Path.DirectorySeparatorChar)));

		var findings = StaleDurabilityDocumentationGuardShould.Classify(optionsSource, reachability.IsReachable);

		findings.ShouldNotBeEmpty(
			$"'{optionsPath}' must contain at least one documented property or this assertion proves nothing");

		findings.Where(finding => finding.Verdict == DocumentationVerdict.Fail).ShouldBeEmpty(
			$"'{optionsPath}' documents a startup consequence while '{seamName}' has "
			+ $"{reachability.ProductionCallSites} production call sites "
			+ $"({reachability.TestCallSites} test). Either wire the seam or correct the documentation.");
	}

	[Fact]
	public void Decide_a_documented_consequence_is_dishonest_only_when_the_seam_is_unreachable()
	{
		const string documentedClaim = """
			public sealed class SomeOptions
			{
				/// <value>The default requires a durable store and fails startup when none is configured.</value>
				public bool AllowVolatileStore { get; set; }
			}
			""";

		// Composed exactly as production will compose them: reachability is measured, then supplied as the
		// documentation guard's state input. Neither guard decides alone.
		var unwired = StaleDurabilityDocumentationGuardShould.Classify(
			documentedClaim,
			MeasureReachability("AddSomeGuard", [Declaration]).IsReachable);

		var wired = StaleDurabilityDocumentationGuardShould.Classify(
			documentedClaim,
			MeasureReachability("AddSomeGuard", [Declaration, ProductionCaller]).IsReachable);

		unwired.ShouldHaveSingleItem().Verdict.ShouldBe(
			DocumentationVerdict.Fail, "the claim is unreachable, so the documentation is false");
		wired.ShouldHaveSingleItem().Verdict.ShouldBe(
			DocumentationVerdict.Pass, "the same sentence is true once the seam is wired");
	}
}
