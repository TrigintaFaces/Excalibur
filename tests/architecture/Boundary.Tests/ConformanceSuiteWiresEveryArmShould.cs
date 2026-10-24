// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
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
/// xunit's AOT generator supports test methods declared in source, but excludes methods inherited from
/// a referenced assembly. PR <see href="https://github.com/xunit/xunit/pull/3627">xunit/xunit#3627</see>
/// fixes source-only inheritance cases; its referenced-assembly exclusion remains intentional. The
/// maintainer reports that fix in 4.0.2-pre.5; this repository pins xunit.v3 4.0.1 in
/// Directory.Packages.props. Declaring wrappers in the consumer suite's source avoids relying on
/// discovery of attributed arms from the kit assembly. This does not make the reflection-based
/// completeness helper trim-safe: that helper is documented for an untrimmed test host.
/// </para>
/// <para>
/// <b>Why a baseline rather than a sweep.</b> The unwired set is not one population: most of it is a
/// family tracked as its own work item, and a large part is kit <i>probes</i> that derive a kit in order to
/// test it and deliberately omit an arm so a guard has something to catch. Asserting completeness on a
/// probe would assert the opposite of its purpose. Exact verified probes are identified below; the
/// baseline records the remaining historical populations, so a reader can
/// tell a deliberate exemption from a forgotten one — which was the original defect, one level up again.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
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

	internal sealed record SuiteDebt(string Identity, IReadOnlyList<string> SourcePaths);

	/// <summary>
	/// Returns each owning type that derives a kit without wiring the completeness guard.
	/// </summary>
	/// <param name="sources">The files to inspect. Supplied by the caller, never discovered here.</param>
	/// <param name="owningProjects">Real source ownership by normalized path; omitted for synthetic controls.</param>
	/// <returns>Distinct owning-project and full-type identities, with source paths only as diagnostics.</returns>
	/// <remarks>
	/// The base list is read from the syntax tree rather than matched textually: a C# base list routinely
	/// wraps onto the line after the class name, and a line-oriented pattern misses those declarations
	/// entirely while returning a confident count. Measured on this repository, a line-based form saw 143
	/// of 183 such declarations — the 40 it could not see were the wrapped ones.
	/// </remarks>
	internal static IReadOnlyList<SuiteDebt> SurveyUnwiredSuites(
		IEnumerable<SourceFile> sources,
		IReadOnlyDictionary<string, string>? owningProjects = null)
	{
		ArgumentNullException.ThrowIfNull(sources);
		string ProjectOf(SourceFile file) => owningProjects is not null
			? owningProjects[Normalize(file.Path)] : SyntheticProjectOf(file.Path);
		var parsed = sources.Select(file => (File: file,
			Root: CSharpSyntaxTree.ParseText(file.Text, path: Normalize(file.Path)).GetCompilationUnitRoot())).ToList();
		var globalAliases = parsed.GroupBy(item => ProjectOf(item.File)).ToDictionary(group => group.Key,
			group => group.SelectMany(item => item.Root.Usings)
				.Where(item => item.Alias is not null && item.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)).ToList());
		var declarations = parsed.SelectMany(item => item.Root
			.DescendantNodes().OfType<ClassDeclarationSyntax>()
			.Select(declaration => (item.File, Declaration: declaration)));
		var unwired = new List<SuiteDebt>();
		foreach (var suite in declarations.GroupBy(item => (
			Project: ProjectOf(item.File),
			Type: QualifiedTypeOf(item.Declaration))))
		{
			var deriving = suite.Where(item => DerivesAConformanceKit(item.Declaration, globalAliases[suite.Key.Project])
				&& !item.Declaration.Modifiers.Any(token => token.IsKind(SyntaxKind.AbstractKeyword))).ToList();
			if (deriving.Count == 0 || suite.Any(item => item.Declaration.Modifiers.Any(token => token.IsKind(SyntaxKind.AbstractKeyword)))
				|| suite.All(item => IsVerifiedMutationProbe(item.File, item.Declaration)))
			{
				continue;
			}

			// Partial declarations share wrappers only within their owning project. A different suite,
			// nested helper, external receiver or unattributed method cannot discharge this obligation.
			var shadowsGuard = suite.Any(item => DeclaresGuardMember(item.Declaration));
			if (!suite.Any(item => WiresOwnGuard(item.Declaration, shadowsGuard)))
			{
				unwired.Add(new SuiteDebt($"{suite.Key.Project}|{suite.Key.Type}", deriving
					.Select(item => Normalize(item.File.Path)).Distinct(StringComparer.Ordinal)
					.OrderBy(path => path, StringComparer.Ordinal).ToList()));
			}
		}
		return unwired.OrderBy(item => item.Identity, StringComparer.Ordinal).ToList();
	}

	private static IReadOnlyList<SuiteDebt> UnlistedSuites(IEnumerable<SuiteDebt> unwired, ISet<string> baseline) =>
		unwired.Where(suite => !baseline.Contains(suite.Identity)).ToList();

	private static IReadOnlyList<string> ObsoleteBaselineEntries(IEnumerable<SuiteDebt> unwired, ISet<string> baseline)
	{
		var current = unwired.Select(suite => suite.Identity).ToHashSet(StringComparer.Ordinal);
		return baseline.Where(identity => !current.Contains(identity)).ToList();
	}

	// Synthetic controls use tests/<category>/<project>/...; real-tree callers supply the nearest
	// csproj directory envelope. This is a source survey, not an evaluated MSBuild Compile population:
	// linked files, Compile Remove, conditional imports and generated sources are outside that envelope.
	// Explicit ownership maps can describe injected linked-source controls.
	private static string SyntheticProjectOf(string path) => string.Join('/', Normalize(path).Split('/').Take(3));

	// The namespace/type delimiter and nested-type delimiter are distinct. A namespace called
	// Probe.Outer must not collide with class Outer in namespace Probe. Every containing declaration
	// participates, including structs, interfaces, records and record structs, with generic arity.
	private static string QualifiedTypeOf(ClassDeclarationSyntax declaration)
	{
		var containingTypes = declaration.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Reverse().ToList();
		var identity = string.Join('.', declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse()
			.Select(item => item.Name.ToString()))
			+ "::" + string.Join('+', containingTypes.Select(item => item.Identifier.ValueText
				+ (item.TypeParameterList is null ? "" : $"`{item.TypeParameterList.Parameters.Count}")));
		// File-local declarations, including their nested types, are distinct across source files.
		// Partial declarations in the same source tree still share this normalized discriminator.
		return containingTypes.Any(type => type.Modifiers.Any(token => token.IsKind(SyntaxKind.FileKeyword)))
			? $"{identity}|file:{Normalize(declaration.SyntaxTree.FilePath)}" : identity;
	}

	private static bool DeclaresGuardMember(ClassDeclarationSyntax declaration) => declaration.Members.Any(member => member switch
	{
		MethodDeclarationSyntax method => method.Identifier.ValueText == CompletenessGuard,
		PropertyDeclarationSyntax property => property.Identifier.ValueText == CompletenessGuard,
		FieldDeclarationSyntax field => field.Declaration.Variables.Any(variable => variable.Identifier.ValueText == CompletenessGuard),
		EventFieldDeclarationSyntax field => field.Declaration.Variables.Any(variable => variable.Identifier.ValueText == CompletenessGuard),
		_ => false,
	});

	// This syntax guard cannot prove an intermediate base override calls the kit implementation.
	// An explicit base call discharges the local wrapper obligation; behavioral kit tests remain
	// responsible for the inherited implementation. Implicit/this calls cannot bypass a local shadow.
	private static bool WiresOwnGuard(ClassDeclarationSyntax declaration, bool shadowsGuard) => declaration.Members
		.OfType<MethodDeclarationSyntax>()
		.Where(method => method.Modifiers.Any(token => token.IsKind(SyntaxKind.PublicKeyword))
			&& !method.Modifiers.Any(token => token.IsKind(SyntaxKind.StaticKeyword))
			&& method.AttributeLists.SelectMany(list => list.Attributes)
				.Any(IsTestAttribute)
			&& !method.AttributeLists.SelectMany(list => list.Attributes).Where(IsTestAttribute).Any(HasSkipOption))
		.Any(method => method.DescendantNodes(node => node is not AnonymousFunctionExpressionSyntax
			and not LocalFunctionStatementSyntax).OfType<InvocationExpressionSyntax>()
			.Any(invocation => invocation.ArgumentList.Arguments.Count == 0 && (invocation.Expression switch
			{
				IdentifierNameSyntax identifier => identifier.Identifier.ValueText == CompletenessGuard
					&& !shadowsGuard && !DeclaresLocalGuardIdentifier(method),
				MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == CompletenessGuard
					&& (member.Expression is BaseExpressionSyntax || member.Expression is ThisExpressionSyntax && !shadowsGuard),
				_ => false,
			})));

	// Any same-named local declaration makes a bare call ambiguous in this syntax survey. A wrapper
	// can use this/base to make its receiver explicit, without requiring semantic compilation here.
	private static bool DeclaresLocalGuardIdentifier(MethodDeclarationSyntax method) => method.DescendantNodes().Any(node => node switch
	{
		LocalFunctionStatementSyntax local => local.Identifier.ValueText == CompletenessGuard,
		VariableDeclaratorSyntax variable => variable.Identifier.ValueText == CompletenessGuard,
		ParameterSyntax parameter => parameter.Identifier.ValueText == CompletenessGuard,
		SingleVariableDesignationSyntax designation => designation.Identifier.ValueText == CompletenessGuard,
		ForEachStatementSyntax loop => loop.Identifier.ValueText == CompletenessGuard,
		CatchDeclarationSyntax caught => caught.Identifier.ValueText == CompletenessGuard,
		FromClauseSyntax clause => clause.Identifier.ValueText == CompletenessGuard,
		JoinClauseSyntax clause => clause.Identifier.ValueText == CompletenessGuard,
		JoinIntoClauseSyntax clause => clause.Identifier.ValueText == CompletenessGuard,
		LetClauseSyntax clause => clause.Identifier.ValueText == CompletenessGuard,
		QueryContinuationSyntax clause => clause.Identifier.ValueText == CompletenessGuard,
		_ => false,
	});

	private static bool IsTestAttribute(AttributeSyntax attribute) =>
		SimpleNameOf(attribute.Name) is "Fact" or "FactAttribute" or "Theory" or "TheoryAttribute";

	private static bool HasSkipOption(AttributeSyntax attribute) => attribute.ArgumentList?.Arguments.Any(argument =>
		argument.NameEquals?.Name.Identifier.ValueText switch
		{
			"Explicit" => !argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression),
			"Skip" => !argument.Expression.IsKind(SyntaxKind.NullLiteralExpression),
			"SkipUnless" or "SkipWhen" or "SkipType" or "SkipExceptions" => true,
			_ => false,
		}) == true;

	// These exact private types are instruments for externally attributed mutation tests, not
	// provider suites. CompletionProbe exercises completion faults. The suite-wiring probes test
	// pass/fail and the missing arm's name; their factories throw because guard reflection must not
	// resolve a store. No new name, path, base, or attributed probe inherits an exemption.
	private static readonly IReadOnlyDictionary<string, string> SuiteWiringProbeBases = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["FullyWiredEventStoreProbe"] = "EventStoreConformanceTestKit",
		["PartiallyWiredEventStoreProbe"] = "EventStoreConformanceTestKit",
		["FullyWiredSagaStoreProbe"] = "SagaStoreConformanceTestKit",
		["PartiallyWiredSagaStoreProbe"] = "SagaStoreConformanceTestKit",
		["FullyWiredInboxStoreProbe"] = "InboxStoreConformanceTestKit",
		["PartiallyWiredInboxStoreProbe"] = "InboxStoreConformanceTestKit",
		["FullyWiredLeaderElectionProbe"] = "LeaderElectionConformanceTestKit",
		["PartiallyWiredLeaderElectionProbe"] = "LeaderElectionConformanceTestKit",
		["FullyWiredDeadLetterStoreProbe"] = "DeadLetterStoreConformanceTestKit",
		["PartiallyWiredDeadLetterStoreProbe"] = "DeadLetterStoreConformanceTestKit",
		["FullyWiredDeduplicatorProbe"] = "DeduplicatorConformanceTestKit",
		["PartiallyWiredDeduplicatorProbe"] = "DeduplicatorConformanceTestKit",
		["FullyWiredKeyEscrowServiceProbe"] = "KeyEscrowServiceConformanceTestKit",
		["PartiallyWiredKeyEscrowServiceProbe"] = "KeyEscrowServiceConformanceTestKit",
		["FullyWiredMasterKeyBackupServiceProbe"] = "MasterKeyBackupServiceConformanceTestKit",
		["PartiallyWiredMasterKeyBackupServiceProbe"] = "MasterKeyBackupServiceConformanceTestKit",
		["FullyWiredEncryptionProviderProbe"] = "EncryptionProviderConformanceTestKit",
		["PartiallyWiredEncryptionProviderProbe"] = "EncryptionProviderConformanceTestKit",
		["FullyWiredEncryptionMigrationServiceProbe"] = "EncryptionMigrationServiceConformanceTestKit",
		["PartiallyWiredEncryptionMigrationServiceProbe"] = "EncryptionMigrationServiceConformanceTestKit",
		["FullyWiredLegalHoldStoreProbe"] = "LegalHoldStoreConformanceTestKit",
		["PartiallyWiredLegalHoldStoreProbe"] = "LegalHoldStoreConformanceTestKit",
		["FullyWiredDataInventoryStoreProbe"] = "DataInventoryStoreConformanceTestKit",
		["PartiallyWiredDataInventoryStoreProbe"] = "DataInventoryStoreConformanceTestKit",
		["FullyWiredScheduleStoreProbe"] = "ScheduleStoreConformanceTestKit",
		["PartiallyWiredScheduleStoreProbe"] = "ScheduleStoreConformanceTestKit",
		["FullyWiredStreamingHandlerProbe"] = "StreamingHandlerConformanceTestKit",
		["PartiallyWiredStreamingHandlerProbe"] = "StreamingHandlerConformanceTestKit",
		["NoKitLayerSuite"] = "ConformanceTestKit",
	};

	// Externally attributed tests verify 18 execution arms / 24 renewal arms and reject one missing
	// arm by name; their factories throw if the guard touches a store. Snapshot probes independently
	// count cleanup calls, surface cleanup faults and verify complete/incomplete suite reflection.
	private static readonly IReadOnlyDictionary<(string Path, string Type), string> AdditionalMutationProbeBases =
		new Dictionary<(string Path, string Type), string>
		{
			[(ExecutionProbePath, "Excalibur.Tests.Testing.Conformance::ScheduleExecutionConformanceWiringShould+FullyWiredProbe")] = "ScheduleExecutionStoreConformanceTestKit",
			[(ExecutionProbePath, "Excalibur.Tests.Testing.Conformance::ScheduleExecutionConformanceWiringShould+MissingExecutionArmProbe")] = "ScheduleExecutionStoreConformanceTestKit",
			[(RenewalProbePath, "Excalibur.Tests.Testing.Conformance::ScheduleRenewalConformanceWiringShould+FullyWiredRenewalProbe")] = "ScheduleExecutionLeaseRenewalConformanceTestKit",
			[(RenewalProbePath, "Excalibur.Tests.Testing.Conformance::ScheduleRenewalConformanceWiringShould+MissingRenewalArmProbe")] = "ScheduleExecutionLeaseRenewalConformanceTestKit",
			[(SnapshotProbePath, "Excalibur.Tests.Testing.Conformance::SnapshotKitSuiteIntegrityShould+CountingProbe")] = "SnapshotStoreConformanceTestKit",
			[(SnapshotProbePath, "Excalibur.Tests.Testing.Conformance::SnapshotKitSuiteIntegrityShould+ThrowingCleanupProbe")] = "SnapshotStoreConformanceTestKit",
			[(SnapshotProbePath, "Excalibur.Tests.Testing.Conformance::SnapshotKitSuiteIntegrityShould+FullyWiredProbe")] = "SnapshotStoreConformanceTestKit",
			[(SnapshotProbePath, "Excalibur.Tests.Testing.Conformance::SnapshotKitSuiteIntegrityShould+PartiallyWiredProbe")] = "SnapshotStoreConformanceTestKit",

			// Drives ONE append-position arm against a controlled store to prove that arm can
			// distinguish a truthful endpoint from an interior one. Its two externally attributed
			// facts are a safety/liveness pair: one asserts a truthful endpoint with position gaps
			// is ACCEPTED, the other that an interior position satisfying both allocation
			// inequalities is still REFUSED. It cannot wire every arm because its store implements
			// only enough surface for that one, which is what makes it a probe rather than a suite.
			[(AppendPositionProbePath, "Excalibur.Tests.Testing.Conformance::EventStoreAppendPositionArmShould+Probe")] = "EventStoreConformanceTestKit",

			// CapabilityResolutionShould externally drives these helpers through seven regressions:
			// hidden direct facets and invalid raw results fail; adapters and intentional declines pass.
			// They are mutation instruments and cannot exercise a complete provider contract.
			[(CapabilityProbePath, "Excalibur.Tests.Testing.Conformance::CapabilityResolutionShould+Probe")] = "ConformanceTestKit",
			[(CapabilityProbePath, "Excalibur.Tests.Testing.Conformance::CapabilityResolutionShould+DeadLetterProbe")] = "DeadLetterStoreConformanceTestKit",
		};

	private static bool IsVerifiedMutationProbe(SourceFile file, ClassDeclarationSyntax declaration)
	{
		if (!declaration.Modifiers.Any(token => token.IsKind(SyntaxKind.PrivateKeyword))
			|| !declaration.Modifiers.Any(token => token.IsKind(SyntaxKind.SealedKeyword))
			|| declaration.DescendantNodes().OfType<AttributeSyntax>().Any()
			|| declaration.BaseList?.Types.Count != 1)
		{
			return false;
		}
		var identity = QualifiedTypeOf(declaration);
		var baseName = SimpleNameOf(declaration.BaseList.Types[0].Type);
		return Normalize(file.Path) == CompletionProbePath
			&& identity == "Excalibur.Tests.Testing.Conformance::ScheduleCompletionStrategyConformanceShould+CompletionProbe"
			&& baseName == "ScheduleStoreConformanceTestKit"
			|| Normalize(file.Path) == SuiteWiringProbePath
			&& identity == $"Excalibur.Tests.Testing.Conformance::ConformanceKitSuiteWiringShould+{declaration.Identifier.ValueText}"
			&& SuiteWiringProbeBases.TryGetValue(declaration.Identifier.ValueText, out var expectedBase)
			&& baseName == expectedBase
			|| AdditionalMutationProbeBases.TryGetValue((Normalize(file.Path), identity), out var additionalBase)
			&& baseName == additionalBase;
	}

	/// <summary>Decides whether a class declaration names a conformance kit in its base list.</summary>
	/// <param name="declaration">The class declaration to inspect.</param>
	/// <returns><see langword="true" /> when any base type's simple name ends with the kit suffix.</returns>
	private static bool DerivesAConformanceKit(ClassDeclarationSyntax declaration, IEnumerable<UsingDirectiveSyntax> globalAliases) =>
		declaration.BaseList is not null
		&& declaration.BaseList.Types.Any(baseType => ResolvedBaseName(baseType.Type, declaration, globalAliases)
			.EndsWith(KitSuffix, StringComparison.Ordinal));

	private static string ResolvedBaseName(TypeSyntax type, ClassDeclarationSyntax declaration, IEnumerable<UsingDirectiveSyntax> globalAliases)
	{
		var aliases = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().SelectMany(item => item.Usings)
			.Concat(declaration.SyntaxTree.GetCompilationUnitRoot().Usings).Concat(globalAliases)
			.Where(item => item.Alias is not null).ToList();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		while (type is IdentifierNameSyntax identifier && seen.Add(identifier.Identifier.ValueText))
		{
			var alias = aliases.FirstOrDefault(item => item.Alias!.Name.Identifier.ValueText == identifier.Identifier.ValueText);
			if (alias?.Name is null) { break; }
			type = alias.Name;
		}
		return SimpleNameOf(type);
	}

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
		var unwired = SurveyUnwiredPaths([SuiteWithoutTheGuard]);

		unwired.ShouldBe(["tests/unit/Probe/UnwiredSuite.cs"]);
	}

	[Fact]
	public void Clear_a_kit_deriving_suite_that_wires_the_guard()
	{
		var unwired = SurveyUnwiredPaths([SuiteWithTheGuard]);

		unwired.ShouldBeEmpty("a suite invoking the completeness guard is wired and must not be reported");
	}

	[Fact]
	public void See_a_declaration_whose_base_list_wraps_onto_the_next_line()
	{
		// The failure this arm exists for is not hypothetical: the line-oriented survey that preceded this
		// guard could not see 40 of the repository's 183 kit-deriving declarations, and reported a count.
		var unwired = SurveyUnwiredPaths([SuiteWithAWrappedBaseList]);

		unwired.ShouldBe(["tests/unit/Probe/WrappedBaseListSuite.cs"]);
	}

	[Fact]
	public void Refuse_to_count_a_mention_of_the_guard_as_wiring_it()
	{
		var unwired = SurveyUnwiredPaths([SuiteThatOnlyMentionsTheGuard]);

		unwired.ShouldBe(
			["tests/unit/Probe/MentionOnlySuite.cs"],
			"a comment or string naming the guard is exactly how a suite looks wired while running nothing");
	}

	// ---------- the ratchet, against the real tree ----------
	[Fact]
	public void Reject_new_unwired_debt_in_a_file_with_an_existing_baseline_entry()
	{
		const string path = "tests/unit/Probe/Debt.cs";
		var oldSource = new SourceFile(path, "namespace Probe; class OldDebt : ScheduleStoreConformanceTestKit { }");
		var historical = SurveyDebtIdentities([oldSource]).ToHashSet(StringComparer.Ordinal);
		var withNewDebt = oldSource with { Text = oldSource.Text + " class NewDebt : ScheduleStoreConformanceTestKit { }" };
		UnlistedSuites(SurveyUnwiredSuites([withNewDebt]), historical).Count.ShouldBe(1,
			"the baseline excuses only OldDebt; another type in the same file must be new debt");
	}

	[Fact]
	public void Require_fixed_historical_debt_to_shrink_even_when_new_debt_uses_its_file()
	{
		const string path = "tests/unit/Probe/Debt.cs";
		var oldSource = new SourceFile(path, "namespace Probe; class OldDebt : ScheduleStoreConformanceTestKit { }");
		var historical = SurveyDebtIdentities([oldSource]).ToHashSet(StringComparer.Ordinal);
		var current = new SourceFile(path, """
			namespace Probe;
			class OldDebt : ScheduleStoreConformanceTestKit
			{
				[Fact] public void Guard() => base.ConformanceSuite_ShouldWireEveryArm();
			}
			class NewDebt : ScheduleStoreConformanceTestKit { }
			""");
		var unwired = SurveyUnwiredSuites([current]);
		ObsoleteBaselineEntries(unwired, historical).Count.ShouldBe(1, "OldDebt was fixed and must leave the baseline");
		UnlistedSuites(unwired, historical).Count.ShouldBe(1, "NewDebt cannot inherit OldDebt's exemption");
	}

	[Theory]
	[InlineData("struct")]
	[InlineData("record")]
	[InlineData("record struct")]
	public void Keep_suites_inside_distinct_non_class_containing_types_separate(string kind)
	{
		var source = new SourceFile("tests/unit/Probe/Nested.cs", $$"""
			namespace Probe;
			{{kind}} First
			{
				public class Suite : ScheduleStoreConformanceTestKit
				{
					[Fact] public void Guard() => base.ConformanceSuite_ShouldWireEveryArm();
				}
			}
			{{kind}} Second { public class Suite : ScheduleStoreConformanceTestKit { } }
			""");
		SurveyDebtIdentities([source]).Count.ShouldBe(1,
			"First.Suite cannot supply a guard wrapper for Second.Suite");
	}

	[Theory]
	[InlineData("class")]
	[InlineData("struct")]
	[InlineData("record")]
	[InlineData("record struct")]
	public void Keep_containing_type_arities_and_leaf_arities_in_the_debt_identity(string kind)
	{
		var source = new SourceFile("tests/unit/Probe/Arities.cs", $$"""
			namespace Probe;
			{{kind}} Outer<T> { public class Suite : ScheduleStoreConformanceTestKit { } }
			{{kind}} Outer<T, U> { public class Suite : ScheduleStoreConformanceTestKit { } }
			class Suite : ScheduleStoreConformanceTestKit { }
			class Suite<T> : ScheduleStoreConformanceTestKit { }
			""");
		SurveyDebtIdentities([source]).Distinct(StringComparer.Ordinal).Count().ShouldBe(4);
	}

	[Fact]
	public void Distinguish_namespace_names_from_containing_type_names()
	{
		var nested = new SourceFile("tests/unit/Probe/Nested.cs",
			"namespace Probe { class Outer { public class Suite : ScheduleStoreConformanceTestKit { } } }");
		var namespaced = new SourceFile("tests/unit/Probe/Namespaced.cs",
			"namespace Probe.Outer { class Suite : ScheduleStoreConformanceTestKit { [Fact] public void Guard() => base.ConformanceSuite_ShouldWireEveryArm(); } }");
		SurveyDebtIdentities([nested, namespaced]).Count.ShouldBe(1);
	}

	[Fact]
	public void Preserve_distinct_owning_projects_in_the_debt_identity()
	{
		var first = new SourceFile("tests/unit/First/Suite.cs", "namespace Probe; class Suite : ScheduleStoreConformanceTestKit { }");
		var second = first with { Path = "tests/unit/Second/Suite.cs" };
		SurveyDebtIdentities([first, second]).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
	}

	[Theory]
	[InlineData("class")]
	[InlineData("struct")]
	[InlineData("record")]
	[InlineData("record struct")]
	public void Merge_partials_of_the_same_nested_generic_type_in_the_same_project(string kind)
	{
		var definition = new SourceFile("tests/unit/Probe/Definition.cs",
			$"namespace Probe; partial {kind} Outer<T> {{ public partial class Suite<U> : ScheduleStoreConformanceTestKit {{ }} }}");
		var wrapper = new SourceFile("tests/unit/Probe/Wrapper.cs",
			$"namespace Probe; partial {kind} Outer<T> {{ public partial class Suite<U> {{ [Fact] public void Guard() => base.ConformanceSuite_ShouldWireEveryArm(); }} }}");
		SurveyDebtIdentities([definition, wrapper]).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("struct")]
	[InlineData("record")]
	[InlineData("record struct")]
	public void Refuse_a_verified_probe_name_relocated_under_another_containing_type(string kind)
	{
		var source = CompletionProbeSource(CompletionProbePath, "");
		source = source with { Text = source.Text.Replace("public class ScheduleCompletionStrategyConformanceShould",
			$"public {kind} Additional {{ public class ScheduleCompletionStrategyConformanceShould", StringComparison.Ordinal) + " }" };
		SurveyDebtIdentities([source]).Count.ShouldBe(1);
	}

	[Fact]
	public void Preserve_the_full_identity_and_diagnostic_path_in_each_survey_result()
	{
		var source = new SourceFile("tests/unit/Probe/Identity.cs",
			"namespace Probe; record struct Outer<T> { public class Suite<U, V> : ScheduleStoreConformanceTestKit { } }");
		var debt = SurveyUnwiredSuites([source]).ShouldHaveSingleItem();
		debt.Identity.ShouldBe("tests/unit/Probe|Probe::Outer`1+Suite`2");
		debt.SourcePaths.ShouldBe([source.Path]);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Keep_file_local_suite_or_containing_type_identities_separate_across_files(bool fileLocalOuter)
	{
		var wiredDeclaration = "class Suite : ScheduleStoreConformanceTestKit { [Fact] public void Guard() => base.ConformanceSuite_ShouldWireEveryArm(); }";
		var unwiredDeclaration = "class Suite : ScheduleStoreConformanceTestKit { }";
		var wired = new SourceFile("tests/unit/Probe/First.cs", "namespace Probe; "
			+ (fileLocalOuter ? $"file class Outer {{ {wiredDeclaration} }}" : "file " + wiredDeclaration));
		var unwired = new SourceFile("tests/unit/Probe/Second.cs", "namespace Probe; "
			+ (fileLocalOuter ? $"file class Outer {{ {unwiredDeclaration} }}" : "file " + unwiredDeclaration));
		SurveyDebtIdentities([wired, unwired]).Count.ShouldBe(1,
			"a file-local type in First.cs cannot lend its wrapper to a distinct type in Second.cs");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Merge_partial_declarations_of_a_file_local_type_within_the_same_file(bool fileLocalOuter)
	{
		const string definition = "partial class Suite : ScheduleStoreConformanceTestKit { }";
		const string wrapper = "partial class Suite { [Fact] public void Guard() => base.ConformanceSuite_ShouldWireEveryArm(); }";
		var source = new SourceFile("tests/unit/Probe/Partials.cs", "namespace Probe; "
			+ (fileLocalOuter ? $"file partial class Outer {{ {definition} }} file partial class Outer {{ {wrapper} }}"
				: $"file {definition} file {wrapper}"));
		SurveyDebtIdentities([source]).ShouldBeEmpty();
	}

	// Kept separate from path-focused invocation controls: these assertions exercise the identities
	// consumed by admission and shrink, rather than merely the diagnostic locations.
	private static IReadOnlyList<string> SurveyDebtIdentities(IEnumerable<SourceFile> sources) =>
		SurveyUnwiredSuites(sources).Select(suite => suite.Identity).ToList();

	// Invocation controls assert diagnostic locations; the ratchet and identity controls above
	// always consume SuiteDebt.Identity, never these projected paths.
	private static IReadOnlyList<string> SurveyUnwiredPaths(
		IEnumerable<SourceFile> sources, IReadOnlyDictionary<string, string>? owningProjects = null) =>
		SurveyUnwiredSuites(sources, owningProjects).SelectMany(suite => suite.SourcePaths)
			.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList();
	[Theory]
	[InlineData("[Fact] public void Guard() => other.ConformanceSuite_ShouldWireEveryArm();")]
	[InlineData("private void Guard() => ConformanceSuite_ShouldWireEveryArm();")]
	[InlineData("private sealed class Helper { [Fact] public void Guard() => ConformanceSuite_ShouldWireEveryArm(); }")]
	[InlineData("[Fact] public void Guard() { void Uncalled() => ConformanceSuite_ShouldWireEveryArm(); }")]
	[InlineData("[Fact] public void Guard() { Action uncalled = () => ConformanceSuite_ShouldWireEveryArm(); }")]
	public void Reject_guard_calls_outside_an_attributed_wrapper_on_the_suite(string member)
	{
		var source = new SourceFile("tests/unit/Probe/Suite.cs",
			$"namespace Probe; class Suite : ScheduleStoreConformanceTestKit {{ {member} }}");
		SurveyUnwiredPaths([source]).ShouldBe([source.Path]);
	}

	[Fact]
	public void Report_an_unwired_suite_even_when_another_suite_in_its_file_is_wired()
	{
		var source = new SourceFile("tests/unit/Probe/TwoSuites.cs", """
			namespace Probe;
			class Unwired : ScheduleStoreConformanceTestKit { }
			class Wired : ScheduleStoreConformanceTestKit
			{
				[Fact] public void Guard() => base.ConformanceSuite_ShouldWireEveryArm();
			}
			""");
		SurveyUnwiredPaths([source]).ShouldBe([source.Path]);
	}

	[Theory]
	[InlineData("ConformanceSuite_ShouldWireEveryArm()")]
	[InlineData("this.ConformanceSuite_ShouldWireEveryArm()")]
	[InlineData("base.ConformanceSuite_ShouldWireEveryArm()")]
	public void Combine_partial_declarations_in_the_same_project(string invocation)
	{
		var definition = new SourceFile("tests/unit/Probe/Definition.cs",
			"namespace Probe; partial class Suite : ScheduleStoreConformanceTestKit { }");
		var wrapper = new SourceFile("tests/unit/Probe/Wrapper.cs",
			$"namespace Probe; partial class Suite {{ [Fact] public void Guard() => {invocation}; }}");
		SurveyUnwiredPaths([definition, wrapper]).ShouldBeEmpty();
	}

	[Fact]
	public void Keep_identically_named_classes_in_different_projects_separate()
	{
		var definition = new SourceFile("tests/unit/FirstProject/Definition.cs",
			"namespace Probe; partial class Suite : ScheduleStoreConformanceTestKit { }");
		var wrapper = new SourceFile("tests/unit/SecondProject/Wrapper.cs",
			"namespace Probe; partial class Suite { [Fact] public void Guard() => ConformanceSuite_ShouldWireEveryArm(); }");
		SurveyUnwiredPaths([definition, wrapper]).ShouldBe([definition.Path]);
	}

	[Fact]
	public void Combine_partial_declarations_across_directories_owned_by_one_project()
	{
		var definition = new SourceFile("tests/unit/Probe/Definition.cs",
			"namespace Probe; partial class Suite : ScheduleStoreConformanceTestKit { }");
		var wrapper = new SourceFile("tests/shared/Wrappers/Guard.cs",
			"namespace Probe; partial class Suite { [Fact] public void Guard() => ConformanceSuite_ShouldWireEveryArm(); }");
		var projects = new Dictionary<string, string> { [definition.Path] = "Probe.csproj", [wrapper.Path] = "Probe.csproj" };
		SurveyUnwiredPaths([definition, wrapper], projects).ShouldBeEmpty();
	}

	private const string CompletionProbePath =
		"tests/unit/Excalibur.Testing.Conformance.Tests/Testing/Conformance/ScheduleCompletionStrategyConformanceShould.cs";

	[Theory]
	[InlineData("ConformanceSuite_ShouldWireEveryArm()")]
	[InlineData("this.ConformanceSuite_ShouldWireEveryArm()")]
	public void Reject_a_wrapper_that_calls_a_shadowed_guard_in_another_partial(string invocation)
	{
		var definition = new SourceFile("tests/unit/Probe/Shadow.cs", """
			namespace Probe;
			partial class Suite : ScheduleStoreConformanceTestKit
			{
				public new void ConformanceSuite_ShouldWireEveryArm() { }
			}
			""");
		var wrapper = new SourceFile("tests/unit/Probe/Wrapper.cs",
			$"namespace Probe; partial class Suite {{ [Fact] public void Guard() => {invocation}; }}");
		SurveyUnwiredPaths([definition, wrapper]).ShouldBe([definition.Path]);
	}

	[Fact]
	public void Permit_an_explicit_base_guard_call_despite_a_same_class_shadow()
	{
		var source = new SourceFile("tests/unit/Probe/Shadow.cs", """
			namespace Probe;
			class Suite : ScheduleStoreConformanceTestKit
			{
				public new void ConformanceSuite_ShouldWireEveryArm() { }
				[Fact] public void Guard() => base.ConformanceSuite_ShouldWireEveryArm();
			}
			""");
		SurveyUnwiredPaths([source]).ShouldBeEmpty();
	}

	[Fact]
	public void Permit_an_attributed_override_that_explicitly_calls_the_base_guard()
	{
		var source = new SourceFile("tests/unit/Probe/Override.cs", """
			namespace Probe;
			class Suite : ScheduleStoreConformanceTestKit
			{
				[Fact] public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();
			}
			""");
		SurveyUnwiredPaths([source]).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("[Fact] public void Guard() { void ConformanceSuite_ShouldWireEveryArm() { } ConformanceSuite_ShouldWireEveryArm(); }")]
	[InlineData("[Fact] public void Guard() { Action ConformanceSuite_ShouldWireEveryArm = () => { }; ConformanceSuite_ShouldWireEveryArm(); }")]
	[InlineData("[Theory] public void Guard(Action ConformanceSuite_ShouldWireEveryArm) { ConformanceSuite_ShouldWireEveryArm(); }")]
	[InlineData("[Fact] public void Guard() { foreach (var ConformanceSuite_ShouldWireEveryArm in callbacks) { ConformanceSuite_ShouldWireEveryArm(); } }")]
	[InlineData("[Fact] public void Guard() { if (callback is Action ConformanceSuite_ShouldWireEveryArm) { ConformanceSuite_ShouldWireEveryArm(); } }")]
	public void Reject_a_bare_guard_call_shadowed_by_a_wrapper_local_identifier(string member)
	{
		var source = new SourceFile("tests/unit/Probe/LocalShadow.cs",
			$"namespace Probe; class Suite : ScheduleStoreConformanceTestKit {{ {member} }}");
		SurveyUnwiredPaths([source]).ShouldBe([source.Path]);
	}

	[Theory]
	[InlineData("this")]
	[InlineData("base")]
	public void Permit_an_explicit_guard_receiver_despite_a_wrapper_local_shadow(string receiver)
	{
		var source = new SourceFile("tests/unit/Probe/LocalShadow.cs",
			$"namespace Probe; class Suite : ScheduleStoreConformanceTestKit {{ [Fact] public void Guard() {{ Action ConformanceSuite_ShouldWireEveryArm = () => {{ }}; {receiver}.ConformanceSuite_ShouldWireEveryArm(); }} }}");
		SurveyUnwiredPaths([source]).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("Skip = \"disabled\"")]
	[InlineData("Skip = \"\"")]
	[InlineData("Explicit = true")]
	[InlineData("SkipUnless = \"Enabled\"")]
	[InlineData("SkipWhen = \"Disabled\"")]
	[InlineData("SkipType = typeof(Options)")]
	public void Reject_a_guard_wrapper_with_skip_or_explicit_discovery_options(string option)
	{
		var source = new SourceFile("tests/unit/Probe/Skipped.cs",
			$"namespace Probe; class Suite : ScheduleStoreConformanceTestKit {{ [Fact({option})] public void Guard() => ConformanceSuite_ShouldWireEveryArm(); }}");
		SurveyUnwiredPaths([source]).ShouldBe([source.Path]);
	}

	[Fact]
	public void Permit_a_guard_wrapper_with_an_explicitly_null_skip_reason()
	{
		var source = new SourceFile("tests/unit/Probe/Active.cs",
			"namespace Probe; class Suite : ScheduleStoreConformanceTestKit { [Fact(Skip = null)] public void Guard() => ConformanceSuite_ShouldWireEveryArm(); }");
		SurveyUnwiredPaths([source]).ShouldBeEmpty();
	}

	[Fact]
	public void Find_a_conformance_kit_named_through_a_using_alias()
	{
		var source = new SourceFile("tests/unit/Probe/Alias.cs", """
			using Kit = Excalibur.Testing.Conformance.ScheduleStoreConformanceTestKit;
			namespace Probe;
			class Provider : Kit { }
			""");
		SurveyUnwiredPaths([source]).ShouldBe([source.Path]);
	}

	[Fact]
	public void Find_a_conformance_kit_named_through_a_global_using_alias()
	{
		var alias = new SourceFile("tests/unit/Probe/Aliases.cs",
			"global using Kit = Excalibur.Testing.Conformance.ScheduleStoreConformanceTestKit;");
		var provider = new SourceFile("tests/unit/Probe/Provider.cs", "namespace Probe; class Provider : Kit { }");
		SurveyUnwiredPaths([alias, provider]).ShouldBe([provider.Path]);
	}

	[Fact]
	public void Exempt_the_specific_unattributed_completion_mutation_probe()
	{
		SurveyUnwiredPaths([CompletionProbeSource(CompletionProbePath, "")]).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("[Fact] public void ProviderArm() { }")]
	[InlineData("[Trait(\"Category\", \"Unit\")] public void ProviderArm() { }")]
	public void Refuse_the_completion_probe_exception_when_a_member_is_attributed(string member)
	{
		SurveyUnwiredPaths([CompletionProbeSource(CompletionProbePath, member)]).ShouldBe([CompletionProbePath]);
	}

	[Fact]
	public void Refuse_a_same_shaped_probe_in_another_file()
	{
		const string path = "tests/unit/Probe/RealProvider.cs";
		SurveyUnwiredPaths([CompletionProbeSource(path, "")]).ShouldBe([path]);
	}

	[Fact]
	public void Refuse_an_attributed_completion_probe_class()
	{
		var source = CompletionProbeSource(CompletionProbePath, "");
		SurveyUnwiredPaths([source with { Text = source.Text.Replace("private sealed", "[Trait(\"Category\", \"Unit\")] private sealed", StringComparison.Ordinal) }])
			.ShouldBe([CompletionProbePath]);
	}

	private static SourceFile CompletionProbeSource(string path, string member) => new(path,
		$$"""
		namespace Excalibur.Tests.Testing.Conformance;
		public class ScheduleCompletionStrategyConformanceShould
		{
			private sealed class CompletionProbe : ScheduleStoreConformanceTestKit { {{member}} }
		}
		""");

	private const string SuiteWiringProbePath =
		"tests/unit/Excalibur.Testing.Conformance.Tests/Testing/Conformance/ConformanceKitSuiteWiringShould.cs";
	private const string ExecutionProbePath =
		"tests/unit/Excalibur.Testing.Conformance.Tests/Testing/Conformance/ScheduleExecutionConformanceWiringShould.cs";
	private const string RenewalProbePath =
		"tests/unit/Excalibur.Testing.Conformance.Tests/Testing/Conformance/ScheduleRenewalConformanceWiringShould.cs";
	private const string SnapshotProbePath =
		"tests/unit/Excalibur.Testing.Conformance.Tests/Testing/Conformance/SnapshotKitSuiteIntegrityShould.cs";
	private const string AppendPositionProbePath =
		"tests/unit/Excalibur.Testing.Conformance.Tests/Testing/Conformance/EventStoreAppendPositionArmShould.cs";
	private const string CapabilityProbePath =
		"tests/unit/Excalibur.Testing.Conformance.Tests/Testing/Conformance/CapabilityResolutionShould.cs";

	[Theory]
	[InlineData(ExecutionProbePath, "ScheduleExecutionConformanceWiringShould", "FullyWiredProbe", "ScheduleExecutionStoreConformanceTestKit")]
	[InlineData(RenewalProbePath, "ScheduleRenewalConformanceWiringShould", "MissingRenewalArmProbe", "ScheduleExecutionLeaseRenewalConformanceTestKit")]
	[InlineData(SnapshotProbePath, "SnapshotKitSuiteIntegrityShould", "CountingProbe", "SnapshotStoreConformanceTestKit")]
	[InlineData(AppendPositionProbePath, "EventStoreAppendPositionArmShould", "Probe", "EventStoreConformanceTestKit")]
	[InlineData(CapabilityProbePath, "CapabilityResolutionShould", "Probe", "ConformanceTestKit")]
	[InlineData(CapabilityProbePath, "CapabilityResolutionShould", "DeadLetterProbe", "DeadLetterStoreConformanceTestKit")]
	public void Exempt_exact_verified_execution_renewal_and_snapshot_mutation_probes(string path, string outer, string name, string kit)
	{
		SurveyUnwiredPaths([AdditionalProbeSource(path, outer, name, kit, "")]).ShouldBeEmpty();
	}

	[Theory]
	[InlineData(ExecutionProbePath, "ScheduleExecutionConformanceWiringShould", "NewProviderProbe", "ScheduleExecutionStoreConformanceTestKit", "")]
	[InlineData(RenewalProbePath, "ScheduleRenewalConformanceWiringShould", "MissingRenewalArmProbe", "ScheduleStoreConformanceTestKit", "")]
	[InlineData(SnapshotProbePath, "SnapshotKitSuiteIntegrityShould", "CountingProbe", "SnapshotStoreConformanceTestKit", "[Fact] public void Arm() { }")]
	[InlineData("tests/unit/Probe/Provider.cs", "SnapshotKitSuiteIntegrityShould", "CountingProbe", "SnapshotStoreConformanceTestKit", "")]
	[InlineData(CapabilityProbePath, "CapabilityResolutionShould", "DeadLetterProbe", "DeadLetterStoreConformanceTestKit", "[Fact] public void Arm() { }")]
	[InlineData(CapabilityProbePath, "AnotherContainer", "DeadLetterProbe", "DeadLetterStoreConformanceTestKit", "")]
	[InlineData(CapabilityProbePath, "CapabilityResolutionShould", "DeadLetterProbe", "OutboxStoreConformanceTestKit", "")]
	[InlineData("tests/unit/Probe/Provider.cs", "CapabilityResolutionShould", "DeadLetterProbe", "DeadLetterStoreConformanceTestKit", "")]
	public void Refuse_an_unverified_execution_renewal_or_snapshot_probe(string path, string outer, string name, string kit, string member)
	{
		SurveyUnwiredPaths([AdditionalProbeSource(path, outer, name, kit, member)]).ShouldBe([path]);
	}

	private static SourceFile AdditionalProbeSource(string path, string outer, string name, string kit, string member) => new(path,
		$$"""
		namespace Excalibur.Tests.Testing.Conformance;
		public class {{outer}} { private sealed class {{name}} : {{kit}} { {{member}} } }
		""");

	[Theory]
	[InlineData("public sealed")]
	[InlineData("private")]
	public void Refuse_capability_probe_when_its_verified_visibility_changes(string modifiers)
	{
		var source = AdditionalProbeSource(CapabilityProbePath, "CapabilityResolutionShould", "DeadLetterProbe", "DeadLetterStoreConformanceTestKit", "");
		SurveyUnwiredPaths([source with { Text = source.Text.Replace("private sealed", modifiers, StringComparison.Ordinal) }]).ShouldBe([CapabilityProbePath]);
	}

	[Fact]
	public void Exempt_an_exact_existing_externally_exercised_wiring_probe()
	{
		SurveyUnwiredPaths([WiringProbeSource(SuiteWiringProbePath, "PartiallyWiredScheduleStoreProbe", "")]).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("tests/unit/Probe/Provider.cs", "PartiallyWiredScheduleStoreProbe", "")]
	[InlineData(SuiteWiringProbePath, "UnexpectedProviderProbe", "")]
	[InlineData(SuiteWiringProbePath, "PartiallyWiredScheduleStoreProbe", "[Fact] public void Arm() { }")]
	public void Refuse_an_unverified_or_attributed_wiring_probe(string path, string name, string member)
	{
		SurveyUnwiredPaths([WiringProbeSource(path, name, member)]).ShouldBe([path]);
	}

	[Fact]
	public void Exclude_abstract_kit_definitions_from_runnable_suite_obligations()
	{
		SurveyUnwiredPaths([new SourceFile("tests/unit/Probe/Kit.cs",
			"namespace Probe; abstract class Kit : ConformanceTestKit { }")]).ShouldBeEmpty();
	}

	private static SourceFile WiringProbeSource(string path, string name, string member) => new(path,
		$$"""
		namespace Excalibur.Tests.Testing.Conformance;
		public class ConformanceKitSuiteWiringShould
		{
			private sealed class {{name}} : ScheduleStoreConformanceTestKit { {{member}} }
		}
		""");

	[Fact]
	public void Admit_no_kit_deriving_suite_outside_the_baseline()
	{
		var sources = ReadTestSources(out var population, out var owningProjects);
		var unwired = SurveyUnwiredSuites(sources, owningProjects);

		population.ShouldNotBeEmpty("the source population must be non-empty or every verdict here is vacuous");

		var baseline = ReadBaseline();

		var unlisted = UnlistedSuites(unwired, baseline);

		unlisted.ShouldBeEmpty(
			$"these suites derive a conformance kit and never invoke {CompletenessGuard}, so any arm nobody "
			+ "wrapped is silently not run. Wire an attributed wrapper on the suite, or verify and document "
			+ $"an exact mutation-probe exception. {BaselineRelativePath} must never grow:{Environment.NewLine}"
			+ string.Join(Environment.NewLine, unlisted.Select(suite => $"{suite.Identity} ({string.Join(", ", suite.SourcePaths)})")));
	}

	[Fact]
	public void Require_a_baselined_suite_that_is_now_wired_to_be_removed_from_the_baseline()
	{
		var sources = ReadTestSources(out _, out var owningProjects);
		var unwired = SurveyUnwiredSuites(sources, owningProjects);

		var baseline = ReadBaseline();

		var nowWired = ObsoleteBaselineEntries(unwired, baseline);

		nowWired.ShouldBeEmpty(
			$"these are listed in {BaselineRelativePath} but now either wire the guard or no longer derive a "
			+ $"kit. Remove them: the baseline shrinks and never grows.{Environment.NewLine}"
			+ string.Join(Environment.NewLine, nowWired));
	}

	/// <summary>Reads every non-generated C# source under <c>tests/</c>.</summary>
	/// <param name="population">Receives the files read, for the non-vacuity assertion.</param>
	/// <returns>The sources, repository-relative.</returns>
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
		// Shared source files outside a project directory get a distinct identity: they cannot lend
		// wrappers to another assembly merely because their namespaces and type names happen to match.
		owningProjects = sources.ToDictionary(file => Normalize(file.Path), file => projects
			.FirstOrDefault(project => Normalize(file.Path).StartsWith(project[..(project.LastIndexOf('/') + 1)], StringComparison.Ordinal))
			?? $"unowned:{Normalize(file.Path)}", StringComparer.Ordinal);

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
