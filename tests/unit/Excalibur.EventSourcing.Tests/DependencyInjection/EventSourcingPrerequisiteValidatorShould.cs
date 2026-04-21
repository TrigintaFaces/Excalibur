// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Regression guard for bd-x6rg45: EventSourcing prerequisite validator must fail loud
/// at host start when the consumer calls <c>AddEventSourcing(...)</c> but omits a
/// concrete <see cref="IEventStore"/> provider, and must pass cleanly when a provider
/// is present.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcingPrerequisiteValidatorShould
{
	[Fact]
	public async Task Throw_WhenIEventStoreIsMissing()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<EventSourcingPrerequisiteValidator>()
			.SingleOrDefault();
		validator.ShouldNotBeNull(
			"AddEventSourcing(...) must register EventSourcingPrerequisiteValidator as an IHostedService.");

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator!.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("IEventStore", Case.Sensitive);
		ex.Message.ShouldContain("AddEventSourcing", Case.Sensitive);
	}

	[Fact]
	public async Task Succeed_WhenIEventStoreIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing());
		services.AddSingleton(A.Fake<IEventStore>());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<EventSourcingPrerequisiteValidator>()
			.Single();

		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public void Register_Once_EvenWhenAddEventSourcingCalledTwice()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing());
		_ = services.AddExcalibur(x => x.AddEventSourcing());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var count = provider.GetServices<IHostedService>()
			.OfType<EventSourcingPrerequisiteValidator>()
			.Count();

		count.ShouldBe(1);
	}
}
