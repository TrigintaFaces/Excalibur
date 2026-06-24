// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Engage-test (bd-pux4gk AC-5, S842 ADR-336 Wave 2 / clause-2 structural guard) for the idempotency
/// presence-guard: when a persistent <see cref="IInboxStore"/> that does NOT implement
/// <see cref="IClaimableInboxStore"/> is registered, startup validation (<c>ValidateOnStart</c>) fails fast with an
/// actionable message — rather than silently degrading to the non-atomic check-then-act under which concurrent
/// duplicates can both execute the handler. The silent-race configuration is made inexpressible.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class IdempotencyPresenceGuardShould
{
	[Fact]
	public void Fail_fast_when_the_registered_inbox_store_cannot_claim_atomically()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		// A persistent inbox store that lacks the atomic-claim capability (no IClaimableInboxStore).
		_ = services.AddInbox<NonClaimableInboxStore>();

		using var provider = services.BuildServiceProvider();

		// ValidateOnStart validators run on first options access; the presence-guard must reject the config.
		var ex = Should.Throw<OptionsValidationException>(
			() => _ = provider.GetRequiredService<IOptions<InboxOptions>>().Value);

		// Names the missing atomic-claim capability so the failure is actionable.
		ex.Message.ShouldContain(nameof(IClaimableInboxStore));
	}

	[Fact]
	public void Start_cleanly_when_the_registered_inbox_store_claims_atomically()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		// A persistent inbox store that DOES support atomic claiming.
		_ = services.AddInbox<ClaimableInboxStore>();

		using var provider = services.BuildServiceProvider();

		// No throw: the claim-capable store satisfies the presence-guard.
		var options = provider.GetRequiredService<IOptions<InboxOptions>>().Value;
		_ = options.ShouldNotBeNull();
	}

	/// <summary>
	/// An inbox store that durably tracks Processing (passes the processing guard) but has NO atomic-claim
	/// capability — so it fails ONLY the idempotency claim presence-guard. Members are never invoked by the validator.
	/// </summary>
	private class NonClaimableInboxStore : IInboxStore, IProcessingTrackingInboxStore
	{
		public ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken) => throw new NotSupportedException();

		public ValueTask<InboxEntry> CreateEntryAsync(string messageId, string handlerType, string messageType, byte[] payload, IDictionary<string, object> metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

		public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) => throw new NotSupportedException();

		public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) => throw new NotSupportedException();

		public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) => throw new NotSupportedException();

		public ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken) => throw new NotSupportedException();

		public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken) => throw new NotSupportedException();
	}

	/// <summary>A claim-capable inbox store (satisfies the presence-guard). Members are never invoked here.</summary>
	private sealed class ClaimableInboxStore : NonClaimableInboxStore, IClaimableInboxStore
	{
		public ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken) => throw new NotSupportedException();

		public ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken) => throw new NotSupportedException();
	}
}
