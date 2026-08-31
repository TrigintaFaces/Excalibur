using Excalibur.Dispatch;
using Excalibur.EventSourcing;
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
	public void ThrowOnNullInner()
	{
		Should.Throw<ArgumentNullException>(() => new TestDelegatingEventStore(null!));
	}

	[Fact]
	public void ExposeInnerProperty()
	{
		_sut.ExposedInner.ShouldBeSameAs(_inner);
	}

	// --- erasure capability probe -------------------------------------------------------------
	// The base DECLARES IEventStoreErasure unconditionally (C# has no conditional declaration) while
	// implementing none of its own -- both members forward inward. So a plain type test on the
	// declaration answers the probe with the decorator over EVERY inner store, including one that
	// cannot erase: a probe that always says yes. IEventStoreErasure.cs states the contract these arms
	// bind -- "a decorator that cannot honour the capability over its inner store answers null rather
	// than claiming it".

	[Fact]
	public void AnswerNullForTheErasureProbeWhenTheInnerChainCannotErase()
	{
		// SAFETY. _inner is an IEventStore and nothing more, so nothing beneath this decorator can
		// erase. Claiming the capability here is what lets the DI guard admit the store and then throw
		// at the first erase, long after the configuration that caused it.
		var sut = new TestDelegatingEventStore(new PlainEventStore());

		sut.GetService(typeof(IEventStoreErasure)).ShouldBeNull(
			"a decorator over a store that cannot erase must answer null, not claim the capability on the "
			+ "strength of its own base-class declaration");
	}

	[Fact]
	public void AnswerWithItselfForTheErasureProbeWhenTheInnerChainCanErase()
	{
		// LIVENESS, and not merely the opposite of the arm above: answering null for EVERYTHING would
		// satisfy that one completely. The returned instance must also be THE DECORATOR, never the
		// inner store -- handing back the inner would route the erase around whatever this decorator
		// imposes, which is the bypass the forwarding exists to prevent.
		var erasingInner = new ErasureCapableEventStore();
		var sut = new TestDelegatingEventStore(erasingInner);

		var resolved = sut.GetService(typeof(IEventStoreErasure));

		_ = resolved.ShouldBeOfType<TestDelegatingEventStore>(
			"the probe must resolve THROUGH the decorator, not hand back the inner store");
		resolved.ShouldBeSameAs(sut);
	}

	[Fact]
	public async Task ReachTheInnerErasureThroughTheDecoratorItReturns()
	{
		// The probe answering is worth nothing if the thing it hands back cannot erase. Without this,
		// both arms above pass over a decorator that returns itself and then throws.
		var erasingInner = new ErasureCapableEventStore();
		var sut = new TestDelegatingEventStore(erasingInner);

		var erasure = (IEventStoreErasure)sut.GetService(typeof(IEventStoreErasure))!;
		var erased = await erasure.EraseEventsAsync("agg-1", "type", Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		erased.ShouldBe(1);
		erasingInner.EraseCalls.ShouldBe(1, "the erase must land on the inner store, through the decorator");
	}

	[Fact]
	public void LetASubclassThatErasesItselfAnswerTheProbeOverANonErasingInner()
	{
		// The documented extension point, and the constraint that rules out "just delete the base
		// declaration" as a fix. A subclass that performs the erasure ITSELF rather than forwarding
		// must still be able to claim the capability over an inner store that has none -- resolving
		// strictly against the inner chain would wrongly deny it.
		var sut = new SelfErasingDelegatingEventStore(new PlainEventStore());

		sut.GetService(typeof(IEventStoreErasure)).ShouldBeSameAs(
			sut,
			"a subclass that overrides GetService to claim its own erasure must keep that answer");
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
			DateTimeOffset.UtcNow);

	private sealed class TestDelegatingEventStore : DelegatingEventStore
	{
		public TestDelegatingEventStore(IEventStore inner) : base(inner) { }
		public IEventStore ExposedInner => Inner;
	}

	/// <summary>An inner store providing erasure, implementing both interfaces directly.</summary>
	private sealed class ErasureCapableEventStore : IEventStore, IEventStoreErasure
	{
		public int EraseCalls { get; private set; }

		public object? GetService(Type serviceType) =>
			serviceType == typeof(IEventStoreErasure) ? this : null;

		public Task<int> EraseEventsAsync(
			string aggregateId, string aggregateType, Guid erasureRequestId, CancellationToken cancellationToken)
		{
			EraseCalls++;
			return Task.FromResult(1);
		}

		public Task<bool> IsErasedAsync(string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			Task.FromResult(true);

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			new(Array.Empty<StoredEvent>());

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
			new(Array.Empty<StoredEvent>());

		public ValueTask<AppendResult> AppendAsync(
			string aggregateId, string aggregateType, IEnumerable<IDomainEvent> events, long expectedVersion,
			CancellationToken cancellationToken) =>
			new(AppendResult.CreateSuccess(1, 0));
	}

	/// <summary>A subclass that performs erasure itself and claims the capability accordingly.</summary>
	private sealed class SelfErasingDelegatingEventStore(IEventStore inner)
		: DelegatingEventStore(inner), IEventStoreErasure
	{
		public override object? GetService(Type serviceType) =>
			serviceType == typeof(IEventStoreErasure) ? this : base.GetService(serviceType);

		public override Task<int> EraseEventsAsync(
			string aggregateId, string aggregateType, Guid erasureRequestId, CancellationToken cancellationToken) =>
			Task.FromResult(1);

		public override Task<bool> IsErasedAsync(
			string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			Task.FromResult(true);
	}

	/// <summary>
	/// An inner store with NO erasure, implementing IEventStore directly and returning null from the
	/// probe. A FakeItEasy fake cannot stand in here: its GetService manufactures a dummy rather than
	/// returning null, so "the chain provides no erasure" is not expressible through it.
	/// </summary>
	private sealed class PlainEventStore : IEventStore
	{
		public object? GetService(Type serviceType) => null;

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			new(Array.Empty<StoredEvent>());

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
			string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
			new(Array.Empty<StoredEvent>());

		public ValueTask<AppendResult> AppendAsync(
			string aggregateId, string aggregateType, IEnumerable<IDomainEvent> events, long expectedVersion,
			CancellationToken cancellationToken) =>
			new(AppendResult.CreateSuccess(1, 0));
	}
}
