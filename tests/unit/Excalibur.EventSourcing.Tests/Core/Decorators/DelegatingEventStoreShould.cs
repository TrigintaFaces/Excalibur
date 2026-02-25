using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Decorators;

namespace Excalibur.EventSourcing.Tests.Core.Decorators;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DelegatingEventStoreShould
{
	private readonly IEventStore _inner;
	private readonly TestDelegatingEventStore _sut;

	public DelegatingEventStoreShould()
	{
		_inner = A.Fake<IEventStore>();
		_sut = new TestDelegatingEventStore(_inner);
	}

	[Fact]
	public async Task DelegateLoadAsync_ToInner()
	{
		// Arrange
		var expected = new List<StoredEvent> { CreateStoredEvent() };
#pragma warning disable CA2012
		A.CallTo(() => _inner.LoadAsync("agg-1", "type", A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(expected));
#pragma warning restore CA2012

		// Act
		var result = await _sut.LoadAsync("agg-1", "type", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task DelegateLoadAsync_WithFromVersion_ToInner()
	{
		// Arrange
		var expected = new List<StoredEvent> { CreateStoredEvent() };
#pragma warning disable CA2012
		A.CallTo(() => _inner.LoadAsync("agg-1", "type", 5L, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(expected));
#pragma warning restore CA2012

		// Act
		var result = await _sut.LoadAsync("agg-1", "type", 5L, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task DelegateAppendAsync_ToInner()
	{
		// Arrange
		var events = Array.Empty<IDomainEvent>();
		var expected = AppendResult.CreateSuccess(1, 0);
#pragma warning disable CA2012
		A.CallTo(() => _inner.AppendAsync("agg-1", "type", events, 0L, A<CancellationToken>._))
			.Returns(new ValueTask<AppendResult>(expected));
#pragma warning restore CA2012

		// Act
		var result = await _sut.AppendAsync("agg-1", "type", events, 0L, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task DelegateGetUndispatchedEventsAsync_ToInner()
	{
		// Arrange
		var expected = new List<StoredEvent> { CreateStoredEvent() };
#pragma warning disable CA2012
		A.CallTo(() => _inner.GetUndispatchedEventsAsync(100, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(expected));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetUndispatchedEventsAsync(100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task DelegateMarkEventAsDispatchedAsync_ToInner()
	{
		// Act
		await _sut.MarkEventAsDispatchedAsync("event-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _inner.MarkEventAsDispatchedAsync("event-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ThrowOnNullInner()
	{
		Should.Throw<ArgumentNullException>(() => new TestDelegatingEventStore(null!));
	}

	[Fact]
	public void ExposeInnerProperty()
	{
		_sut.ExposedInner.ShouldBeSameAs(_inner);
	}

	private static StoredEvent CreateStoredEvent() =>
		new(
			Guid.NewGuid().ToString(),
			"agg-1",
			"type",
			"TestEvent",
			Array.Empty<byte>(),
			null,
			1,
			DateTimeOffset.UtcNow,
			false);

	private sealed class TestDelegatingEventStore : DelegatingEventStore
	{
		public TestDelegatingEventStore(IEventStore inner) : base(inner) { }
		public IEventStore ExposedInner => Inner;
	}
}
