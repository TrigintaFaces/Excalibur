// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// scyy8a — the <c>AddExcaliburInbox(...)</c> hosted-service prerequisite validator must FAIL FAST at host
/// start when the registered default <see cref="IInboxStore"/> cannot atomically CLAIM. Without the gate a
/// non-claimable store silently degrades the exactly-once concurrent-duplicate guard to a racy
/// check-then-act (both duplicates execute the handler) and only surfaces a <c>NotSupportedException</c> at
/// first dedup — a latent correctness hole. The gate probes the EFFECTIVE capability
/// (<see cref="IInboxStoreCapabilities.SupportsClaim"/>, fallback <see cref="IClaimableInboxStore"/>) so a
/// decorator that statically declares claimability over a non-claimable inner is still rejected — mirroring
/// the delivery-path validator so both entrypoints fail identically.
/// </summary>
/// <remarks>
/// Every SAFETY arm (a bad config is rejected) is paired with the LIVENESS arm (a valid claimable config still
/// starts): a safety-only gate is satisfied by a validator that rejects everything.
/// Tested through the PUBLIC surface — <c>AddExcaliburInbox</c> registers the validator as an
/// <see cref="IHostedService"/>, and starting the hosted services runs the startup probe (no internal access).
/// </remarks>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class InboxPrerequisiteValidatorShould
{
	private static async Task StartHostedServicesAsync(IServiceProvider provider)
	{
		foreach (var hostedService in provider.GetServices<IHostedService>())
		{
			await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY — a default store that implements only <see cref="IInboxStore"/> (no atomic-claim capability)
	/// must fail host start. RED on pre-fix: the existence-only validator did not throw for a present-but-
	/// non-claimable store.
	/// </summary>
	[Fact]
	public async Task Throw_AtHostStart_WhenDefaultStoreCannotClaim()
	{
		var nonClaimable = A.Fake<IInboxStore>();

		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(_ => { });
		_ = services.AddKeyedSingleton<IInboxStore>("default", nonClaimable);

		await using var provider = services.BuildServiceProvider();

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => StartHostedServicesAsync(provider)).ConfigureAwait(false);
		exception.Message.ShouldContain("atomic");
	}

	/// <summary>
	/// SAFETY (decorator-aware) — a store that statically declares <see cref="IClaimableInboxStore"/> but whose
	/// EFFECTIVE capability (<see cref="IInboxStoreCapabilities.SupportsClaim"/>) is <see langword="false"/>
	/// must STILL fail — the exact hole a lying decorator over a non-claimable inner would open. RED on pre-fix
	/// (no capability probe at all) and RED against a naive <c>is IClaimableInboxStore</c> check.
	/// </summary>
	[Fact]
	public async Task Throw_AtHostStart_WhenDecoratorDeclaresClaimable_ButEffectiveCapabilityIsFalse()
	{
		var lyingDecorator = A.Fake<IInboxStore>(options => options
			.Implements<IClaimableInboxStore>()
			.Implements<IInboxStoreCapabilities>());
		A.CallTo(() => ((IInboxStoreCapabilities)lyingDecorator).SupportsClaim).Returns(false);

		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(_ => { });
		_ = services.AddKeyedSingleton<IInboxStore>("default", lyingDecorator);

		await using var provider = services.BuildServiceProvider();

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => StartHostedServicesAsync(provider)).ConfigureAwait(false);
	}

	/// <summary>
	/// LIVENESS (mandatory pair) — a valid claimable store (the in-memory inbox) must start CLEAN: the gate
	/// must not block valid configs. Without this arm, a validator that rejected everything would pass the
	/// safety arms while being useless.
	/// </summary>
	[Fact]
	public async Task StartClean_WhenDefaultStoreSupportsClaim_InMemory()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging(); // a real host registers logging; the in-memory store depends on ILogger<T>
		_ = services.AddExcaliburInbox(inbox => inbox.UseInMemory());

		await using var provider = services.BuildServiceProvider();

		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider)).ConfigureAwait(false);
	}
}
