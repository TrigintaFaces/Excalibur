// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.Inbox;

/// <summary>
/// Regression guard for bd-x6rg45: Inbox prerequisite validator must fail loud at host
/// start when the consumer calls <c>AddInbox(...)</c> but omits a concrete
/// <see cref="IInboxStore"/> provider, and must pass cleanly when a provider is present.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class InboxPrerequisiteValidatorShould
{
	[Fact]
	public async Task Throw_WhenIInboxStoreIsMissing()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(_ => { });

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<InboxPrerequisiteValidator>()
			.SingleOrDefault();
		validator.ShouldNotBeNull(
			"AddExcaliburInbox(...) must register InboxPrerequisiteValidator as an IHostedService.");

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => validator!.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("IInboxStore", Case.Sensitive);
		ex.Message.ShouldContain("AddInbox", Case.Sensitive);
	}

	[Fact]
	public async Task Succeed_WhenKeyedIInboxStoreIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(_ => { });
		services.AddKeyedSingleton<IInboxStore>("default", (_, _) => A.Fake<IInboxStore>());

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var validator = provider.GetServices<IHostedService>()
			.OfType<InboxPrerequisiteValidator>()
			.Single();

		await validator.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ResolveNonKeyedIInboxStore_WhenKeyedDefaultIsRegistered()
	{
		// The non-keyed IInboxStore convenience alias forwards to keyed "default",
		// so consumers can inject IInboxStore directly without [FromKeyedServices].
		var fakeStore = A.Fake<IInboxStore>();
		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(_ => { });
		services.AddKeyedSingleton<IInboxStore>("default", (_, _) => fakeStore);

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var resolved = provider.GetService<IInboxStore>();

		resolved.ShouldNotBeNull("Non-keyed IInboxStore alias should forward to keyed 'default'.");
		resolved.ShouldBeSameAs(fakeStore);
	}

	[Fact]
	public void Register_Once_EvenWhenAddInboxCalledTwice()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(_ => { });
		_ = services.AddExcaliburInbox(_ => { });

		using var provider = services.BuildServiceProvider(validateScopes: false);
		var count = provider.GetServices<IHostedService>()
			.OfType<InboxPrerequisiteValidator>()
			.Count();

		count.ShouldBe(1);
	}
}