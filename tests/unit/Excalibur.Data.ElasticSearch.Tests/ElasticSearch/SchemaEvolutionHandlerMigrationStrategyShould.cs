// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Regression coverage for the <see cref="SchemaEvolutionHandler"/> migration
/// strategy dispatch — specifically the <see cref="MigrationStrategy.UpdateInPlace"/>
/// path that SENTINEL flagged (sprint-802 msg 1952) as a silent no-op after the
/// S802-A1 γ-seam consolidation at <c>b965e6eb1</c>.
/// </summary>
/// <remarks>
/// <para>
/// Bug path (current main): <see cref="SchemaEvolutionHandler.PlanMigrationAsync"/>
/// branches on <c>Reindex / AliasSwitch / DualWrite</c>; the <c>else</c> branch
/// (i.e. <see cref="MigrationStrategy.UpdateInPlace"/>) emits a single
/// <see cref="StepOperationType.UpdateMapping"/> step targeting
/// <c>request.SourceIndex</c>. Post-A1 consolidation, <c>ExecuteStepAsync</c>'s
/// <c>UpdateMapping</c> case is a bare <c>return true</c> — no seam invocation —
/// producing <c>Success = true</c> with zero ES state change.
/// </para>
/// <para>
/// Fix (Option B, per FORGE msg 1954 + SENTINEL msg 1956 acceptance criteria):
/// re-shape <c>MigrateAsync</c> to accept <c>sourceIndex == targetIndex</c> as a
/// pure mapping-put + route <c>UpdateInPlace</c> planning through a Reindex step
/// with equal source/target. The assertion below pins that routing shape — the
/// test fails on current main and turns green once the fix lands.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class SchemaEvolutionHandlerMigrationStrategyShould
{
	private const string SourceIndex = "orders-v1";
	private const string TargetIndex = "orders-v2";
	private const string ProjectionType = "OrderProjection";

	[Fact]
	public async Task UpdateInPlace_AppliesMappingToSourceIndex()
	{
		// Arrange — internal test-seam ctor with all 4 γ seams faked.
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();

		// PlanMigrationAsync needs a document-count to compute estimated duration.
		A.CallTo(() => inspection.CountDocumentsAsync(SourceIndex, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(100));

		// EnsureIndicesAsync fires before StoreMigrationResultAsync — satisfy both history seams.
		A.CallTo(() => history.EnsureHistoryIndexAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => migrationHistory.EnsureHistoryIndexAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => migrationHistory.WriteMigrationResultAsync(
				A<string>._, A<string>._, A<MigrationHistoryRecord>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Option (B): the fix routes UpdateInPlace through MigrateAsync with sourceIndex == targetIndex.
		A.CallTo(() => ops.MigrateAsync(
				A<string>._, A<string>._, A<object?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new MigrationStepOutcome(Success: true, ErrorDetails: null)));

		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);

		var request = new SchemaMigrationRequest
		{
			ProjectionType = ProjectionType,
			SourceIndex = SourceIndex,
			TargetIndex = TargetIndex,  // Note: plan's else-branch uses SourceIndex regardless of TargetIndex.
			Strategy = MigrationStrategy.UpdateInPlace,
			NewSchema = new { mapping = "placeholder" },
		};

		// Act
		var plan = await handler.PlanMigrationAsync(request, CancellationToken.None);
		var result = await handler.ExecuteMigrationAsync(plan, CancellationToken.None);

		// Assert — SENTINEL msg 1956 pin: the handler MUST invoke MigrateAsync with
		// sourceIndex == sourceIndex (in-place shape). Merely asserting "some seam
		// called" would pass for option (A)/(C); this pins Option (B)'s routing.
		A.CallTo(() => ops.MigrateAsync(
				SourceIndex,
				SourceIndex,
				A<object?>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Correctness contract: the plan must report Success with the seam actually invoked.
		result.Success.ShouldBeTrue();
		result.PlanId.ShouldBe(plan.PlanId);
	}

	private static (
		ISchemaEvolutionOperations ops,
		ISchemaHistoryStore history,
		IMigrationHistoryStore migrationHistory,
		IIndexInspection inspection,
		IIndexAliasManager aliasManager,
		IOptions<ProjectionOptions> options,
		ILogger<SchemaEvolutionHandler> logger) CreateSeamFakes()
	{
		var ops = A.Fake<ISchemaEvolutionOperations>();
		var history = A.Fake<ISchemaHistoryStore>();
		var migrationHistory = A.Fake<IMigrationHistoryStore>();
		var inspection = A.Fake<IIndexInspection>();
		var aliasManager = A.Fake<IIndexAliasManager>();
		var options = Options.Create(new ProjectionOptions
		{
			IndexPrefix = "test",
			SchemaEvolution = new SchemaEvolutionOptions { AllowBreakingChanges = false },
		});
		var logger = A.Fake<ILogger<SchemaEvolutionHandler>>();
		return (ops, history, migrationHistory, inspection, aliasManager, options, logger);
	}
}
