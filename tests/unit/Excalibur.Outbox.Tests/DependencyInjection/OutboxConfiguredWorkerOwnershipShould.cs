// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Outbox.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Outbox.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class OutboxConfiguredWorkerOwnershipShould
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task ValidateConfiguredWorkerOwnershipWithoutActivatingAStagingOnlyHost(bool worker, bool election)
	{
		var store = A.Fake<IOutboxStore>();
		A.CallTo(() => store.GetService(A<Type>._)).Returns(null);
		await using var services = Provider(store, worker, election);
		var validator = new OutboxPrerequisiteValidator(services);
		if (worker)
		{
			var error = await Should.ThrowAsync<InvalidOperationException>(() => validator.StartAsync(CancellationToken.None));
			error.Message.ShouldContain("owned worker connection");
		}
		else
		{
			await validator.StartAsync(CancellationToken.None);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StartALegitimatePublisherWorkerWhenItsSelectedConfiguredCapabilitiesExist(bool election)
	{
		var store = A.Fake<IOutboxStore>(options => options
			.Implements<IClaimScopedOutboxStore>().Implements<IFencedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>().Implements<IFencedDeadLetterableOutboxStore>());
		A.CallTo(() => store.GetService(A<Type>._))
			.ReturnsLazily((Type type) => type.IsInstanceOfType(store) ? store : null);
		await using var services = Provider(store, worker: true, election);
		await new OutboxPrerequisiteValidator(services).StartAsync(CancellationToken.None);
	}

	private static ServiceProvider Provider(IOutboxStore store, bool worker, bool election)
	{
		var services = new ServiceCollection();
		services.AddSingleton(store);
		services.Configure<OutboxDeliveryOptions>(_ => { });
		if (worker) { services.AddSingleton<OutboxDrainMarker>(); }
		if (election)
		{
			services.AddSingleton(A.Fake<ILeaderElection>());
			services.AddSingleton(A.Fake<ILeaderProcessingGate>());
		}
		return services.BuildServiceProvider(validateScopes: true);
	}
}
