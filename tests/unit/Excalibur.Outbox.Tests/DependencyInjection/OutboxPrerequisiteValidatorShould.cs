// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Outbox.Tests.DependencyInjection;

/// <summary>
/// Regression guard for bd-x6rg45: Outbox prerequisite validator must fail loud at host
/// start when the consumer calls <c>AddOutbox(...)</c> but omits a concrete
/// <see cref="IOutboxStore"/> provider, and must pass cleanly when a provider is present.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxPrerequisiteValidatorShould
{
	[Fact]
	public async Task Throw_WhenIOutboxStoreIsMissing()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.SingleOrDefault();
		validator.ShouldNotBeNull(
			"AddOutbox(...) must register OutboxPrerequisiteValidator as an IHostedService.");

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator!.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("IOutboxStore", Case.Sensitive);
		ex.Message.ShouldContain("AddOutbox", Case.Sensitive);
	}

	[Fact]
	public async Task Succeed_WhenKeyedIOutboxStoreIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));
		services.AddKeyedSingleton<IOutboxStore>("default", (_, _) => A.Fake<IOutboxStore>());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.Single();

		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ResolveNonKeyedIOutboxStore_WhenKeyedDefaultIsRegistered()
	{
		// The non-keyed IOutboxStore convenience alias forwards to keyed "default",
		// so consumers can inject IOutboxStore directly without [FromKeyedServices].
		var fakeStore = A.Fake<IOutboxStore>();
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));
		services.AddKeyedSingleton<IOutboxStore>("default", (_, _) => fakeStore);

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var resolved = provider.GetService<IOutboxStore>();

		resolved.ShouldNotBeNull("Non-keyed IOutboxStore alias should forward to keyed 'default'.");
		resolved.ShouldBeSameAs(fakeStore);
	}

	[Fact]
	public async Task ResolveNonKeyedIOutboxStoreAdmin_WhenKeyedDefaultIsRegistered()
	{
		// When a provider (e.g. Elasticsearch) registers IOutboxStoreAdmin as keyed "default",
		// the non-keyed alias should forward to it.
		var fakeAdmin = A.Fake<IOutboxStoreAdmin>();
		var fakeStore = A.Fake<IOutboxStore>();
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));
		services.AddKeyedSingleton<IOutboxStore>("default", (_, _) => fakeStore);
		services.AddKeyedSingleton<IOutboxStoreAdmin>("default", (_, _) => fakeAdmin);

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var resolved = provider.GetService<IOutboxStoreAdmin>();

		resolved.ShouldNotBeNull("Non-keyed IOutboxStoreAdmin alias should forward to keyed 'default'.");
		resolved.ShouldBeSameAs(fakeAdmin);
	}

	[Fact]
	public async Task ResolveNonKeyedIOutboxStoreAdmin_ViaCastFromIOutboxStore()
	{
		// When no separate IOutboxStoreAdmin keyed registration exists (most providers),
		// the forwarding falls back to casting from the keyed IOutboxStore.
		var fakeStore = A.Fake<IOutboxStore>(x => x.Implements<IOutboxStoreAdmin>());
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));
		services.AddKeyedSingleton<IOutboxStore>("default", (_, _) => fakeStore);

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var resolved = provider.GetService<IOutboxStoreAdmin>();

		resolved.ShouldNotBeNull("Non-keyed IOutboxStoreAdmin should fall back to casting from IOutboxStore.");
		resolved.ShouldBeSameAs(fakeStore);
	}

	[Fact]
	public void Register_Once_EvenWhenAddOutboxCalledTwice()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var count = provider.GetServices<IHostedService>()
			.OfType<OutboxPrerequisiteValidator>()
			.Count();

		count.ShouldBe(1);
	}
}
