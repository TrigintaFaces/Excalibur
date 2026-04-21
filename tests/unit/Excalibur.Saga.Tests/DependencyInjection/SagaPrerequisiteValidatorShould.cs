// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Regression guard for bd-x6rg45: Saga prerequisite validator must fail loud at host
/// start when the consumer calls <c>AddSagas(...)</c> but omits a concrete
/// <see cref="ISagaStateStore"/> provider, and must pass cleanly when a provider is present.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaPrerequisiteValidatorShould
{
	[Fact]
	public async Task Throw_WhenISagaStateStoreIsMissing()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddSagas());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<SagaPrerequisiteValidator>()
			.SingleOrDefault();
		validator.ShouldNotBeNull(
			"AddSagas(...) must register SagaPrerequisiteValidator as an IHostedService.");

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator!.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("ISagaStateStore", Case.Sensitive);
		ex.Message.ShouldContain("AddSagas", Case.Sensitive);
	}

	[Fact]
	public async Task Succeed_WhenISagaStateStoreIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddSagas());
		services.AddSingleton(A.Fake<ISagaStateStore>());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<SagaPrerequisiteValidator>()
			.Single();

		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public void Register_Once_EvenWhenAddSagasCalledTwice()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddSagas());
		_ = services.AddExcalibur(x => x.AddSagas());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var count = provider.GetServices<IHostedService>()
			.OfType<SagaPrerequisiteValidator>()
			.Count();

		count.ShouldBe(1);
	}
}
