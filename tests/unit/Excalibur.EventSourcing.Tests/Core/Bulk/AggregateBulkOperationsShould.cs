using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Bulk;

namespace Excalibur.EventSourcing.Tests.Core.Bulk;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AggregateBulkOperationsShould
{
	private readonly IEventSourcedRepository<TestBulkAggregate, string> _repository;
	private readonly AggregateBulkOperations<TestBulkAggregate, string> _sut;

	public AggregateBulkOperationsShould()
	{
		_repository = A.Fake<IEventSourcedRepository<TestBulkAggregate, string>>();
		_sut = new AggregateBulkOperations<TestBulkAggregate, string>(_repository);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public async Task LoadManyAsync_ReturnFoundAggregates()
	{
		// Arrange
		var agg1 = new TestBulkAggregate("id-1");
		var agg2 = new TestBulkAggregate("id-2");
		A.CallTo(() => _repository.GetByIdAsync("id-1", A<CancellationToken>._)).Returns(agg1);
		A.CallTo(() => _repository.GetByIdAsync("id-2", A<CancellationToken>._)).Returns(agg2);
		A.CallTo(() => _repository.GetByIdAsync("id-3", A<CancellationToken>._)).Returns((TestBulkAggregate?)null);

		// Act
		var result = await _sut.LoadManyAsync(["id-1", "id-2", "id-3"], CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(2);
		result["id-1"].ShouldBeSameAs(agg1);
		result["id-2"].ShouldBeSameAs(agg2);
		result.ContainsKey("id-3").ShouldBeFalse();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public async Task LoadManyAsync_ReturnEmptyForEmptyInput()
	{
		// Act
		var result = await _sut.LoadManyAsync(Array.Empty<string>(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count.ShouldBe(0);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public async Task LoadManyAsync_ThrowOnNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.LoadManyAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public async Task LoadManyAsync_RespectCancellation()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => _sut.LoadManyAsync(["id-1"], cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public async Task SaveManyAsync_SaveAllAggregatesSuccessfully()
	{
		// Arrange
		var agg1 = new TestBulkAggregate("id-1");
		var agg2 = new TestBulkAggregate("id-2");

		// Act
		var result = await _sut.SaveManyAsync([agg1, agg2], CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.SuccessCount.ShouldBe(2);
		result.FailureCount.ShouldBe(0);
		result.AllSucceeded.ShouldBeTrue();
		result.Failures.ShouldBeEmpty();
		A.CallTo(() => _repository.SaveAsync(agg1, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _repository.SaveAsync(agg2, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public async Task SaveManyAsync_RecordFailures()
	{
		// Arrange
		var agg1 = new TestBulkAggregate("id-1");
		var agg2 = new TestBulkAggregate("id-2");
		A.CallTo(() => _repository.SaveAsync(agg2, A<CancellationToken>._))
			.Throws(new InvalidOperationException("save failed"));

		// Act
		var result = await _sut.SaveManyAsync([agg1, agg2], CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(1);
		result.AllSucceeded.ShouldBeFalse();
		result.Failures.Count.ShouldBe(1);
		result.Failures[0].AggregateId.ShouldBe("id-2");
		result.Failures[0].Exception.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public async Task SaveManyAsync_ThrowOnNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveManyAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public async Task SaveManyAsync_RespectCancellation()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);
		var agg = new TestBulkAggregate("id-1");

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => _sut.SaveManyAsync([agg], cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public void ThrowOnNullRepository()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AggregateBulkOperations<TestBulkAggregate, string>(null!));
	}
}

#pragma warning disable CA1034
public sealed class TestBulkAggregate : AggregateRoot<string>, IAggregateSnapshotSupport
{
	public TestBulkAggregate(string id) => Id = id;

	public long SnapshotVersion { get; set; }
	public void RestoreFromSnapshot(ISnapshot snapshot) { }
	public new ISnapshot CreateSnapshot() => A.Fake<ISnapshot>();
	protected override void ApplyEventInternal(IDomainEvent @event) { }
}
#pragma warning restore CA1034
