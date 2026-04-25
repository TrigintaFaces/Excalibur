// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Tests.Persistence;

/// <summary>
/// Regression guard for bd-x6rg45: Persistence prerequisite validator must fail loud at host
/// start when the consumer calls <c>AddPersistence(...)</c> but omits a concrete
/// <see cref="IPersistenceProvider"/>, and must pass cleanly when a provider is present.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class PersistencePrerequisiteValidatorShould
{
	[Fact]
	public async Task Throw_WhenIPersistenceProviderIsMissing()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddPersistence();

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<PersistencePrerequisiteValidator>()
			.SingleOrDefault();
		validator.ShouldNotBeNull(
			"AddPersistence() must register PersistencePrerequisiteValidator as an IHostedService.");

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator!.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("IPersistenceProvider", Case.Sensitive);
	}

	[Fact]
	public async Task Succeed_WhenKeyedIPersistenceProviderIsRegistered()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddPersistence();
		services.AddKeyedSingleton<IPersistenceProvider>("default", (_, _) => A.Fake<IPersistenceProvider>());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<PersistencePrerequisiteValidator>()
			.Single();

		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ResolveNonKeyedIPersistenceProvider_WhenKeyedDefaultIsRegistered()
	{
		// The non-keyed IPersistenceProvider convenience alias forwards to keyed "default",
		// so consumers can inject IPersistenceProvider directly without [FromKeyedServices].
		var fakeProvider = A.Fake<IPersistenceProvider>();
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddPersistence();
		services.AddKeyedSingleton<IPersistenceProvider>("default", (_, _) => fakeProvider);

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var resolved = provider.GetService<IPersistenceProvider>();

		resolved.ShouldNotBeNull("Non-keyed IPersistenceProvider alias should forward to keyed 'default'.");
		resolved.ShouldBeSameAs(fakeProvider);
	}

	[Fact]
	public void Register_Once_EvenWhenAddPersistenceCalledTwice()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		_ = services.AddPersistence();
		_ = services.AddPersistence();

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var count = provider.GetServices<IHostedService>()
			.OfType<PersistencePrerequisiteValidator>()
			.Count();

		count.ShouldBe(1);
	}
}
