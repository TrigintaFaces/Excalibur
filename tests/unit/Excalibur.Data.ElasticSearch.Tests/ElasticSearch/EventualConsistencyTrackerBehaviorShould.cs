// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Behavioral coverage for <see cref="EventualConsistencyTracker"/> via the four
/// S801 <c>bd-r3xkes</c> operation-axis seams
/// (<see cref="IProjectionEventIngest"/>, <see cref="IProjectionEventLookup"/>,
/// <see cref="IProjectionEventScan"/>, <see cref="IProjectionIndexProvisioning"/>).
/// Closes the S801 coverage gap identified in CRUCIBLE msg 1919: seam 4/6 landed
/// in <c>1cd409ad2</c> without paired behavioral test migration. Exercises the
/// tracker through the internal test-seam ctor so no real SDK is in play.
/// </summary>
/// <remarks>
/// ADR-142 §D7 seam-passthrough discipline: assertions target seam-method
/// invocations, not Elastic cluster behavior. A real-SDK smoke per adapter
/// lives under the integration shard
/// (<c>tests/integration/Excalibur.Integration.Tests/DataElasticSearch/Conformance/</c>).
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class EventualConsistencyTrackerBehaviorShould : UnitTestBase
{
	private const string IndexPrefix = "test";
	private const string WriteIndex = "test-consistency-writes";
	private const string ReadIndex = "test-consistency-reads";
	private const string CheckpointIndex = "test-consistency-checkpoints";

	[Fact]
	public void Constructor_InternalTestSeam_ThrowsOnNullIngest()
	{
		var (_, lookup, scan, provisioning, options, logger) = CreateSeamFakes();
		_ = Should.Throw<ArgumentNullException>(() =>
			new EventualConsistencyTracker(null!, lookup, scan, provisioning, options, logger));
	}

	[Fact]
	public void Constructor_InternalTestSeam_ThrowsOnNullLookup()
	{
		var (ingest, _, scan, provisioning, options, logger) = CreateSeamFakes();
		_ = Should.Throw<ArgumentNullException>(() =>
			new EventualConsistencyTracker(ingest, null!, scan, provisioning, options, logger));
	}

	[Fact]
	public void Constructor_InternalTestSeam_ThrowsOnNullScan()
	{
		var (ingest, lookup, _, provisioning, options, logger) = CreateSeamFakes();
		_ = Should.Throw<ArgumentNullException>(() =>
			new EventualConsistencyTracker(ingest, lookup, null!, provisioning, options, logger));
	}

	[Fact]
	public void Constructor_InternalTestSeam_ThrowsOnNullProvisioning()
	{
		var (ingest, lookup, scan, _, options, logger) = CreateSeamFakes();
		_ = Should.Throw<ArgumentNullException>(() =>
			new EventualConsistencyTracker(ingest, lookup, scan, null!, options, logger));
	}

	[Fact]
	public async Task TrackWriteModelEventAsync_WhenTrackingEnabled_IndexesDocumentViaIngestSeam()
	{
		// Arrange
		var (ingest, lookup, scan, provisioning, options, logger) = CreateSeamFakes(enabled: true);
		A.CallTo(() => provisioning.IndexExistsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => ingest.IndexWriteEventAsync(A<WriteEventDocument>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		using var tracker = new EventualConsistencyTracker(ingest, lookup, scan, provisioning, options, logger);

		// Act
		await tracker.TrackWriteModelEventAsync(
			"evt-1", "agg-1", "OrderPlaced", DateTime.UtcNow, CancellationToken.None);

		// Assert — ingest seam invoked with the framework DTO keyed by event id.
		A.CallTo(() => ingest.IndexWriteEventAsync(
				A<WriteEventDocument>.That.Matches(d =>
					d.EventId == "evt-1" &&
					d.AggregateId == "agg-1" &&
					d.EventType == "OrderPlaced"),
				"evt-1",
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task TrackWriteModelEventAsync_WhenTrackingDisabled_DoesNotTouchAnySeam()
	{
		var (ingest, lookup, scan, provisioning, options, logger) = CreateSeamFakes(enabled: false);
		using var tracker = new EventualConsistencyTracker(ingest, lookup, scan, provisioning, options, logger);

		await tracker.TrackWriteModelEventAsync(
			"evt-1", "agg-1", "OrderPlaced", DateTime.UtcNow, CancellationToken.None);

		A.CallTo(ingest).MustNotHaveHappened();
		A.CallTo(provisioning).MustNotHaveHappened();
	}

	[Fact]
	public async Task TrackReadModelProjectionAsync_WhenEnabled_IndexesBothReadEventAndCheckpoint()
	{
		var (ingest, lookup, scan, provisioning, options, logger) = CreateSeamFakes(enabled: true);
		A.CallTo(() => provisioning.IndexExistsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => ingest.IndexReadEventAsync(A<ReadEventDocument>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => ingest.IndexCheckpointAsync(A<ProjectionCheckpointDocument>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		using var tracker = new EventualConsistencyTracker(ingest, lookup, scan, provisioning, options, logger);

		await tracker.TrackReadModelProjectionAsync(
			"evt-1", "OrderProjection", DateTime.UtcNow, CancellationToken.None);

		A.CallTo(() => ingest.IndexReadEventAsync(
				A<ReadEventDocument>.That.Matches(d =>
					d.EventId == "evt-1" && d.ProjectionType == "OrderProjection"),
				"evt-1:OrderProjection",
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => ingest.IndexCheckpointAsync(
				A<ProjectionCheckpointDocument>.That.Matches(d =>
					d.ProjectionType == "OrderProjection" && d.LastEventId == "evt-1"),
				"OrderProjection",
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task EnsureIndicesAsync_WhenIndexMissing_CreatesViaProvisioningSeam()
	{
		var (ingest, lookup, scan, provisioning, options, logger) = CreateSeamFakes(enabled: true);
		A.CallTo(() => provisioning.IndexExistsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(false));
		A.CallTo(() => provisioning.CreateIndexAsync(A<string>._, A<ConsistencyIndexKind>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => ingest.IndexWriteEventAsync(A<WriteEventDocument>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		using var tracker = new EventualConsistencyTracker(ingest, lookup, scan, provisioning, options, logger);

		// Act — any tracking call triggers EnsureIndicesAsync.
		await tracker.TrackWriteModelEventAsync(
			"evt-1", "agg-1", "OrderPlaced", DateTime.UtcNow, CancellationToken.None);

		// Assert — provisioning seam called once per index kind.
		A.CallTo(() => provisioning.CreateIndexAsync(WriteIndex, ConsistencyIndexKind.WriteEvents, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => provisioning.CreateIndexAsync(ReadIndex, ConsistencyIndexKind.ReadEvents, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => provisioning.CreateIndexAsync(CheckpointIndex, ConsistencyIndexKind.Checkpoints, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetConsistencyLagAsync_WhenEnabled_ReadsFromScanAndLookupSeams()
	{
		var (ingest, lookup, scan, provisioning, options, logger) = CreateSeamFakes(enabled: true);
		A.CallTo(() => provisioning.IndexExistsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => scan.GetLatestWriteTimestampAsync(A<CancellationToken>._))
			.Returns(Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow));
		A.CallTo(() => lookup.GetLatestReadForProjectionAsync("OrderProjection", A<CancellationToken>._))
			.Returns(Task.FromResult<ReadEventDocument?>(new ReadEventDocument
			{
				EventId = "evt-1",
				ProjectionType = "OrderProjection",
				ReadTimestamp = DateTimeOffset.UtcNow,
			}));
		A.CallTo(() => scan.SearchReadsAsync(A<ReadEventSearch>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<ReadEventDocument>>([]));
		A.CallTo(() => scan.GetDocumentCountAsync(A<string>._, A<ProjectionCountFilter>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<long>(3));
		using var tracker = new EventualConsistencyTracker(ingest, lookup, scan, provisioning, options, logger);

		var lag = await tracker.GetConsistencyLagAsync("OrderProjection", CancellationToken.None);

		lag.ShouldNotBeNull();
		lag.ProjectionType.ShouldBe("OrderProjection");
		A.CallTo(() => scan.GetLatestWriteTimestampAsync(A<CancellationToken>._)).MustHaveHappened();
		A.CallTo(() => lookup.GetLatestReadForProjectionAsync("OrderProjection", A<CancellationToken>._)).MustHaveHappened();
		A.CallTo(() => scan.GetDocumentCountAsync(WriteIndex, ProjectionCountFilter.All, null, A<CancellationToken>._)).MustHaveHappened();
		A.CallTo(() => scan.GetDocumentCountAsync(ReadIndex, ProjectionCountFilter.ReadsByProjectionType, "OrderProjection", A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task IsEventFullyProcessedAsync_WhenWriteMissing_ReturnsFalseWithoutProbingReads()
	{
		var (ingest, lookup, scan, provisioning, options, logger) = CreateSeamFakes(enabled: true);
		A.CallTo(() => provisioning.IndexExistsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => lookup.GetWriteEventByIdAsync("evt-missing", A<CancellationToken>._))
			.Returns(Task.FromResult<WriteEventDocument?>(null));
		using var tracker = new EventualConsistencyTracker(ingest, lookup, scan, provisioning, options, logger);

		var result = await tracker.IsEventFullyProcessedAsync("evt-missing", CancellationToken.None);

		result.ShouldBeFalse();
		A.CallTo(() => scan.SearchReadsAsync(A<ReadEventSearch>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task IsEventFullyProcessedAsync_WhenAllProjectionsProcessed_ReturnsTrue()
	{
		var (ingest, lookup, scan, provisioning, options, logger) = CreateSeamFakes(enabled: true);
		A.CallTo(() => provisioning.IndexExistsAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		A.CallTo(() => lookup.GetWriteEventByIdAsync("evt-1", A<CancellationToken>._))
			.Returns(Task.FromResult<WriteEventDocument?>(new WriteEventDocument
			{
				EventId = "evt-1",
				AggregateId = "agg-1",
				EventType = "OrderPlaced",
				WriteTimestamp = DateTimeOffset.UtcNow,
			}));
		A.CallTo(() => lookup.GetProjectionTypesAsync(A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<string>>(["OrderProjection", "AuditProjection"]));
		A.CallTo(() => scan.SearchReadsAsync(A<ReadEventSearch>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<ReadEventDocument>>(
			[
				new() { EventId = "evt-1", ProjectionType = "OrderProjection", ReadTimestamp = DateTimeOffset.UtcNow },
				new() { EventId = "evt-1", ProjectionType = "AuditProjection", ReadTimestamp = DateTimeOffset.UtcNow },
			]));
		using var tracker = new EventualConsistencyTracker(ingest, lookup, scan, provisioning, options, logger);

		var result = await tracker.IsEventFullyProcessedAsync("evt-1", CancellationToken.None);

		result.ShouldBeTrue();
	}

	private static (
		IProjectionEventIngest Ingest,
		IProjectionEventLookup Lookup,
		IProjectionEventScan Scan,
		IProjectionIndexProvisioning Provisioning,
		IOptions<ProjectionOptions> Options,
		ILogger<EventualConsistencyTracker> Logger)
		CreateSeamFakes(bool enabled = false)
	{
		var ingest = A.Fake<IProjectionEventIngest>();
		var lookup = A.Fake<IProjectionEventLookup>();
		var scan = A.Fake<IProjectionEventScan>();
		var provisioning = A.Fake<IProjectionIndexProvisioning>();
		var options = Options.Create(new ProjectionOptions
		{
			IndexPrefix = IndexPrefix,
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = enabled,
				ExpectedMaxLag = TimeSpan.FromSeconds(30),
			},
		});
		var logger = A.Fake<ILogger<EventualConsistencyTracker>>();
		return (ingest, lookup, scan, provisioning, options, logger);
	}
}