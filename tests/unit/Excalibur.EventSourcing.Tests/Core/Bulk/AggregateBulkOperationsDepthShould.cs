// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Bulk;

namespace Excalibur.EventSourcing.Tests.Core.Bulk;

/// <summary>
/// Depth coverage tests for <see cref="AggregateBulkOperations{TAggregate,TKey}"/>.
/// Covers LoadManyAsync, SaveManyAsync, failure handling, null guards, and cancellation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AggregateBulkOperationsDepthShould
{
	// Reuses TestBulkAggregate from AggregateBulkOperationsShould.cs in same namespace
	private readonly IEventSourcedRepository<TestBulkAggregate, string> _repository =
		A.Fake<IEventSourcedRepository<TestBulkAggregate, string>>();

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AggregateBulkOperations<TestBulkAggregate, string>(null!));
	}

	[Fact]
	public async Task LoadManyAsync_ThrowsArgumentNullException_WhenIdsIsNull()
	{
		var bulk = new AggregateBulkOperations<TestBulkAggregate, string>(_repository);
		await Should.ThrowAsync<ArgumentNullException>(() =>
			bulk.LoadManyAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task LoadManyAsync_ReturnsEmpty_WhenNoIdsProvided()
	{
		// Arrange
		var bulk = new AggregateBulkOperations<TestBulkAggregate, string>(_repository);

		// Act
		var result = await bulk.LoadManyAsync([], CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task LoadManyAsync_ReturnsFoundAggregates()
	{
		// Arrange
		var agg1 = new TestBulkAggregate("agg-1");
		A.CallTo(() => _repository.GetByIdAsync("agg-1", A<CancellationToken>._)).Returns(agg1);
		A.CallTo(() => _repository.GetByIdAsync("agg-2", A<CancellationToken>._)).Returns((TestBulkAggregate?)null);

		var bulk = new AggregateBulkOperations<TestBulkAggregate, string>(_repository);

		// Act
		var result = await bulk.LoadManyAsync(["agg-1", "agg-2"], CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result.ShouldContainKey("agg-1");
	}

	[Fact]
	public async Task LoadManyAsync_RespectsCancel()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		var bulk = new AggregateBulkOperations<TestBulkAggregate, string>(_repository);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(() =>
			bulk.LoadManyAsync(["agg-1"], cts.Token));
	}

	[Fact]
	public async Task SaveManyAsync_ThrowsArgumentNullException_WhenAggregatesIsNull()
	{
		var bulk = new AggregateBulkOperations<TestBulkAggregate, string>(_repository);
		await Should.ThrowAsync<ArgumentNullException>(() =>
			bulk.SaveManyAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task SaveManyAsync_SavesAllSuccessfully()
	{
		// Arrange
		var aggregates = new[] { new TestBulkAggregate("a"), new TestBulkAggregate("b") };
		var bulk = new AggregateBulkOperations<TestBulkAggregate, string>(_repository);

		// Act
		var result = await bulk.SaveManyAsync(aggregates, CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(2);
		result.FailureCount.ShouldBe(0);
		result.Failures.ShouldBeEmpty();
	}

	[Fact]
	public async Task SaveManyAsync_CapturesFailures()
	{
		// Arrange
		var agg1 = new TestBulkAggregate("a");
		var agg2 = new TestBulkAggregate("b");

		A.CallTo(() => _repository.SaveAsync(agg2, A<CancellationToken>._))
			.Throws(new InvalidOperationException("save failed"));

		var bulk = new AggregateBulkOperations<TestBulkAggregate, string>(_repository);

		// Act
		var result = await bulk.SaveManyAsync([agg1, agg2], CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(1);
		result.Failures[0].AggregateId.ShouldBe("b");
	}

	[Fact]
	public async Task SaveManyAsync_RespectsCancel()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		var bulk = new AggregateBulkOperations<TestBulkAggregate, string>(_repository);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(() =>
			bulk.SaveManyAsync([new TestBulkAggregate("a")], cts.Token));
	}
}
