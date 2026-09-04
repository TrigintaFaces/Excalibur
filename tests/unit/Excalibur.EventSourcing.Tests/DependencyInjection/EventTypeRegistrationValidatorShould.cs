// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Regression guard for kk3l17: the event-type-registration startup validator must fail loud at host
/// start on the known-brick triple (event sourcing + the default type-rejecting <c>JsonEventSerializer</c>
/// + an empty/absent event-type allow-list), stay silent for every safe configuration, and the in-builder
/// <c>RegisterEventTypes*</c> helpers must populate the allow-list.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventTypeRegistrationValidatorShould
{
	private static EventTypeRegistrationValidator ResolveValidator(IServiceProvider provider) =>
		provider.GetServices<IHostedService>()
			.OfType<EventTypeRegistrationValidator>()
			.Single();

	[Fact]
	public async Task Throw_WhenDefaultSerializerAndNoEventTypesRegistered()
	{
		// Default JsonEventSerializer (via AddDispatch) + no AddEventTypes/RegisterEventTypes call
		// => the allow-list is absent (null registry) => every replay bricks at runtime.
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing());
		services.AddKeyedSingleton<IEventStore>("default", (_, _) => A.Fake<IEventStore>());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = ResolveValidator(provider);

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("event type", Case.Insensitive);
		ex.Message.ShouldContain("RegisterEventTypesFromAssembly", Case.Sensitive);
	}

	[Fact]
	public async Task Succeed_WhenEventTypesRegisteredViaBuilder()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.RegisterEventTypes<TestEvent>()));
		services.AddKeyedSingleton<IEventStore>("default", (_, _) => A.Fake<IEventStore>());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = ResolveValidator(provider);

		// Non-empty allow-list => no brick => no throw.
		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task Succeed_WhenCustomSerializerRegistered_EvenWithEmptyRegistry()
	{
		// A consumer-supplied (non-default) serializer owns its own type-resolution contract and is exempt,
		// even with an empty allow-list.
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing());
		services.AddKeyedSingleton<IEventStore>("default", (_, _) => A.Fake<IEventStore>());
		services.AddSingleton(A.Fake<IEventSerializer>()); // resolves last => not JsonEventSerializer

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = ResolveValidator(provider);

		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task Succeed_WhenEventTypesRegisteredViaParamsOverload()
	{
		// The params Type[] overload must populate the allow-list too — proven observably: with a
		// non-empty allow-list the brick-guard passes (IEventTypeRegistry is internal, so registration is
		// verified through the guard's behavior, not by inspecting the registry directly).
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
			es.RegisterEventTypes(typeof(TestEvent), typeof(OtherTestEvent))));
		services.AddKeyedSingleton<IEventStore>("default", (_, _) => A.Fake<IEventStore>());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = ResolveValidator(provider);

		await validator.StartAsync(CancellationToken.None);
	}

	[MessageName("Test.Es.ValidatorTestEvent")]
	private sealed record TestEvent;

	[MessageName("Test.Es.ValidatorOtherTestEvent")]
	private sealed record OtherTestEvent;
}
