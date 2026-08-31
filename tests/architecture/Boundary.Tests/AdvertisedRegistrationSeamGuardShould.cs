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

	// ---------- the inverse guard: a composition that must DECIDE handler registration ----------

	// The guard above asks whether a declared seam has any caller. This one asks the opposite: whether
	// every caller of a seam made a decision the seam itself no longer makes for them.
	//
	// AddDispatch(Action<IDispatchBuilder>) used to discover handlers from the entry assembly whenever
	// its lambda named none. That fallback is gone, and every method that composes dispatch on a
	// consumer behalf now has to say what happens to handler registration. Nine such methods existed
	// when the fallback was removed and not one of them said anything; the shipped result was a
	// documented one-line entry point that registered no handlers at all.
	//
	// Enumerating the callers would only re-state the list as it stands today. The property is asserted
	// instead: a method that composes dispatch either names the discovery, or appears below with a
	// written reason.

	/// <summary>
	/// A method that composes dispatch on behalf of a consumer, and whether it names the entry-assembly
	/// handler discovery.
	/// </summary>
	internal sealed record DispatchComposition(string Path, string MethodName, bool NamesEntryAssemblyDiscovery);

	/// <summary>
	/// Every composing method found, plus the call sites that belonged to no method declaration.
	/// </summary>
	/// <remarks>
	/// <paramref name="UnattributedCallSites" /> exists because the survey is per-method: a composing call
	/// in a field initialiser or a top-level statement would be invisible to it, and invisible is
	/// indistinguishable from compliant. A non-zero count means the survey missed something, not that the
	/// repository is clean.
	/// </remarks>
	internal sealed record DispatchCompositionSurvey(
		IReadOnlyList<DispatchComposition> Compositions,
		int UnattributedCallSites);

	/// <summary>
	/// Methods allowed to compose dispatch without naming <c>AddHandlersFromEntryAssembly</c>, each with
	/// the reason it needs no such call. A method reaches this list by argument, never by convenience.
	/// </summary>
	private static readonly Dictionary<string, string> HandlerRegistrationExemptions = new(StringComparer.Ordinal)
	{
		["AddDispatchWithDefaults"] =
			"takes the handler assembly as a parameter and registers its handlers by name",
	};

	/// <summary>
	/// Surveys every method that invokes <c>AddDispatch</c> with at least one argument.
	/// </summary>
	/// <param name="sources">The files to search. Supplied by the caller, never discovered here.</param>
	/// <returns>The survey.</returns>
	/// <remarks>
	/// The predicate is "an <c>AddDispatch</c> invocation carrying at least one argument", which is a
	/// deliberate over-approximation of "reaches the <c>Action&lt;IDispatchBuilder&gt;</c> overload".
	/// Overload resolution needs a semantic model and this guard parses syntax only, so the choice is
	/// between over- and under-inclusion. Over-inclusion costs one allowlist entry with a written reason;
	/// under-inclusion is how the defect shipped. A zero-argument call is excluded because it resolves to
	/// the assembly overload, which discovers the entry assembly itself.
	/// </remarks>
	internal static DispatchCompositionSurvey SurveyDispatchCompositions(IEnumerable<SourceFile> sources)
	{
		ArgumentNullException.ThrowIfNull(sources);

		var compositions = new List<DispatchComposition>();
		var unattributed = 0;

		foreach (var file in sources)
		{
			var root = CSharpSyntaxTree.ParseText(file.Text).GetRoot();

			var composingCalls = root.DescendantNodes()
				.OfType<InvocationExpressionSyntax>()
				.Count(IsComposingCall);

			if (composingCalls == 0)
			{
				continue;
			}

			var attributed = 0;

			foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
			{
				var callsHere = method.DescendantNodes()
					.OfType<InvocationExpressionSyntax>()
					.Count(IsComposingCall);

				if (callsHere == 0)
				{
					continue;
				}

				attributed += callsHere;

				var namesDiscovery = method.DescendantNodes()
					.OfType<InvocationExpressionSyntax>()
					.Any(static invocation => InvokedNameOf(invocation) == "AddHandlersFromEntryAssembly");

				compositions.Add(new DispatchComposition(file.Path, NameOfMethod(method), namesDiscovery));
			}

			unattributed += composingCalls - attributed;
		}

		return new DispatchCompositionSurvey(compositions, unattributed);
	}

	private static bool IsComposingCall(InvocationExpressionSyntax invocation) =>
		InvokedNameOf(invocation) == "AddDispatch" && invocation.ArgumentList.Arguments.Count > 0;

	private static string NameOfMethod(BaseMethodDeclarationSyntax method) => method switch
	{
		MethodDeclarationSyntax named => named.Identifier.ValueText,
		ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
		_ => method.Kind().ToString(),
	};

	// ---------- SAFETY: a composing method that decides nothing is a finding ----------

	private static readonly SourceFile ComposerThatDecidesNothing = new(
		"src/Some.Package/SilentComposer.cs",
		"""
		public static class SilentComposer
		{
			public static IServiceCollection AddSomethingSilent(this IServiceCollection services) =>
				services.AddDispatch(dispatch => dispatch.UseObservability());
		}
		""");

	private static readonly SourceFile ComposerThatDecides = new(
		"src/Some.Package/DecidingComposer.cs",
		"""
		public static class DecidingComposer
		{
			public static IServiceCollection AddSomethingDeciding(
				this IServiceCollection services,
				Action<IDispatchBuilder>? configure) =>
				services.AddDispatch(configure ?? (static d => d.AddHandlersFromEntryAssembly()));
		}
		""");

	[Fact]
	public void Flag_a_composing_method_that_never_names_the_entry_assembly_discovery()
	{
		var survey = SurveyDispatchCompositions([ComposerThatDecidesNothing]);

		survey.Compositions.ShouldHaveSingleItem().NamesEntryAssemblyDiscovery.ShouldBeFalse(
			"the lambda names no handler and the overload no longer discovers any, so this composition "
			+ "registers nothing and says nothing about it");
		survey.UnattributedCallSites.ShouldBe(0);
	}

	[Fact]
	public void Clear_a_composing_method_that_names_the_entry_assembly_discovery()
	{
		SurveyDispatchCompositions([ComposerThatDecides])
			.Compositions.ShouldHaveSingleItem().NamesEntryAssemblyDiscovery.ShouldBeTrue(
				"the null branch names the discovery, so the decision is stated at the call site");
	}

	[Fact]
	public void Ignore_a_zero_argument_AddDispatch_call()
	{
		var zeroArgument = new SourceFile(
			"src/Some.Package/ZeroConfigComposer.cs",
			"""
			public static class ZeroConfigComposer
			{
				public static void Compose(IServiceCollection services) => services.AddDispatch();
			}
			""");

		SurveyDispatchCompositions([zeroArgument]).Compositions.ShouldBeEmpty(
			"a no-argument call resolves to the assembly overload, which discovers the entry assembly "
			+ "itself, so there is no decision left for the caller to make");
	}

	[Fact]
	public void Notice_a_composing_call_that_belongs_to_no_method()
	{
		var fieldInitialiser = new SourceFile(
			"src/Some.Package/InitialiserComposer.cs",
			"""
			public static class InitialiserComposer
			{
				private static readonly IServiceCollection Composed =
					new ServiceCollection().AddDispatch(static d => d.UseObservability());
			}
			""");

		SurveyDispatchCompositions([fieldInitialiser]).UnattributedCallSites.ShouldBe(
			1,
			"a call the per-method survey cannot see must be reported, not silently treated as compliant");
	}

	// ---------- the real repository ----------

	// ---------- the third guard: a subsystem that bootstraps dispatch must not SCAN on the way ----------

	// The guard above governs the lambda overload. This one governs the other end of the same seam.
	//
	// A no-argument AddDispatch() resolves to AddDispatch(params Assembly[]), which -- when the caller
	// named no assembly -- discovers handlers from Assembly.GetEntryAssembly(). That is the right
	// behaviour for a CONSUMER who called it: they asked for zero-config and they got it, and the
	// overload carries the trimming and dynamic-code annotations that say so.
	//
	// It is the wrong behaviour for one of our own subsystems bootstrapping the dispatch primitives it
	// needs. A consumer registering an outbox asked for an outbox; the reflective scan of their own entry
	// assembly is imposed on them by a package they never asked to scan anything, and it lands whether or
	// not they have already configured handlers by hand. The pipeline is what those call sites want, and
	// AddDispatchPipeline() registers exactly that -- including the handler registry -- and scans nothing.
	//
	// Entry-assembly discovery has a named, annotated, opt-in home: AddHandlersFromEntryAssembly(). A
	// composition that wants it says so. This guard asserts nothing in src/ takes it implicitly.

	/// <summary>
	/// Methods allowed to call the zero-argument <c>AddDispatch()</c>, each with the reason the implicit
	/// entry-assembly scan is correct for that call site.
	/// </summary>
	/// <remarks>
	/// Empty, and that is the intended state: no composition in <c>src/</c> currently needs the implicit
	/// scan. The dictionary exists so a future call site that genuinely does can be admitted by a written
	/// argument rather than by weakening the guard.
	/// </remarks>
	private static readonly Dictionary<string, string> ImplicitEntryAssemblyScanExemptions =
		new(StringComparer.Ordinal);

	private static bool IsImplicitlyScanningCall(InvocationExpressionSyntax invocation) =>
		InvokedNameOf(invocation) == "AddDispatch" && invocation.ArgumentList.Arguments.Count == 0;

	/// <summary>
	/// Surveys every method that invokes the zero-argument <c>AddDispatch()</c>.
	/// </summary>
	/// <param name="sources">The files to search. Supplied by the caller, never discovered here.</param>
	/// <returns>One entry per call site: the file it was found in and the method that contains it.</returns>
	internal static IReadOnlyList<string> SurveyImplicitEntryAssemblyScans(IEnumerable<SourceFile> sources)
	{
		ArgumentNullException.ThrowIfNull(sources);

		var found = new List<string>();

		foreach (var file in sources)
		{
			var root = CSharpSyntaxTree.ParseText(file.Text).GetRoot();

			foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
			{
				var callsHere = method.DescendantNodes()
					.OfType<InvocationExpressionSyntax>()
					.Count(IsImplicitlyScanningCall);

				for (var occurrence = 0; occurrence < callsHere; occurrence++)
				{
					found.Add($"{file.Path} :: {NameOfMethod(method)}");
				}
			}
		}

		return found;
	}

	// ---------- SAFETY: a bootstrapping call that scans is a finding ----------

	[Fact]
	public void Flag_a_subsystem_that_bootstraps_dispatch_with_the_scanning_overload()
	{
		var scanningBootstrap = new SourceFile(
			"src/Some.Package/ScanningBootstrap.cs",
			"""
			public static class ScanningBootstrap
			{
				public static IServiceCollection AddSomeSubsystem(this IServiceCollection services)
				{
					services.AddDispatch();
					return services;
				}
			}
			""");

		SurveyImplicitEntryAssemblyScans([scanningBootstrap]).ShouldHaveSingleItem().ShouldBe(
			"src/Some.Package/ScanningBootstrap.cs :: AddSomeSubsystem",
			"a zero-argument AddDispatch() discovers the consumer's entry assembly, which a subsystem "
			+ "bootstrapping its own dispatch primitives never asked for and cannot opt them out of");
	}

	// ---------- LIVENESS: the pipeline-only bootstrap is clean ----------

	[Fact]
	public void Clear_a_subsystem_that_bootstraps_dispatch_with_the_pipeline_only_entry_point()
	{
		var pipelineBootstrap = new SourceFile(
			"src/Some.Package/PipelineBootstrap.cs",
			"""
			public static class PipelineBootstrap
			{
				public static IServiceCollection AddSomeSubsystem(this IServiceCollection services)
				{
					services.AddDispatchPipeline();
					return services;
				}
			}
			""");

		SurveyImplicitEntryAssemblyScans([pipelineBootstrap]).ShouldBeEmpty(
			"AddDispatchPipeline registers the primitives and the handler registry and scans nothing");
	}

	// ---------- LIVENESS: an argument-carrying call is a different overload ----------

	[Fact]
	public void Ignore_an_AddDispatch_call_that_carries_an_argument()
	{
		SurveyImplicitEntryAssemblyScans([ComposerThatDecides]).ShouldBeEmpty(
			"a call carrying an argument reaches an overload that discovers nothing on its own; whether "
			+ "it decides handler registration is the neighbouring guard's question");
	}

	// ---------- LIVENESS: a mention is not a call ----------

	[Fact]
	public void Not_count_a_documented_AddDispatch_call_as_an_implicit_scan()
	{
		var documented = new SourceFile(
			"src/Some.Package/DocumentedBootstrap.cs",
			"""
			public static class DocumentedBootstrap
			{
				/// <example><code>services.AddDispatch();</code></example>
				public static IServiceCollection AddSomeSubsystem(this IServiceCollection services)
				{
					services.AddDispatchPipeline();
					return services;
				}
			}
			""");

		SurveyImplicitEntryAssemblyScans([documented]).ShouldBeEmpty(
			"an example in a doc comment invokes nothing");
	}

	// ---------- the real repository ----------

	[Fact]
	public void Refuse_an_implicit_entry_assembly_scan_anywhere_in_the_shipped_source()
	{
		var sources = ShippedSources();

		// Positive control on the same population, the same parser and the same call-site predicate
		// shape: the neighbouring survey finds AddDispatch call sites here. Without it, an empty result
		// below is indistinguishable from a survey that silently stopped reading files.
		SurveyDispatchCompositions(sources).Compositions.ShouldNotBeEmpty(
			"the control must find AddDispatch call sites in src/ or the verdict below is vacuous");

		var scanning = SurveyImplicitEntryAssemblyScans(sources)
			.Where(site => !ImplicitEntryAssemblyScanExemptions.ContainsKey(
				site[(site.LastIndexOf(" :: ", StringComparison.Ordinal) + 4)..]))
			.OrderBy(static site => site, StringComparer.Ordinal)
			.ToList();

		scanning.ShouldBeEmpty(
			"These methods call the zero-argument AddDispatch(), which discovers handlers by scanning "
			+ "the consumer's entry assembly. A subsystem bootstrapping its own dispatch primitives "
			+ "imposes that reflective scan on a consumer who asked for the subsystem, not for a scan, "
			+ "and who cannot opt out of it.\n"
			+ "  (a) the call wants the pipeline and the handler registry -> call AddDispatchPipeline(), "
			+ "which registers both and scans nothing;\n"
			+ "  (b) the call genuinely needs the consumer's entry assembly -> say so by calling "
			+ "AddHandlersFromEntryAssembly(), or add the method to "
			+ "ImplicitEntryAssemblyScanExemptions with the reason.\nScanning:\n    "
			+ string.Join("\n    ", scanning));
	}

	/// <summary>
	/// Every C# source file that ships, excluding build output.
	/// </summary>
	/// <returns>The repository-relative path and text of each file.</returns>
	private static List<SourceFile> ShippedSources()
	{
		var repositoryRoot = TestHelpers.GetRepositoryRoot();

		var sources = Directory
			.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Select(path => new SourceFile(Path.GetRelativePath(repositoryRoot, path), File.ReadAllText(path)))
			.ToList();

		sources.ShouldNotBeEmpty("the source population must be non-empty or every verdict is vacuous");

		return sources;
	}

	[Fact]
	public void Require_every_dispatch_composing_method_in_the_repository_to_decide_handler_registration()
	{
		var repositoryRoot = TestHelpers.GetRepositoryRoot();

		var sources = Directory
			.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Select(path => new SourceFile(Path.GetRelativePath(repositoryRoot, path), File.ReadAllText(path)))
			.ToList();

		sources.ShouldNotBeEmpty("the source population must be non-empty or every verdict below is vacuous");

		var survey = SurveyDispatchCompositions(sources);

		survey.Compositions.ShouldNotBeEmpty(
			"no composing method was found at all, which means the survey stopped working rather than "
			+ "that the repository stopped composing dispatch");

		survey.UnattributedCallSites.ShouldBe(
			0,
			"a composing call outside any method declaration is outside the reach of this guard; teach "
			+ "the survey to see it before trusting the verdict below");

		var undecided = survey.Compositions
			.Where(composition =>
				!composition.NamesEntryAssemblyDiscovery
				&& !HandlerRegistrationExemptions.ContainsKey(composition.MethodName))
			.Select(composition => $"{composition.Path} :: {composition.MethodName}")
			.OrderBy(static description => description, StringComparer.Ordinal)
			.ToList();

		undecided.ShouldBeEmpty(
			"These methods compose dispatch through AddDispatch(Action<IDispatchBuilder>), which no "
			+ "longer discovers handlers when the lambda names none. Each one has to decide, and the "
			+ "decision is yours to make:\n"
			+ "  (a) the caller supplied no configuration, so this composition should discover the "
			+ "handlers of the consumer application -> call AddHandlersFromEntryAssembly() on that "
			+ "branch;\n"
			+ "  (b) this composition registers its handlers by another route, or deliberately registers "
			+ "none -> add the method name to HandlerRegistrationExemptions with the reason.\n"
			+ "Leaving it as it is is neither: it ships an entry point that silently registers no "
			+ "handlers.\nUndecided:\n    " + string.Join("\n    ", undecided));
	}
}
