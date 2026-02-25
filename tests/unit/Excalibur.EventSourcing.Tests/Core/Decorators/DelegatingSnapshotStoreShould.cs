using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Decorators;

namespace Excalibur.EventSourcing.Tests.Core.Decorators;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DelegatingSnapshotStoreShould
{
	private readonly ISnapshotStore _inner;
	private readonly TestDelegatingSnapshotStore _sut;

	public DelegatingSnapshotStoreShould()
	{
		_inner = A.Fake<ISnapshotStore>();
		_sut = new TestDelegatingSnapshotStore(_inner);
	}

	[Fact]
	public async Task DelegateGetLatestSnapshotAsync_ToInner()
	{
		// Arrange
		var snapshot = A.Fake<ISnapshot>();
#pragma warning disable CA2012
		A.CallTo(() => _inner.GetLatestSnapshotAsync("agg-1", "type", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(snapshot));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "type", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(snapshot);
	}

	[Fact]
	public async Task DelegateGetLatestSnapshotAsync_ReturnsNull()
	{
#pragma warning disable CA2012
		A.CallTo(() => _inner.GetLatestSnapshotAsync("agg-1", "type", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));
#pragma warning restore CA2012

		var result = await _sut.GetLatestSnapshotAsync("agg-1", "type", CancellationToken.None).ConfigureAwait(false);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task DelegateSaveSnapshotAsync_ToInner()
	{
		var snapshot = A.Fake<ISnapshot>();
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _inner.SaveSnapshotAsync(snapshot, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateDeleteSnapshotsAsync_ToInner()
	{
		await _sut.DeleteSnapshotsAsync("agg-1", "type", CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _inner.DeleteSnapshotsAsync("agg-1", "type", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateDeleteSnapshotsOlderThanAsync_ToInner()
	{
		await _sut.DeleteSnapshotsOlderThanAsync("agg-1", "type", 10L, CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _inner.DeleteSnapshotsOlderThanAsync("agg-1", "type", 10L, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ThrowOnNullInner()
	{
		Should.Throw<ArgumentNullException>(() => new TestDelegatingSnapshotStore(null!));
	}

	[Fact]
	public void ExposeInnerProperty()
	{
		_sut.ExposedInner.ShouldBeSameAs(_inner);
	}

	private sealed class TestDelegatingSnapshotStore : DelegatingSnapshotStore
	{
		public TestDelegatingSnapshotStore(ISnapshotStore inner) : base(inner) { }
		public ISnapshotStore ExposedInner => Inner;
	}
}
