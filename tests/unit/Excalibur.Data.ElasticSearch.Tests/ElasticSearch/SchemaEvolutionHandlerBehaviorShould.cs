// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Behavioral routing coverage for <see cref="SchemaEvolutionHandler"/> via
/// the four S802 <c>bd-itj7qt</c> Path-4 γ seams
/// (<see cref="ISchemaEvolutionOperations"/>, <see cref="ISchemaHistoryStore"/>,
/// <see cref="IMigrationHistoryStore"/>, <see cref="IIndexInspection"/>).
/// Closes the S802 NB-1 coverage gap flagged by SENTINEL msg 1952 (bd-hc915h)
/// + CRUCIBLE msg 1950 Phase 3 audit. Exercises the handler through the
/// internal test-seam ctor so no real SDK is in play.
/// </summary>
/// <remarks>
/// ADR-142 §D7 seam-passthrough discipline: assertions target seam-method
/// invocations, not Elastic cluster behavior. A real-SDK smoke per adapter
/// lives under the integration shard
/// (<c>tests/integration/Excalibur.Integration.Tests/DataElasticSearch/Conformance/</c>).
/// Ctor null-guards already covered by <see cref="SchemaEvolutionHandlerDisposeShould"/>
/// — not duplicated here.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class SchemaEvolutionHandlerBehaviorShould
{
	private const string SourceIndex = "orders-v1";
	private const string TargetIndex = "orders-v2";
	private const string ProjectionType = "OrderProjection";
	private const string IndexPrefix = "test";

	[Fact]
	public async Task CompareSchemaAsync_ReadsVersionFromBothIndicesViaOperationsSeam()
	{
		// Arrange
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();
		A.CallTo(() => ops.GetSchemaVersionAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new SchemaVersion(Version: "1.0.0", MappingJson: null)));
		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);

		// Act
		_ = await handler.CompareSchemaAsync(SourceIndex, TargetIndex, CancellationToken.None);

		// Assert — version lookup routes through ISchemaEvolutionOperations.
		A.CallTo(() => ops.GetSchemaVersionAsync(SourceIndex, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => ops.GetSchemaVersionAsync(TargetIndex, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PlanMigrationAsync_ConsultsInspectionSeamForDocumentCount()
	{
		// Arrange
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();
		A.CallTo(() => inspection.CountDocumentsAsync(SourceIndex, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(5000));
		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);
		var request = new SchemaMigrationRequest
		{
			ProjectionType = ProjectionType,
			SourceIndex = SourceIndex,
			TargetIndex = TargetIndex,
			Strategy = MigrationStrategy.Reindex,
			NewSchema = new { mapping = "placeholder" },
		};

		// Act
		var plan = await handler.PlanMigrationAsync(request, CancellationToken.None);

		// Assert — Plan counts documents through IIndexInspection (not raw SDK).
		A.CallTo(() => inspection.CountDocumentsAsync(SourceIndex, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		plan.EstimatedDocuments.ShouldBe(5000);
		plan.Strategy.ShouldBe(MigrationStrategy.Reindex);
	}

	[Fact]
	public async Task ExecuteMigrationAsync_RoutesReindexStepThroughOperationsSeam()
	{
		// Arrange
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();
		A.CallTo(() => ops.EnsureMigrationIndexAsync(A<string>._, A<object?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new MigrationStepOutcome(Success: true, ErrorDetails: null)));
		A.CallTo(() => ops.MigrateAsync(A<string>._, A<string>._, A<object?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new MigrationStepOutcome(Success: true, ErrorDetails: null)));
		A.CallTo(() => history.EnsureHistoryIndexAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => migrationHistory.EnsureHistoryIndexAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => migrationHistory.WriteMigrationResultAsync(
				A<string>._, A<string>._, A<MigrationHistoryRecord>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => inspection.CountDocumentsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(0));

		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);
		var request = new SchemaMigrationRequest
		{
			ProjectionType = ProjectionType,
			SourceIndex = SourceIndex,
			TargetIndex = TargetIndex,
			Strategy = MigrationStrategy.Reindex,
			NewSchema = new { mapping = "placeholder" },
		};
		var plan = await handler.PlanMigrationAsync(request, CancellationToken.None);

		// Act
		var result = await handler.ExecuteMigrationAsync(plan, CancellationToken.None);

		// Assert — CreateIndex step → ops.EnsureMigrationIndexAsync; Reindex step → ops.MigrateAsync.
		A.CallTo(() => ops.EnsureMigrationIndexAsync(TargetIndex, A<object?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => ops.MigrateAsync(SourceIndex, TargetIndex, A<object?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		// Migration result is persisted through IMigrationHistoryStore.
		A.CallTo(() => migrationHistory.WriteMigrationResultAsync(
				A<string>._, A<string>._, A<MigrationHistoryRecord>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task RegisterSchemaVersionAsync_EnsuresIndicesAndWritesViaHistorySeam()
	{
		// Arrange
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();
		A.CallTo(() => history.EnsureHistoryIndexAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => migrationHistory.EnsureHistoryIndexAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => history.WriteSchemaVersionAsync(
				A<string>._, A<string>._, A<SchemaHistoryRecord>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);
		var registration = new SchemaVersionRegistration
		{
			ProjectionType = ProjectionType,
			Version = "2.0.0",
			Schema = new { mapping = "placeholder" },
			RegisteredAt = DateTimeOffset.UtcNow,
			Description = "Add field X",
		};

		// Act
		await handler.RegisterSchemaVersionAsync(registration, CancellationToken.None);

		// Assert — EnsureIndicesAsync primes both history seams; then ISchemaHistoryStore writes.
		A.CallTo(() => history.EnsureHistoryIndexAsync($"{IndexPrefix}-schema-history", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => migrationHistory.EnsureHistoryIndexAsync($"{IndexPrefix}-schema-migrations", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => history.WriteSchemaVersionAsync(
				$"{IndexPrefix}-schema-history",
				$"{ProjectionType}:2.0.0",
				A<SchemaHistoryRecord>.That.Matches(r =>
					r.ProjectionType == ProjectionType && r.Version == "2.0.0"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetSchemaHistoryAsync_QueriesBothHistorySeams()
	{
		// Arrange
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();
		A.CallTo(() => history.EnsureHistoryIndexAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => migrationHistory.EnsureHistoryIndexAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => history.QueryHistoryAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<SchemaHistoryRecord>>([
				new SchemaHistoryRecord
				{
					ProjectionType = ProjectionType,
					Version = "1.0.0",
					SchemaJson = "{}",
					RegisteredAt = DateTimeOffset.UtcNow,
				},
			]));
		A.CallTo(() => migrationHistory.QueryHistoryAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<MigrationHistoryRecord>>([]));
		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);

		// Act
		var schemaHistory = await handler.GetSchemaHistoryAsync(ProjectionType, CancellationToken.None);

		// Assert — both stores queried with the correct index + projection type.
		A.CallTo(() => history.QueryHistoryAsync(
				$"{IndexPrefix}-schema-history", ProjectionType, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => migrationHistory.QueryHistoryAsync(
				$"{IndexPrefix}-schema-migrations", ProjectionType, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		schemaHistory.CurrentVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public async Task DryRunMigrationAsync_SamplesDocumentIdsViaInspectionSeam()
	{
		// Arrange
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();
		A.CallTo(() => inspection.SampleDocumentIdsAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<string>>(["id1", "id2", "id3"]));
		A.CallTo(() => inspection.CountDocumentsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(42));

		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);
		var request = new SchemaMigrationRequest
		{
			ProjectionType = ProjectionType,
			SourceIndex = SourceIndex,
			TargetIndex = TargetIndex,
			Strategy = MigrationStrategy.Reindex,
			NewSchema = new { mapping = "placeholder" },
		};
		var plan = await handler.PlanMigrationAsync(request, CancellationToken.None);

		// Act
		var dryRun = await handler.DryRunMigrationAsync(plan, sampleSize: 10, CancellationToken.None);

		// Assert — sample routed through IIndexInspection, not SDK.
		A.CallTo(() => inspection.SampleDocumentIdsAsync(SourceIndex, 10, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		dryRun.Success.ShouldBeTrue();
		dryRun.DocumentsTested.ShouldBe(3);
	}

	[Fact]
	public async Task ValidateBackwardsCompatibilityAsync_IsPureLogic_DoesNotTouchAnySeam()
	{
		// Arrange
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();
		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);

		// Act
		var result = await handler.ValidateBackwardsCompatibilityAsync(
			currentSchema: new { mapping = "a" },
			newSchema: new { mapping = "a" },
			CancellationToken.None);

		// Assert — no seam invocations; pure schema-comparison logic.
		A.CallTo(() => ops.GetSchemaVersionAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => history.QueryHistoryAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => migrationHistory.QueryHistoryAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => inspection.CountDocumentsAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		result.IsCompatible.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteMigrationAsync_OnSeamFailure_ReportsFailureAndPersistsResult()
	{
		// Arrange — ensure critical-step failure path propagates to result.
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();
		A.CallTo(() => ops.EnsureMigrationIndexAsync(A<string>._, A<object?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new MigrationStepOutcome(Success: false, ErrorDetails: "cluster red")));
		A.CallTo(() => history.EnsureHistoryIndexAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => migrationHistory.EnsureHistoryIndexAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => migrationHistory.WriteMigrationResultAsync(
				A<string>._, A<string>._, A<MigrationHistoryRecord>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => inspection.CountDocumentsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long?>(0));

		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);
		var request = new SchemaMigrationRequest
		{
			ProjectionType = ProjectionType,
			SourceIndex = SourceIndex,
			TargetIndex = TargetIndex,
			Strategy = MigrationStrategy.Reindex,
			NewSchema = new { mapping = "placeholder" },
		};
		var plan = await handler.PlanMigrationAsync(request, CancellationToken.None);

		// Act
		var result = await handler.ExecuteMigrationAsync(plan, CancellationToken.None);

		// Assert — CreateIndex failure is critical → migration short-circuits, Reindex not called,
		// failing result is still persisted.
		A.CallTo(() => ops.EnsureMigrationIndexAsync(TargetIndex, A<object?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => ops.MigrateAsync(A<string>._, A<string>._, A<object?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => migrationHistory.WriteMigrationResultAsync(
				A<string>._, A<string>._, A<MigrationHistoryRecord>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public async Task CompareSchemaAsync_OnNullOrWhitespaceIndex_ThrowsArgumentException()
	{
		// Arrange
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();
		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);

		// Act + Assert — guard clauses short-circuit before any seam invocation.
		_ = await Should.ThrowAsync<ArgumentException>(
			() => handler.CompareSchemaAsync(null!, TargetIndex, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(
			() => handler.CompareSchemaAsync("   ", TargetIndex, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentException>(
			() => handler.CompareSchemaAsync(SourceIndex, null!, CancellationToken.None));
		A.CallTo(() => ops.GetSchemaVersionAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PlanMigrationAsync_OnNullRequest_ThrowsArgumentNullException()
	{
		// Arrange
		var (ops, history, migrationHistory, inspection, aliasManager, options, logger) = CreateSeamFakes();
		using var handler = new SchemaEvolutionHandler(
			ops, history, migrationHistory, inspection, aliasManager, options, logger);

		// Act + Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => handler.PlanMigrationAsync(null!, CancellationToken.None));
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
			IndexPrefix = IndexPrefix,
			SchemaEvolution = new SchemaEvolutionOptions { AllowBreakingChanges = false },
		});
		var logger = A.Fake<ILogger<SchemaEvolutionHandler>>();
		return (ops, history, migrationHistory, inspection, aliasManager, options, logger);
	}
}