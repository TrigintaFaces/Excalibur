// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Locks declarative correlation: a saga declares which member of a message carries the business key, and
/// the identifier is derived from it so no publisher has to know the saga's GUID.
/// </summary>
/// <remarks>
/// The registry holds a typed closure over the consumer's own selector, so reading the key is a direct
/// call rather than a lookup by property name — which is what keeps the path working under trimming and
/// Native AOT.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
public sealed class SagaCorrelationRegistryShould
{
	/// <summary>
	/// A declared correlation derives the identifier from the key on the message.
	/// </summary>
	[Fact]
	public void DeriveTheIdentifierFromTheDeclaredKey()
	{
		var registry = Declared();

		registry.TryResolveSagaId(typeof(OrderSaga), new OrderSubmitted("ORDER-7"), out var resolved)
			.ShouldBeTrue("a correlation is declared for this saga and message type");

		resolved.ShouldBe(
			SagaCorrelation.ToSagaId(nameof(OrderSaga), "ORDER-7"),
			"the registry must derive through the same function a publisher would call by hand, or a "
			+ "message routed declaratively and one routed explicitly reach different instances");
	}

	/// <summary>
	/// Every message carrying the same key resolves to the same instance, including across message types.
	/// </summary>
	[Fact]
	public void ResolveTwoDifferentMessageTypesCarryingTheSameKeyToOneInstance()
	{
		var registry = Declared();

		_ = registry.TryResolveSagaId(typeof(OrderSaga), new OrderSubmitted("ORDER-7"), out var fromSubmit);
		_ = registry.TryResolveSagaId(typeof(OrderSaga), new OrderShipped("ORDER-7"), out var fromShip);

		fromShip.ShouldBe(
			fromSubmit,
			"this is the whole purpose: the submit and the ship messages come from different services that "
			+ "share only the order number, and both must reach the saga the first one created");
	}

	/// <summary>
	/// No declaration returns false, so the caller falls back to the identifier on the message. This is the
	/// regression arm for every saga that existed before correlation.
	/// </summary>
	[Fact]
	public void ReportNoCorrelationWhenNoneIsDeclared() =>
		Declared().TryResolveSagaId(typeof(OrderSaga), new UndeclaredMessage("ORDER-7"), out _)
			.ShouldBeFalse(
				"nothing is declared for this message type, and that must be distinguishable from a "
				+ "declaration that failed. A saga written before correlation existed still routes by the "
				+ "identifier its publisher sets, and returning true here would break all of them.");

	/// <summary>
	/// A declaration that yields no key THROWS. It must not fall back.
	/// </summary>
	[Fact]
	public void ThrowWhenADeclaredCorrelationYieldsNoKey()
	{
		var thrown = Should.Throw<InvalidOperationException>(
			() => Declared().TryResolveSagaId(typeof(OrderSaga), new OrderSubmitted(""), out _));

		thrown.Message.ShouldContain(
			"OrderSubmitted",
			Case.Sensitive,
			"the fault must name the message whose key was missing. Falling back here instead would route "
			+ "by whatever identifier the message carried, which is usually a fresh one, and quietly start "
			+ "a second saga for every message -- the exact failure correlation exists to prevent.");
	}

	/// <summary>
	/// Two sagas may correlate on the same message type without colliding.
	/// </summary>
	[Fact]
	public void ScopeDerivedIdentifiersBySaga()
	{
		var registry = new SagaCorrelationRegistry();
		registry.Declare(typeof(OrderSaga), typeof(OrderSubmitted), nameof(OrderSaga), m => ((OrderSubmitted)m).OrderId);
		registry.Declare(typeof(BillingSaga), typeof(OrderSubmitted), nameof(BillingSaga), m => ((OrderSubmitted)m).OrderId);

		_ = registry.TryResolveSagaId(typeof(OrderSaga), new OrderSubmitted("ORDER-7"), out var order);
		_ = registry.TryResolveSagaId(typeof(BillingSaga), new OrderSubmitted("ORDER-7"), out var billing);

		billing.ShouldNotBe(
			order,
			"an order number is an ordinary key for more than one saga to watch. Without per-saga scoping "
			+ "the billing saga's event lands in the shipping saga's instance and both look healthy.");
	}

	/// <summary>
	/// Declaring the same saga and message type twice throws at composition.
	/// </summary>
	[Fact]
	public void RefuseASecondDeclarationForTheSameSagaAndMessage()
	{
		var registry = new SagaCorrelationRegistry();
		registry.Declare(typeof(OrderSaga), typeof(OrderSubmitted), nameof(OrderSaga), m => ((OrderSubmitted)m).OrderId);

		_ = Should.Throw<InvalidOperationException>(() => registry.Declare(
			typeof(OrderSaga), typeof(OrderSubmitted), nameof(OrderSaga), m => "something-else"),
			"two selectors over one message derive two identifiers from it, so messages would split across "
			+ "two instances. Overwriting quietly makes which one wins depend on registration order.");
	}

	/// <summary>
	/// The builder registers one shared registry, so declarations made in separate calls are all visible.
	/// </summary>
	[Fact]
	public void ShareOneRegistryAcrossSeparateDeclarations()
	{
		var services = new ServiceCollection();
		var builder = new TestSagaBuilder(services);

		_ = builder.CorrelateBy<OrderSaga, OrderSubmitted>(m => m.OrderId);
		_ = builder.CorrelateBy<OrderSaga, OrderShipped>(m => m.OrderId);

		var resolved = services.BuildServiceProvider().GetRequiredService<ISagaCorrelationRegistry>();

		resolved.TryResolveSagaId(typeof(OrderSaga), new OrderSubmitted("ORDER-7"), out var a).ShouldBeTrue();
		resolved.TryResolveSagaId(typeof(OrderSaga), new OrderShipped("ORDER-7"), out var b).ShouldBeTrue(
			"the second declaration must land in the same registry as the first. A fresh registry per call "
			+ "would leave only the last one visible to the coordinator, and the others would silently do "
			+ "nothing while reading as configured.");
		b.ShouldBe(a);
	}


	[Fact]
	[Trait("Pattern", "Regression")]
	public void KeepUnkeyedDeclarationsSeparateFromAPrecedingKeyedRegistry()
	{
		var services = new ServiceCollection();
		var keyedRegistry = new SagaCorrelationRegistry();
		services.AddKeyedSingleton<ISagaCorrelationRegistry>("isolated", keyedRegistry);
		var builder = new TestSagaBuilder(services);

		_ = builder.CorrelateBy<OrderSaga, OrderSubmitted>(m => m.OrderId);
		_ = builder.CorrelateBy<OrderSaga, OrderShipped>(m => m.OrderId);

		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISagaCorrelationRegistry>();
		registry.ShouldNotBeSameAs(keyedRegistry);
		registry.TryResolveSagaId(typeof(OrderSaga), new OrderSubmitted("ORDER-7"), out var submitted)
			.ShouldBeTrue();
		registry.TryResolveSagaId(typeof(OrderSaga), new OrderShipped("ORDER-7"), out var shipped)
			.ShouldBeTrue();
		shipped.ShouldBe(submitted);
		provider.GetRequiredKeyedService<ISagaCorrelationRegistry>("isolated").ShouldBeSameAs(keyedRegistry);
		keyedRegistry.TryResolveSagaId(typeof(OrderSaga), new OrderSubmitted("ORDER-7"), out _)
			.ShouldBeFalse("ordinary correlation declarations must not mutate a keyed registry");
		keyedRegistry.TryResolveSagaId(typeof(OrderSaga), new OrderShipped("ORDER-7"), out _)
			.ShouldBeFalse();
	}

	/// <summary>
	/// THE WIRING ARM: the coordinator consults the registry, and a declared correlation OUTRANKS the
	/// identifier the message carries.
	/// </summary>
	/// <returns>The arm.</returns>
	/// <remarks>
	/// <para>
	/// The registry can be perfectly correct and change nothing if the coordinator never asks it, which is
	/// the half-wired failure this project keeps finding. So the event here carries a DIFFERENT, valid
	/// identifier from the derived one: if the coordinator parsed the message's identifier the saga would
	/// be created under that, and this arm fails.
	/// </para>
	/// <para>
	/// It also pins the precedence ruling. Honouring the message's identifier first would let a publisher
	/// that sets one out of habit create a saga per message while the correlation sat there looking
	/// configured.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task BeConsultedByTheCoordinatorAndOutrankTheIdentifierOnTheMessage()
	{
		var services = new ServiceCollection();
		var builder = new TestSagaBuilder(services);
		_ = builder.CorrelateBy<OrderSaga, CorrelatedStart>(m => m.OrderId);
		_ = services.AddSingleton(A.Fake<IDispatcher>());
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);
		var provider = services.BuildServiceProvider();

		var store = new InMemorySagaStore(new TestTenantContext());
		var coordinator = new SagaCoordinator(
			provider, store, MsOptions.Create(new SagaOptions()), NullLogger<SagaCoordinator>.Instance);

		var sagaInfo = new SagaInfo(typeof(OrderSaga), typeof(OrderSagaState)).StartsWith<CorrelatedStart>();

		// A valid but UNRELATED identifier on the message. The declared correlation must win.
		var unrelated = Guid.NewGuid();
		var evt = new CorrelatedStart("ORDER-99") { SagaId = unrelated.ToString() };

		await coordinator.HandleEventInternalAsync<OrderSaga, OrderSagaState>(
			A.Fake<IMessageContext>(), evt, sagaInfo, CancellationToken.None);

		var derived = SagaCorrelation.ToSagaId<OrderSaga>("ORDER-99");

		_ = (await store.LoadAsync<OrderSagaState>(derived, CancellationToken.None)).ShouldNotBeNull(
			"the coordinator must have resolved the identifier through the registry. Nothing under the "
			+ "derived id means it parsed the identifier on the message instead and the declaration did "
			+ "nothing, which is indistinguishable from correlation working until someone counts sagas.");

		(await store.LoadAsync<OrderSagaState>(unrelated, CancellationToken.None)).ShouldBeNull(
			"and nothing may exist under the identifier the message carried, or both precedences are in "
			+ "play and which one applies depends on the order things ran");
	}

    [Fact]
    [Trait("Pattern", "Regression")]
    public async Task KeepSameNameSagasInDifferentNamespacesSeparateEvenWithTheSameStateAndBusinessKey()
    {
        var services = new ServiceCollection(); var builder = new TestSagaBuilder(services);
        builder.CorrelateBy<CorrelationScopeA.OrderSaga, CorrelatedStart>(message => message.OrderId);
        builder.CorrelateBy<CorrelationScopeB.OrderSaga, CorrelatedStart>(message => message.OrderId);
        services.AddSingleton(A.Fake<IDispatcher>()); services.AddSingleton<ILogger>(NullLogger.Instance);
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ISagaCorrelationRegistry>();
        var messageA = new CorrelatedStart("SHARED-KEY"); var messageB = new CorrelatedStart("SHARED-KEY");
        registry.TryResolveSagaId(typeof(CorrelationScopeA.OrderSaga), messageA, out var idA).ShouldBeTrue();
        registry.TryResolveSagaId(typeof(CorrelationScopeB.OrderSaga), messageB, out var idB).ShouldBeTrue();
        idA.ShouldBe(SagaCorrelation.ToSagaId<CorrelationScopeA.OrderSaga>("SHARED-KEY"));
        idB.ShouldBe(SagaCorrelation.ToSagaId<CorrelationScopeB.OrderSaga>("SHARED-KEY"));
        idA.ShouldNotBe(idB, "namespace-distinct saga types cannot share the same persisted state identity");
        var store = new InMemorySagaStore(new TestTenantContext());
        var coordinator = new SagaCoordinator(provider, store, MsOptions.Create(new SagaOptions()), NullLogger<SagaCoordinator>.Instance);
        await coordinator.HandleEventInternalAsync<CorrelationScopeA.OrderSaga, CorrelationSharedState>(A.Fake<IMessageContext>(),
            messageA, new SagaInfo(typeof(CorrelationScopeA.OrderSaga), typeof(CorrelationSharedState)).StartsWith<CorrelatedStart>(), CancellationToken.None);
        await coordinator.HandleEventInternalAsync<CorrelationScopeB.OrderSaga, CorrelationSharedState>(A.Fake<IMessageContext>(),
            messageB, new SagaInfo(typeof(CorrelationScopeB.OrderSaga), typeof(CorrelationSharedState)).StartsWith<CorrelatedStart>(), CancellationToken.None);
        var stateA = (await store.LoadAsync<CorrelationSharedState>(idA, CancellationToken.None)).ShouldNotBeNull();
        var stateB = (await store.LoadAsync<CorrelationSharedState>(idB, CancellationToken.None)).ShouldNotBeNull();
        stateA.Owner.ShouldBe("A"); stateB.Owner.ShouldBe("B"); stateA.SagaId.ShouldNotBe(stateB.SagaId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [Trait("Pattern", "Regression")]
    public void RefuseAnUnsupportedAuthoritativeUnkeyedRegistryWithoutOrphaningDeclarations(int registration)
    {
        var services = new ServiceCollection(); var calls = 0;
        if (registration == 0) { services.AddSingleton<ISagaCorrelationRegistry>(new CustomRegistry()); }
        else if (registration == 1) { services.AddSingleton<ISagaCorrelationRegistry, CustomRegistry>(); }
        else { services.AddSingleton<ISagaCorrelationRegistry>(_ => { calls++; return new CustomRegistry(); }); }
        var descriptor = services.Single();
        var error = Should.Throw<InvalidOperationException>(() =>
            new TestSagaBuilder(services).CorrelateBy<OrderSaga, OrderSubmitted>(message => message.OrderId));
        error.Message.ShouldContain("last unkeyed"); error.Message.ShouldContain("without CorrelateBy");
        calls.ShouldBe(0, "composition must not invoke consumer factories or build a temporary provider");
        services.Count.ShouldBe(1); services.Single().ShouldBeSameAs(descriptor);
    }

    [Fact]
    public void ComposeIntoTheLastUnkeyedFrameworkRegistryThatMicrosoftDiActuallyResolves()
    {
        var first = new SagaCorrelationRegistry(); var last = new SagaCorrelationRegistry();
        var services = new ServiceCollection(); services.AddSingleton<ISagaCorrelationRegistry>(first);
        services.AddSingleton<ISagaCorrelationRegistry>(last);
        new TestSagaBuilder(services).CorrelateBy<OrderSaga, OrderSubmitted>(message => message.OrderId);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ISagaCorrelationRegistry>().ShouldBeSameAs(last);
        last.TryResolveSagaId(typeof(OrderSaga), new OrderSubmitted("KEY"), out var id).ShouldBeTrue();
        id.ShouldBe(SagaCorrelation.ToSagaId<OrderSaga>("KEY"));
        first.TryResolveSagaId(typeof(OrderSaga), new OrderSubmitted("KEY"), out _).ShouldBeFalse();
    }

    [Fact]
    public void FollowTheLastDescriptorRatherThanCastingOrMutatingAnEarlierCustomRegistry()
    {
        var services = new ServiceCollection(); services.AddSingleton<ISagaCorrelationRegistry>(new CustomRegistry());
        var last = new SagaCorrelationRegistry(); services.AddSingleton<ISagaCorrelationRegistry>(last);
        new TestSagaBuilder(services).CorrelateBy<OrderSaga, OrderSubmitted>(message => message.OrderId);
        last.TryResolveSagaId(typeof(OrderSaga), new OrderSubmitted("KEY"), out _).ShouldBeTrue();
        services.AddSingleton<ISagaCorrelationRegistry>(new CustomRegistry());
        Should.Throw<InvalidOperationException>(() => new TestSagaBuilder(services).CorrelateBy<OrderSaga, OrderShipped>(message => message.OrderId));
        last.TryResolveSagaId(typeof(OrderSaga), new OrderShipped("KEY"), out _).ShouldBeFalse();
    }

    [Fact]
    public void PreserveACustomRegistryWhenDeclarativeCorrelationIsNotRequested()
    {
        var custom = new CustomRegistry(); var services = new ServiceCollection(); services.AddSingleton<ISagaCorrelationRegistry>(custom);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ISagaCorrelationRegistry>().ShouldBeSameAs(custom);
    }

    private sealed class CustomRegistry : ISagaCorrelationRegistry
    {
        public bool TryResolveSagaId(Type sagaType, object message, out Guid sagaId) { sagaId = Guid.Empty; return false; }
    }

	private static SagaCorrelationRegistry Declared()
	{
		var registry = new SagaCorrelationRegistry();
		registry.Declare(typeof(OrderSaga), typeof(OrderSubmitted), nameof(OrderSaga), m => ((OrderSubmitted)m).OrderId);
		registry.Declare(typeof(OrderSaga), typeof(OrderShipped), nameof(OrderSaga), m => ((OrderShipped)m).OrderId);
		return registry;
	}

	private sealed record OrderSubmitted(string OrderId);

	private sealed record CorrelatedStart(string OrderId) : ISagaEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
		public string AggregateId { get; init; } = "order";
		public long Version { get; init; } = 1;
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(CorrelatedStart);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	private sealed record OrderShipped(string OrderId);

	private sealed record UndeclaredMessage(string OrderId);

	private sealed class OrderSagaState : SagaState
	{
	}

	private sealed class BillingSaga
	{
	}

	private sealed class OrderSaga(OrderSagaState state, IDispatcher dispatcher, ILogger logger)
		: SagaBase<OrderSagaState>(state, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;

		public override Task<SagaEventOutcome> HandleAsync(
			object eventMessage, CancellationToken cancellationToken) =>
			Task.FromResult(SagaEventOutcome.Handled);
	}

	private sealed class TestSagaBuilder(IServiceCollection services) : ISagaBuilder
	{
		public IServiceCollection Services { get; } = services;
	}
}
