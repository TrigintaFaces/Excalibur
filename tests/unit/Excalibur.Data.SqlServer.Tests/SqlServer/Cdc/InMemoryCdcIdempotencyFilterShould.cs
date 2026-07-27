// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="InMemoryCdcIdempotencyFilter"/>.
/// Covers happy path, capacity limits, concurrent access, and null argument validation.
/// </summary>
/// <remarks>
/// Sprint 825 — bd-jmx38a: CDC idempotency filtering.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class InMemoryCdcIdempotencyFilterShould : UnitTestBase
{
	/// <summary>The asking consumer. These arms are not about consumer isolation, so one identity is used
	/// throughout; CdcIdempotencyConsumerCollisionShould is where two identities are contrasted.</summary>
	private const string TestConsumer = "test-consumer";

	private static readonly byte[] SampleLsn = [0x00, 0x00, 0x00, 0x01];
	private static readonly byte[] SampleSeqVal = [0x00, 0x01];
	private const string SampleTable = "dbo_Orders";

	#region Happy Path

	[Fact]
	public async Task ReturnFalse_WhenEventHasNotBeenProcessed()
	{
		// Arrange
		var filter = CreateFilter();

		// Act
		var result = await filter.IsProcessedAsync(SampleTable, SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnTrue_AfterEventIsMarkedProcessed()
	{
		// Arrange
		var filter = CreateFilter();

		// Act
		await filter.MarkProcessedAsync(SampleTable, SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None);
		var result = await filter.IsProcessedAsync(SampleTable, SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task TrackCountAfterMarkingProcessed()
	{
		// Arrange
		var filter = CreateFilter();

		// Act
		await filter.MarkProcessedAsync(SampleTable, SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None);

		// Assert
		filter.Count.ShouldBe(1);
	}

	[Fact]
	public async Task DistinguishDifferentEvents()
	{
		// Arrange
		var filter = CreateFilter();
		var lsn1 = new byte[] { 0x01 };
		var lsn2 = new byte[] { 0x02 };
		var seqVal = new byte[] { 0x01 };

		// Act
		await filter.MarkProcessedAsync(SampleTable, lsn1, seqVal, TestConsumer, CancellationToken.None);

		// Assert — same table, different LSN is NOT processed
		var result = await filter.IsProcessedAsync(SampleTable, lsn2, seqVal, TestConsumer, CancellationToken.None);
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task DistinguishDifferentTables_WithSameLsnAndSeqVal()
	{
		// Arrange
		var filter = CreateFilter();

		// Act
		await filter.MarkProcessedAsync("dbo_Orders", SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None);

		// Assert — different table, same LSN+SeqVal is NOT processed
		var result = await filter.IsProcessedAsync("dbo_Customers", SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None);
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task DistinguishDifferentSeqVals_WithSameLsnAndTable()
	{
		// Arrange
		var filter = CreateFilter();
		var seqVal1 = new byte[] { 0x01 };
		var seqVal2 = new byte[] { 0x02 };

		// Act
		await filter.MarkProcessedAsync(SampleTable, SampleLsn, seqVal1, TestConsumer, CancellationToken.None);

		// Assert
		var result = await filter.IsProcessedAsync(SampleTable, SampleLsn, seqVal2, TestConsumer, CancellationToken.None);
		result.ShouldBeFalse();
	}

	#endregion

	#region Idempotent Mark

	[Fact]
	public async Task MarkProcessedIdempotently_WithSameEventTwice()
	{
		// Arrange
		var filter = CreateFilter();

		// Act
		await filter.MarkProcessedAsync(SampleTable, SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None);
		await filter.MarkProcessedAsync(SampleTable, SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None);

		// Assert — count should still be 1 (TryAdd is idempotent)
		filter.Count.ShouldBe(1);
		var result = await filter.IsProcessedAsync(SampleTable, SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None);
		result.ShouldBeTrue();
	}

	#endregion

	#region Capacity Limit (Fail-Closed)

	[Fact]
	public async Task FailClosed_WhenIsProcessedSeesNewEvent_AtCapacity()
	{
		// Arrange — capacity of 3
		var filter = CreateFilter(capacity: 3);

		// Fill to capacity
		for (var i = 0; i < 3; i++)
		{
			await filter.MarkProcessedAsync(SampleTable, new byte[] { (byte)i }, SampleSeqVal, TestConsumer, CancellationToken.None);
		}

		filter.Count.ShouldBe(3);

		// MarkProcessedAsync stays best-effort — it runs AFTER the handler, so it must never throw.
		await Should.NotThrowAsync(
			() => filter.MarkProcessedAsync(SampleTable, new byte[] { 0xFF }, SampleSeqVal, TestConsumer, CancellationToken.None));
		filter.Count.ShouldBe(3);

		// IsProcessedAsync is the pre-process fail-closed gate: a not-yet-seen event at capacity cannot be
		// deduplicated, so it throws transient rather than admit it un-tracked (which would risk a double-
		// process on redelivery).
		await Should.ThrowAsync<CdcIdempotencyCapacityExceededException>(
			() => filter.IsProcessedAsync(SampleTable, new byte[] { 0xFE }, SampleSeqVal, TestConsumer, CancellationToken.None));
	}

	[Fact]
	public async Task StillReturnTrue_ForExistingEvents_WhenCapacityReached()
	{
		// Arrange — capacity of 2
		var filter = CreateFilter(capacity: 2);
		var lsn1 = new byte[] { 0x01 };
		var lsn2 = new byte[] { 0x02 };

		await filter.MarkProcessedAsync(SampleTable, lsn1, SampleSeqVal, TestConsumer, CancellationToken.None);
		await filter.MarkProcessedAsync(SampleTable, lsn2, SampleSeqVal, TestConsumer, CancellationToken.None);

		// Act & Assert — already-tracked events are still found
		(await filter.IsProcessedAsync(SampleTable, lsn1, SampleSeqVal, TestConsumer, CancellationToken.None)).ShouldBeTrue();
		(await filter.IsProcessedAsync(SampleTable, lsn2, SampleSeqVal, TestConsumer, CancellationToken.None)).ShouldBeTrue();
	}

	#endregion

	#region Concurrent Access

	[Fact]
	public async Task HandleConcurrentMarkAndCheck_WithoutErrors()
	{
		// Arrange
		var filter = CreateFilter(capacity: 1000);
		var tasks = new List<Task>();

		// Act — 50 concurrent marks + 50 concurrent checks
		for (var i = 0; i < 50; i++)
		{
			var lsn = new byte[] { (byte)(i / 256), (byte)(i % 256) };
			tasks.Add(filter.MarkProcessedAsync(SampleTable, lsn, SampleSeqVal, TestConsumer, CancellationToken.None));
			tasks.Add(filter.IsProcessedAsync(SampleTable, lsn, SampleSeqVal, TestConsumer, CancellationToken.None));
		}

		// Assert — no exceptions
		await Task.WhenAll(tasks);
		filter.Count.ShouldBeGreaterThan(0);
		filter.Count.ShouldBeLessThanOrEqualTo(50);
	}

	#endregion

	#region Argument Validation

	[Fact]
	public async Task ThrowArgumentNullException_WhenIsProcessed_TableNameIsNull()
	{
		var filter = CreateFilter();
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.IsProcessedAsync(null!, SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenIsProcessed_LsnIsNull()
	{
		var filter = CreateFilter();
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.IsProcessedAsync(SampleTable, null!, SampleSeqVal, TestConsumer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenIsProcessed_SeqValIsNull()
	{
		var filter = CreateFilter();
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.IsProcessedAsync(SampleTable, SampleLsn, null!, TestConsumer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenMarkProcessed_TableNameIsNull()
	{
		var filter = CreateFilter();
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.MarkProcessedAsync(null!, SampleLsn, SampleSeqVal, TestConsumer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenMarkProcessed_LsnIsNull()
	{
		var filter = CreateFilter();
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.MarkProcessedAsync(SampleTable, null!, SampleSeqVal, TestConsumer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenMarkProcessed_SeqValIsNull()
	{
		var filter = CreateFilter();
		await Should.ThrowAsync<ArgumentNullException>(
			() => filter.MarkProcessedAsync(SampleTable, SampleLsn, null!, TestConsumer, CancellationToken.None));
	}

	#endregion

	#region Constructor Validation

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new InMemoryCdcIdempotencyFilter(null!));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void ThrowArgumentOutOfRangeException_WhenCapacityIsInvalid(int capacity)
	{
		Should.Throw<ArgumentOutOfRangeException>(
			() => new InMemoryCdcIdempotencyFilter(capacity, NullLogger<InMemoryCdcIdempotencyFilter>.Instance));
	}

	[Fact]
	public void ExposeDefaultCapacityConstant()
	{
		InMemoryCdcIdempotencyFilter.DefaultCapacity.ShouldBe(10_000);
	}

	#endregion

	#region Helpers

	private static InMemoryCdcIdempotencyFilter CreateFilter(int capacity = InMemoryCdcIdempotencyFilter.DefaultCapacity)
		=> new(capacity, NullLogger<InMemoryCdcIdempotencyFilter>.Instance);

	#endregion
}
