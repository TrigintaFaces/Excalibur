// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests;

/// <summary>
/// Structural startup-validation guard for supported registration shapes. A profile is a collection,
/// options type, and options name within a lexical registration method. Separate builders for that same
/// profile compose; different types, names, and collections do not. Validation delegates and data
/// annotations are valid alternatives to a registered <c>IValidateOptions&lt;T&gt;</c>.
/// <para>
/// The eight documented baseline exceptions retain their existing reasons. Unsupported composition is
/// reported separately with source locations; it is neither coverage nor an independently established
/// production defect. This syntax guard does not prove validator effectiveness, runtime reachability,
/// every target framework, or behavior of helpers across methods.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ValidateOnStartCoverageGuardTests
{
	private readonly ITestOutputHelper _output;

	public ValidateOnStartCoverageGuardTests(ITestOutputHelper output) => _output = output;

	private const string BaselineRelativePath = "tests/architecture/Boundary.Tests/validate-on-start-baseline.txt";

	[Fact]
	public void No_new_unvalidated_AddOptions_outside_the_baseline()
	{
		var scan = ScanRepository();
		_output.WriteLine($"Registered types: {scan.AddOptionsTypes.Count}; supported covered profiles: {scan.CoveredProfiles.Count}; " +
			$"supported gap profiles: {scan.GapProfiles.Count}; unresolved profiles: {scan.Unresolved.Count}.");
		foreach (var unresolved in scan.Unresolved)
		{
			_output.WriteLine($"UNRESOLVED {unresolved.Location}: {unresolved.OptionsType}: {unresolved.Reason}");
		}

		scan.AddOptionsTypes.Count.ShouldBeGreaterThan(100,
			"the repo-wide scan should discover the AddOptions<T> registrations; a low count means the scan or " +
			"repo-root resolution is broken (vacuous-guard protection).");

		var baseline = ReadBaseline();
		var newGaps = scan.Gaps.Where(g => !baseline.Contains(g)).OrderBy(s => s, StringComparer.Ordinal).ToList();

		newGaps.ShouldBeEmpty(
			"Supported registration profile(s) lack startup evaluation or validation, and are not documented " +
			"baseline exceptions. Validate/ValidateDataAnnotations or an actual validator service registration " +
			"can supply validation. Unresolved composition is reported separately, not counted as coverage:\n  " +
			string.Join("\n  ", newGaps));
	}

	[Fact]
	public void Baseline_entries_are_still_genuine_gaps()
	{
		var scan = ScanRepository();
		var baseline = ReadBaseline();

		var nowValidated = baseline.Where(b => !scan.Gaps.Contains(b) && !scan.Unresolved.Any(u => u.OptionsType == b))
			.OrderBy(s => s, StringComparer.Ordinal).ToList();

		nowValidated.ShouldBeEmpty(
			"baselined option type(s) are now fully validated (ValidateOnStart + IValidateOptions) — remove them from " +
			"validate-on-start-baseline.txt so the bd-ccr9eg burn-down list shrinks to empty:\n  " +
			string.Join("\n  ", nowValidated));
	}

	[Fact]
	public void Baseline_entries_are_actually_registered_options()
	{
		var scan = ScanRepository();
		var baseline = ReadBaseline();

		var stale = baseline.Where(b => !scan.AddOptionsTypes.Contains(b)).OrderBy(s => s, StringComparer.Ordinal).ToList();

		stale.ShouldBeEmpty(
			"baseline lists type(s) no longer registered via AddOptions<T> anywhere — remove the stale entries:\n  " +
			string.Join("\n  ", stale));
	}

	// ---- NFR-2 non-vacuity controls -----------------------------------------------------------------------

	[Fact]
	public void Detector_classifies_a_known_validated_and_a_synthetic_gap()
	{
		var scan = ScanRepository();

		// Positive control: LeaderElectionOptions is fully validated (ValidateOnStart + LeaderElectionOptionsValidator).
		scan.AddOptionsTypes.ShouldContain("Excalibur.Dispatch.LeaderElection.LeaderElectionOptions");
		scan.Gaps.ShouldNotContain("Excalibur.Dispatch.LeaderElection.LeaderElectionOptions",
			"LeaderElectionOptions is fully validated; the scan must not flag it (false-positive control).");
		scan.CoveredProfiles.ShouldContain(profile =>
			profile.OptionsType == "Excalibur.Dispatch.LeaderElection.LeaderElectionOptions",
			"the positive control must establish an actual supported covered profile, not merely an absent gap.");

		// Negative control: a synthetic type is registered nowhere → never a discovered gap, and is correctly absent.
		scan.AddOptionsTypes.ShouldNotContain("Zzz_NonexistentOptions_Control");
	}

	// ---- detection (single pass over src/) ----------------------------------------------------------------
	[Fact]
	public void Detector_keeps_validation_for_different_option_types_separate()
	{
		var scan = AnalyzeControl("""
			services.AddOptions<AlphaOptions>().ValidateOnStart();
			services.AddSingleton<IValidateOptions<AlphaOptions>, AlphaValidator>();
			services.AddOptions<BetaOptions>();
			services.AddSingleton<IValidateOptions<BetaOptions>, BetaValidator>();
			""");
		scan.Gaps.ShouldContain("BetaOptions");
		scan.Gaps.ShouldNotContain("AlphaOptions");
	}

	[Fact]
	public void Detector_does_not_count_a_validator_declaration_as_registration()
	{
		var scan = AnalyzeControl("services.AddOptions<AlphaOptions>().ValidateOnStart();",
			"class AlphaValidator : IValidateOptions<AlphaOptions> { }");
		scan.Gaps.ShouldContain("AlphaOptions");
	}

	[Fact]
	public void Detector_accepts_a_registered_validation_delegate()
	{
		var scan = AnalyzeControl("services.AddOptions<AlphaOptions>().Validate(o => o.Count > 0).ValidateOnStart();");
		scan.Gaps.ShouldNotContain("AlphaOptions");
	}

	[Fact]
	public void Detector_accepts_data_annotation_validation()
	{
		var scan = AnalyzeControl("services.AddOptions<AlphaOptions>().ValidateDataAnnotations().ValidateOnStart();");
		scan.Gaps.ShouldNotContain("AlphaOptions");
	}

	[Fact]
	public void Detector_recognizes_validation_on_a_local_options_builder()
	{
		var scan = AnalyzeControl("""
			var options = services.AddOptions<AlphaOptions>();
			options.Validate(o => o.Count > 0);
			options.ValidateOnStart();
			""");
		scan.Gaps.ShouldNotContain("AlphaOptions");
	}

	[Fact]
	public void Detector_recognizes_a_registered_validator_descriptor()
	{
		var scan = AnalyzeControl("""
			services.AddOptions<AlphaOptions>().ValidateOnStart();
			services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AlphaOptions>, AlphaValidator>());
			""");
		scan.Gaps.ShouldNotContain("AlphaOptions");
	}

	[Fact]
	public void Detector_ignores_registration_words_in_comments_and_strings()
	{
		var scan = AnalyzeControl("""
			// services.AddOptions<CommentOptions>().ValidateOnStart();
			var documentation = "AddOptions<StringOptions>() IValidateOptions<StringOptions> ValidateOnStart";
			services.AddOptions<AlphaOptions>();
			""");
		scan.AddOptionsTypes.Count.ShouldBe(1);
		scan.AddOptionsTypes.ShouldContain("AlphaOptions");
		scan.Gaps.ShouldContain("AlphaOptions");
	}

	[Fact]
	public void Detector_keeps_service_collections_separate()
	{
		var scan = AnalyzeControl("""
			var other = new ServiceCollection();
			services.AddOptions<AlphaOptions>().ValidateOnStart();
			other.AddSingleton<IValidateOptions<AlphaOptions>, AlphaValidator>();
			""");
		scan.Gaps.ShouldContain("AlphaOptions");
	}

	[Fact]
	public void Detector_keeps_named_options_profiles_separate()
	{
		var scan = AnalyzeControl("""
			services.AddOptions<AlphaOptions>("foo").ValidateOnStart();
			services.AddOptions<AlphaOptions>("bar");
			services.AddSingleton<IValidateOptions<AlphaOptions>, AlphaValidator>();
			""");
		scan.Gaps.ShouldContain("AlphaOptions");
	}

	[Fact]
	public void Detector_merges_separate_builders_for_the_same_options_profile()
	{
		var scan = AnalyzeControl("""
			services.AddOptions<AlphaOptions>("foo").Configure(o => o.Count = 1);
			services.AddOptions<AlphaOptions>("foo").Validate(o => o.Count > 0).ValidateOnStart();
			""");
		scan.Gaps.ShouldNotContain("AlphaOptions");
	}

	[Fact]
	public void Detector_reports_a_real_registered_gap_control()
	{
		var scan = AnalyzeControl("services.AddOptions<AlphaOptions>();");
		scan.AddOptionsTypes.ShouldContain("AlphaOptions");
		scan.Gaps.ShouldContain("AlphaOptions");
	}

	[Theory]
	[InlineData("if (flag) { services.AddSingleton<IValidateOptions<AlphaOptions>, AlphaValidator>(); }")]
	[InlineData("foreach (var item in items) { services.AddSingleton<IValidateOptions<AlphaOptions>, AlphaValidator>(); }")]
	[InlineData("switch (mode) { case 1: services.AddSingleton<IValidateOptions<AlphaOptions>, AlphaValidator>(); break; }")]
	public void Detector_reports_conditional_validator_registration_as_unresolved(string registration)
	{
		var scan = AnalyzeControl("services.AddOptions<AlphaOptions>().ValidateOnStart(); " + registration);
		scan.CoveredProfiles.ShouldBeEmpty();
		scan.GapProfiles.ShouldBeEmpty();
		scan.Unresolved.ShouldContain(profile => profile.OptionsType == "AlphaOptions" &&
			profile.Reason.Contains("conditional, loop, or lambda", StringComparison.Ordinal));
	}

	[Fact]
	public void Detector_reports_service_collection_reassignment_as_unresolved()
	{
		var scan = AnalyzeControl("""
			services.AddOptions<AlphaOptions>().ValidateOnStart();
			services = new ServiceCollection();
			services.AddOptions<AlphaOptions>().Validate(o => o.Count > 0);
			""");
		scan.CoveredProfiles.ShouldBeEmpty();
		scan.GapProfiles.ShouldBeEmpty();
		scan.Unresolved.ShouldContain(profile => profile.OptionsType == "AlphaOptions" &&
			profile.Reason.Contains("collection identifier is reassigned", StringComparison.Ordinal));
	}

	[Fact]
	public void Detector_reports_sibling_block_builder_shadowing_as_unresolved()
	{
		var scan = AnalyzeControl("""
			{ var options = services.AddOptions<AlphaOptions>(); }
			{ var options = services.AddOptions<BetaOptions>(); options.Validate(o => true).ValidateOnStart(); }
			""");
		scan.CoveredProfiles.ShouldBeEmpty();
		scan.GapProfiles.ShouldBeEmpty();
		scan.Unresolved.ShouldContain(profile => profile.OptionsType == "AlphaOptions" &&
			profile.Reason.Contains("shadowed local options builder", StringComparison.Ordinal));
	}

	[Fact]
	public void Detector_accepts_a_validator_instance_registered_directly()
	{
		var scan = AnalyzeControl("""
			services.AddOptions<AlphaOptions>().ValidateOnStart();
			services.AddSingleton<IValidateOptions<AlphaOptions>>(new AlphaValidator());
			""");
		scan.CoveredProfiles.ShouldContain(profile => profile.OptionsType == "AlphaOptions");
		scan.GapProfiles.ShouldBeEmpty();
		scan.Unresolved.ShouldBeEmpty();
	}

	[Fact]
	public void Detector_accepts_a_validator_instance_descriptor_registered_on_the_collection()
	{
		var scan = AnalyzeControl("""
			services.AddOptions<AlphaOptions>().ValidateOnStart();
			services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AlphaOptions>>(new AlphaValidator()));
			""");
		scan.CoveredProfiles.ShouldContain(profile => profile.OptionsType == "AlphaOptions");
		scan.GapProfiles.ShouldBeEmpty();
		scan.Unresolved.ShouldBeEmpty();
	}

	[Fact]
	public void Detector_does_not_count_an_unregistered_validator_instance_descriptor()
	{
		var scan = AnalyzeControl("""
			services.AddOptions<AlphaOptions>().ValidateOnStart();
			var unused = ServiceDescriptor.Singleton<IValidateOptions<AlphaOptions>>(new AlphaValidator());
			""");
		scan.CoveredProfiles.ShouldBeEmpty();
		scan.Gaps.ShouldContain("AlphaOptions");
		scan.Unresolved.ShouldBeEmpty();
	}

	[Fact]
	public void Detector_keeps_a_conditional_validator_instance_unresolved()
	{
		var scan = AnalyzeControl("""
			services.AddOptions<AlphaOptions>().ValidateOnStart();
			if (flag) { services.AddSingleton<IValidateOptions<AlphaOptions>>(new AlphaValidator()); }
			""");
		scan.CoveredProfiles.ShouldBeEmpty();
		scan.GapProfiles.ShouldBeEmpty();
		scan.Unresolved.ShouldContain(profile => profile.OptionsType == "AlphaOptions");
	}

	[Fact]
	public void Detector_keeps_validator_instances_on_different_collections_separate()
	{
		var scan = AnalyzeControl("""
			var other = new ServiceCollection();
			services.AddOptions<AlphaOptions>().ValidateOnStart();
			other.AddSingleton<IValidateOptions<AlphaOptions>>(new AlphaValidator());
			""");
		scan.CoveredProfiles.ShouldBeEmpty();
		scan.Gaps.ShouldContain("AlphaOptions");
		scan.Unresolved.ShouldBeEmpty();
	}

	[Fact]
	public void Detector_keeps_cross_method_validator_instances_unresolved()
	{
		var scan = AnalyzeControl("services.AddOptions<AlphaOptions>().ValidateOnStart(); RegisterValidator(services);", """
			class Helper {
				void RegisterValidator(IServiceCollection services) {
					services.AddSingleton<IValidateOptions<AlphaOptions>>(new AlphaValidator());
				}
			}
			""");
		scan.CoveredProfiles.ShouldBeEmpty();
		scan.GapProfiles.ShouldBeEmpty();
		scan.Unresolved.ShouldContain(profile => profile.OptionsType == "AlphaOptions" &&
			profile.Reason.Contains("another lexical scope", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("services.AddSingleton<IValidateOptions<AlphaOptions>>(provider => new AlphaValidator());")]
	[InlineData("var validator = new AlphaValidator(); services.AddSingleton<IValidateOptions<AlphaOptions>>(validator);")]
	[InlineData("services.AddSingleton(typeof(IValidateOptions<AlphaOptions>), typeof(AlphaValidator));")]
	public void Detector_reports_unsupported_validator_registration_forms_as_unresolved(string registration)
	{
		var scan = AnalyzeControl("services.AddOptions<AlphaOptions>().ValidateOnStart(); " + registration);
		scan.CoveredProfiles.ShouldBeEmpty();
		scan.GapProfiles.ShouldBeEmpty();
		scan.Unresolved.ShouldContain(profile => profile.OptionsType == "AlphaOptions" &&
			profile.Reason.Contains("unsupported validator registration", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("Validate(o => o.Count > 0)")]
	[InlineData("ValidateDataAnnotations()")]
	public void Detector_keeps_cross_method_builder_validation_unresolved(string validation)
	{
		var scan = AnalyzeControl("services.AddOptions<AlphaOptions>().ValidateOnStart(); RegisterValidator(services);",
			"class Helper { void RegisterValidator(IServiceCollection services) { services.AddOptions<AlphaOptions>()." + validation + "; } }");
		scan.CoveredProfiles.ShouldBeEmpty();
		scan.GapProfiles.ShouldBeEmpty();
		scan.Unresolved.ShouldContain(profile => profile.OptionsType == "AlphaOptions" &&
			profile.Reason.Contains("builder validation exists in another lexical scope", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("// services.AddOptions<AlphaOptions>().Validate(o => true).ValidateOnStart();")]
	[InlineData("var mention = \"services.AddOptions<AlphaOptions>().Validate(o => true).ValidateOnStart();\";")]
	[InlineData("var unrelated = new AlphaOptions(); unrelated.Validate(o => true);")]
	[InlineData("services.AddOptions<BetaOptions>().Validate(o => true).ValidateOnStart();")]
	public void Detector_does_not_treat_unrelated_validation_words_as_helper_evidence(string evidence)
	{
		var scan = AnalyzeControl("services.AddOptions<AlphaOptions>().ValidateOnStart();",
			"class Helper { void RegisterValidator(IServiceCollection services) {\n" + evidence + "\n} }");
		scan.CoveredProfiles.ShouldNotContain(profile => profile.OptionsType == "AlphaOptions");
		scan.Gaps.ShouldContain("AlphaOptions");
		scan.Unresolved.ShouldNotContain(profile => profile.OptionsType == "AlphaOptions");
	}

	private static ScanResult AnalyzeControl(string statements, string declarations = "") => AnalyzeSources(
		[("control.cs", "class AlphaOptions { public int Count { get; set; } } class BetaOptions { } " +
			"class Composition { void Register(IServiceCollection services) { " + statements + " } } " + declarations)]);

	private sealed record ProfileKey(string Scope, string Collection, string OptionsType, string Name);
	private sealed record UnresolvedProfile(string OptionsType, string Location, string Reason);
	private sealed record ValidatorRegistration(string Scope, string Collection, string OptionsType, string? Problem);

	private sealed class Profile(ProfileKey key, string location)
	{
		public ProfileKey Key { get; } = key;
		public string Location { get; } = location;
		public bool HasStartup { get; set; }
		public bool HasValidation { get; set; }
		public bool HasBuilderValidation { get; set; }
		public HashSet<string> Problems { get; } = new(StringComparer.Ordinal);
	}

	private sealed record ScanResult(
		IReadOnlySet<string> AddOptionsTypes,
		IReadOnlySet<string> Gaps,
		IReadOnlyList<ProfileKey> CoveredProfiles,
		IReadOnlyList<ProfileKey> GapProfiles,
		IReadOnlyList<UnresolvedProfile> Unresolved);

	private static ScanResult ScanRepository()
	{
		var root = TestHelpers.GetRepositoryRoot();
		var srcDir = Path.Combine(root, "src");
		return AnalyzeSources(EnumerateSourceFiles(srcDir).Select(file => (file, File.ReadAllText(file))));
	}

	private static ScanResult AnalyzeSources(IEnumerable<(string Path, string Text)> sources)
	{
		var files = sources.Select(file => (file.Path, Root: CSharpSyntaxTree.ParseText(file.Text).GetRoot())).ToArray();
		var typeNames = files.SelectMany(file => file.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
			.Select(TypeIdentity).Distinct(StringComparer.Ordinal).ToArray();
		var profiles = new Dictionary<ProfileKey, Profile>();
		var validators = new List<ValidatorRegistration>();
		var unresolved = new List<UnresolvedProfile>();
		var registered = new HashSet<string>(StringComparer.Ordinal);

		foreach (var file in files)
		{
			var invocations = file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
			foreach (var call in invocations.Where(call => InvokedName(call) == "AddOptions"))
			{
				if (call.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } member ||
					generic.TypeArgumentList.Arguments.Count != 1)
				{
					unresolved.Add(new("<unknown>", Location(file.Path, call), "unsupported AddOptions invocation shape"));
					continue;
				}

				var type = ResolveType(generic.TypeArgumentList.Arguments[0], call, typeNames);
				registered.Add(type ?? generic.TypeArgumentList.Arguments[0].ToString());
				var scope = Scope(file.Path, call);
				var name = OptionsName(call);
				if (type is null || scope is null || name is null || !IsStableCollection(member.Expression))
				{
					unresolved.Add(new(type ?? generic.TypeArgumentList.Arguments[0].ToString(), Location(file.Path, call),
						"unresolved type, lexical scope, collection expression, or dynamic options name"));
					continue;
				}

				var key = new ProfileKey(scope, member.Expression.ToString(), type, name);
				if (!profiles.TryGetValue(key, out var profile))
				{
					profile = new(key, Location(file.Path, call));
					profiles.Add(key, profile);
				}

				MarkUncertainControlFlow(profile, call, file.Root);
				MarkUnstableCollection(profile, member.Expression, call);
				var outermost = ApplyChain(profile, call);
				if (outermost.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable })
				{
					ApplyLocalBuilder(profile, variable, invocations);
				}
			}

			foreach (var call in invocations)
			{
				var validator = ReadValidator(call, file.Path, typeNames);
				if (validator is not null)
				{
					validators.Add(validator);
				}
			}
		}

		var covered = new List<ProfileKey>();
		var gaps = new List<ProfileKey>();
		foreach (var profile in profiles.Values)
		{
			foreach (var validator in validators.Where(v => v.Scope == profile.Key.Scope &&
				v.Collection == profile.Key.Collection && v.OptionsType == profile.Key.OptionsType))
			{
				if (validator.Problem is null)
				{
					profile.HasValidation = true;
				}
				else
				{
					profile.Problems.Add(validator.Problem);
				}
			}
			// A helper may contribute the missing registration. Syntax cannot prove its call path or
			// collection identity, so cross-method evidence is unresolved rather than global coverage.
			if (!profile.HasValidation && validators.Any(v => v.OptionsType == profile.Key.OptionsType && v.Scope != profile.Key.Scope))
			{
				profile.Problems.Add("validator registration exists in another lexical scope; helper composition not proven");
			}
			if (!profile.HasValidation && profiles.Values.Any(p => p.Key.OptionsType == profile.Key.OptionsType &&
				p.Key.Scope != profile.Key.Scope && p.HasBuilderValidation))
			{
				profile.Problems.Add("builder validation exists in another lexical scope; helper composition not proven");
			}
			if (!profile.HasStartup && profiles.Values.Any(p => p.Key.OptionsType == profile.Key.OptionsType &&
				p.Key.Scope != profile.Key.Scope && p.HasStartup))
			{
				profile.Problems.Add("startup evaluation exists in another lexical scope; helper composition not proven");
			}

			if (profile.Problems.Count != 0)
			{
				unresolved.Add(new(profile.Key.OptionsType, profile.Location,
					string.Join("; ", profile.Problems.OrderBy(reason => reason, StringComparer.Ordinal))));
			}
			else if (profile.HasStartup && profile.HasValidation)
			{
				covered.Add(profile.Key);
			}
			else
			{
				gaps.Add(profile.Key);
			}
		}

		return new(registered, gaps.Select(profile => profile.OptionsType).ToHashSet(StringComparer.Ordinal),
			covered, gaps, unresolved);
	}

	private static string TypeIdentity(BaseTypeDeclarationSyntax declaration)
	{
		var namespaces = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString());
		var containingTypes = declaration.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.ValueText);
		return string.Join(".", namespaces.Concat(containingTypes).Append(declaration.Identifier.ValueText));
	}

	private static string? ResolveType(TypeSyntax type, SyntaxNode site, IReadOnlyList<string> typeNames)
	{
		var name = type.ToString().Replace("global::", string.Empty, StringComparison.Ordinal);
		if (name.Contains('.'))
		{
			return typeNames.Contains(name, StringComparer.Ordinal) ? name : null;
		}

		var namespaces = site.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString());
		var current = string.Join(".", namespaces);
		var imports = site.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>()
			.Where(u => u.Alias is null && !u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
			.Select(u => u.Name?.ToString()).Where(n => n is not null).Select(n => n!).ToArray();
		var candidates = typeNames.Where(t => t == name || t == current + "." + name || imports.Any(i => t == i + "." + name)).ToArray();
		if (candidates.Length == 1)
		{
			return candidates[0];
		}

		// The one documented external baseline type is not declared in the source corpus.
		return name == "HttpStandardResilienceOptions" && imports.Contains("Microsoft.Extensions.Http.Resilience", StringComparer.Ordinal)
			? "Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions"
			: null;
	}

	private static string? Scope(string path, SyntaxNode node)
	{
		var method = node.Ancestors().FirstOrDefault(n => n is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax);
		return method is null ? null : path + ":" + method.SpanStart;
	}

	private static string Location(string path, SyntaxNode node)
	{
		var position = node.GetLocation().GetLineSpan().StartLinePosition;
		return $"{path}:{position.Line + 1}:{position.Character + 1} (span {node.SpanStart}..{node.Span.End})";
	}

	private static string? OptionsName(InvocationExpressionSyntax call) => call.ArgumentList.Arguments.Count switch
	{
		0 => string.Empty,
		1 when call.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
			=> literal.Token.ValueText,
		_ => null,
	};

	private static bool IsStableCollection(ExpressionSyntax expression) => expression is IdentifierNameSyntax ||
		expression is MemberAccessExpressionSyntax member && IsStableCollection(member.Expression);

	private static string? InvokedName(InvocationExpressionSyntax call) => call.Expression switch
	{
		MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
		IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
		GenericNameSyntax generic => generic.Identifier.ValueText,
		_ => null,
	};

	private static InvocationExpressionSyntax ApplyChain(Profile profile, InvocationExpressionSyntax call)
	{
		var current = call;
		while (current.Parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax parent } member &&
			member.Expression == current)
		{
			ApplyBuilderOperation(profile, parent);
			current = parent;
		}
		return current;
	}

	private static void ApplyBuilderOperation(Profile profile, InvocationExpressionSyntax call)
	{
		switch (InvokedName(call))
		{
			case "ValidateOnStart":
				profile.HasStartup = true;
				break;
			case "Validate" when call.ArgumentList.Arguments.Count > 0:
			case "ValidateDataAnnotations":
				profile.HasValidation = true;
				profile.HasBuilderValidation = true;
				break;
			case "Bind":
			case "BindConfiguration":
			case "Configure":
			case "PostConfigure":
				break;
			default:
				profile.Problems.Add("unsupported options-builder operation " + InvokedName(call));
				break;
		}
	}

	private static void ApplyLocalBuilder(Profile profile, VariableDeclaratorSyntax variable,
		IReadOnlyList<InvocationExpressionSyntax> invocations)
	{
		var method = variable.Ancestors().First(n => n is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax);
		if (method.DescendantNodes().OfType<VariableDeclaratorSyntax>()
			.Count(v => v.Identifier.ValueText == variable.Identifier.ValueText) > 1)
		{
			profile.Problems.Add("shadowed local options builder requires lexical binding evidence");
			return;
		}
		if (method.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(a => a.Left.ToString() == variable.Identifier.ValueText))
		{
			profile.Problems.Add("local options builder is reassigned");
			return;
		}
		foreach (var call in invocations.Where(call => call.SpanStart > variable.SpanStart &&
			call.Ancestors().FirstOrDefault(n => n is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax) == method &&
			call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax identifier } &&
			identifier.Identifier.ValueText == variable.Identifier.ValueText))
		{
			MarkUncertainControlFlow(profile, call, method.SyntaxTree.GetRoot());
			ApplyBuilderOperation(profile, call);
			_ = ApplyChain(profile, call);
		}
	}

	private static void MarkUncertainControlFlow(Profile profile, SyntaxNode call, SyntaxNode root)
	{
		if (UncertainRegistrationPath(call))
		{
			profile.Problems.Add("conditional, loop, or lambda registration path requires runtime evidence");
		}
		if (root.DescendantTrivia(descendIntoTrivia: true).Any(t => t.IsKind(SyntaxKind.IfDirectiveTrivia)))
		{
			profile.Problems.Add("conditional compilation requires target-specific evidence");
		}
	}

	private static bool UncertainRegistrationPath(SyntaxNode call) => call.Ancestors().Any(n =>
		n is IfStatementSyntax or SwitchStatementSyntax or ConditionalExpressionSyntax or AnonymousFunctionExpressionSyntax or
		ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax or WhileStatementSyntax or DoStatementSyntax);

	private static void MarkUnstableCollection(Profile profile, ExpressionSyntax collection, SyntaxNode call)
	{
		var method = call.Ancestors().FirstOrDefault(n => n is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax);
		if (method is null)
		{
			return;
		}
		var rootIdentifier = collection.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().FirstOrDefault()?.Identifier.ValueText;
		if (rootIdentifier is null)
		{
			return;
		}
		if (method.DescendantNodes().OfType<AssignmentExpressionSyntax>()
			.Any(a => a.Left.ToString() == rootIdentifier || a.Left.ToString() == collection.ToString()))
		{
			profile.Problems.Add("collection identifier is reassigned; object identity requires data-flow evidence");
		}
		var declarations = method.DescendantNodes().OfType<VariableDeclaratorSyntax>()
			.Where(v => v.Identifier.ValueText == rootIdentifier).ToArray();
		if (declarations.Length > 1)
		{
			profile.Problems.Add("shadowed collection identifier requires lexical binding evidence");
		}
		if (declarations.Any(v => v.Initializer?.Value is IdentifierNameSyntax or MemberAccessExpressionSyntax))
		{
			profile.Problems.Add("collection alias requires object-identity evidence");
		}
	}

	private static ValidatorRegistration? ReadValidator(InvocationExpressionSyntax call, string path,
		IReadOnlyList<string> typeNames)
	{
		if (call.Expression is not MemberAccessExpressionSyntax member || !IsStableCollection(member.Expression))
		{
			return null;
		}
		var methodName = member.Name.Identifier.ValueText;
		TypeSyntax? serviceType = null;
		string? unsupported = null;
		if (methodName is "AddSingleton" or "AddTransient" or "AddScoped" or "TryAddSingleton" or "TryAddTransient" or "TryAddScoped" &&
			member.Name is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count > 0)
		{
			serviceType = generic.TypeArgumentList.Arguments[0];
			if (!(generic.TypeArgumentList.Arguments.Count == 2 && call.ArgumentList.Arguments.Count == 0) &&
				!(methodName == "AddSingleton" && generic.TypeArgumentList.Arguments.Count == 1 &&
					call.ArgumentList.Arguments.Count == 1 && call.ArgumentList.Arguments[0].Expression is ObjectCreationExpressionSyntax))
			{
				unsupported = "unsupported validator registration: factory, variable, or lifetime shape";
			}
		}
		else if (methodName is "TryAddEnumerable" or "Add" && call.ArgumentList.Arguments.Count == 1)
		{
			var expression = call.ArgumentList.Arguments[0].Expression;
			if (expression is IdentifierNameSyntax variable)
			{
				var method = call.Ancestors().FirstOrDefault(n => n is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax);
				var declarations = method?.DescendantNodes().OfType<VariableDeclaratorSyntax>()
					.Where(v => v.Identifier.ValueText == variable.Identifier.ValueText).ToArray();
				if (declarations is { Length: 1 } && declarations[0].Initializer is not null)
				{
					expression = declarations[0].Initializer!.Value;
					unsupported = "unsupported validator registration: descriptor variable identity requires data-flow evidence";
				}
			}
			if (expression is InvocationExpressionSyntax
				{
					Expression: MemberAccessExpressionSyntax
					{
						Expression: IdentifierNameSyntax { Identifier.ValueText: "ServiceDescriptor" },
						Name: GenericNameSyntax descriptor,
					},
				} descriptorCall && descriptor.TypeArgumentList.Arguments.Count > 0)
			{
				serviceType = descriptor.TypeArgumentList.Arguments[0];
				var supportedDescriptor = descriptor.Identifier.ValueText is "Singleton" or "Transient" or "Scoped" &&
					descriptor.TypeArgumentList.Arguments.Count == 2 && descriptorCall.ArgumentList.Arguments.Count == 0 ||
					descriptor.Identifier.ValueText == "Singleton" && descriptor.TypeArgumentList.Arguments.Count == 1 &&
					descriptorCall.ArgumentList.Arguments.Count == 1 &&
					descriptorCall.ArgumentList.Arguments[0].Expression is ObjectCreationExpressionSyntax;
				if (!supportedDescriptor)
				{
					unsupported = "unsupported validator registration: descriptor factory, variable, or lifetime shape";
				}
			}
			else if (expression is InvocationExpressionSyntax descriptorInvocation &&
				descriptorInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax descriptorType)
			{
				serviceType = descriptorType.Type;
				unsupported = "unsupported validator registration: typeof descriptor service shape";
			}
		}
		else if (methodName is "AddSingleton" or "AddTransient" or "AddScoped" or "TryAddSingleton" or "TryAddTransient" or "TryAddScoped" &&
			call.ArgumentList.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax service)
		{
			serviceType = service.Type;
			unsupported = "unsupported validator registration: typeof service shape";
		}

		var validatorType = serviceType switch
		{
			GenericNameSyntax genericType => genericType,
			QualifiedNameSyntax { Right: GenericNameSyntax genericType } => genericType,
			_ => null,
		};
		if (validatorType is not { Identifier.ValueText: "IValidateOptions", TypeArgumentList.Arguments.Count: 1 })
		{
			return null;
		}
		var type = ResolveType(validatorType.TypeArgumentList.Arguments[0], call, typeNames);
		var scope = Scope(path, call);
		if (type is null || scope is null)
		{
			return null;
		}
		var evidence = new Profile(new(scope, member.Expression.ToString(), type, string.Empty), Location(path, call));
		if (unsupported is not null)
		{
			evidence.Problems.Add(unsupported);
		}
		MarkUncertainControlFlow(evidence, call, call.SyntaxTree.GetRoot());
		MarkUnstableCollection(evidence, member.Expression, call);
		return new(scope, member.Expression.ToString(), type,
			evidence.Problems.Count == 0 ? null : string.Join("; ", evidence.Problems.OrderBy(reason => reason, StringComparer.Ordinal)));
	}

	private static IEnumerable<string> EnumerateSourceFiles(string dir) =>
		Directory.Exists(dir)
			? Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).Where(p => !IsGenerated(p))
			: [];

	private static bool IsGenerated(string path)
	{
		var n = path.Replace('\\', '/');
		return n.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
			|| n.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
	}

	private static HashSet<string> ReadBaseline()
	{
		var root = TestHelpers.GetRepositoryRoot();
		var path = Path.Combine(root, BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));
		File.Exists(path).ShouldBeTrue($"ValidateOnStart baseline file not found at {BaselineRelativePath}.");

		return File.ReadAllLines(path)
			.Select(l => l.Trim())
			.Where(l => l.Length > 0 && !l.StartsWith('#'))
			.ToHashSet(StringComparer.Ordinal);
	}
}
