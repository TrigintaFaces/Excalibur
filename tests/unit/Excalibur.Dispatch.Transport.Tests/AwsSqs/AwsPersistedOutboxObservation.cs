// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

// TEST ONLY: observes acknowledged transitions from the REAL provider. Never
// mutates the staged source object or manufactures a row/status from an intent.
internal sealed class AwsPersistedOutboxObservation : OutboxStoreDecorator,
	IClaimScopedOutboxStore, IClaimScopedDeadLetterableOutboxStore,
	IFencedOutboxStore, IFencedClaimScopedOutboxStore, IFencedDeadLetterableOutboxStore
{
	private readonly ConcurrentDictionary<(string Id, string Claim), ClaimEvidence> _claims = [];
	private readonly ConcurrentDictionary<string, TerminalEvidence> _terminals = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, int> _failures = new(StringComparer.Ordinal);
	private readonly IClaimScopedOutboxStore _claim;
	private readonly IClaimScopedDeadLetterableOutboxStore _dead;
	private readonly IFencedOutboxStore _fenced;
	private readonly IFencedClaimScopedOutboxStore _fencedClaim;
	private readonly IFencedDeadLetterableOutboxStore _fencedDead;

	internal AwsPersistedOutboxObservation(IOutboxStore inner) : base(inner)
	{
		_claim = Require<IClaimScopedOutboxStore>();
		_dead = Require<IClaimScopedDeadLetterableOutboxStore>();
		_fenced = Require<IFencedOutboxStore>();
		_fencedClaim = Require<IFencedClaimScopedOutboxStore>();
		_fencedDead = Require<IFencedDeadLetterableOutboxStore>();
	}

	internal static void Register(IServiceCollection services)
	{
		services.AddSingleton(provider => new AwsPersistedOutboxObservation(provider.GetRequiredService<InMemoryOutboxStore>()));
		services.AddSingleton<IOutboxStore>(provider => provider.GetRequiredService<AwsPersistedOutboxObservation>());
	}

	public override async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
		Capture(await Inner.GetUnsentMessagesAsync(batchSize, cancellationToken), null);
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, long fencingToken, CancellationToken cancellationToken) =>
		Capture(await _fenced.GetUnsentMessagesAsync(batchSize, fencingToken, cancellationToken), fencingToken);
	public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken) => _fenced.MarkSentAsync(messageId, fencingToken, cancellationToken);
	public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount, long fencingToken, CancellationToken cancellationToken) =>
		_fencedDead.MarkDeadLetteredAsync(messageId, reason, attemptCount, fencingToken, cancellationToken);

	public async ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, string claimIdentity, CancellationToken cancellationToken)
	{
		var result = await _claim.MarkSentAsync(messageId, claimIdentity, cancellationToken);
		Observe(result, messageId, claimIdentity, null, sent: true, attemptCount: null, reason: null);
		return result;
	}
	public async ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, OutboxWriteAuthority authority, CancellationToken cancellationToken)
	{
		var result = await _fencedClaim.MarkSentAsync(messageId, authority, cancellationToken);
		Observe(result, messageId, authority.ClaimIdentity, authority.FencingToken, sent: true, attemptCount: null, reason: null);
		return result;
	}
	public async ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount, string claimIdentity, CancellationToken cancellationToken)
	{
		var result = await _dead.MarkDeadLetteredAsync(messageId, reason, attemptCount, claimIdentity, cancellationToken);
		Observe(result, messageId, claimIdentity, null, sent: false, attemptCount, reason);
		return result;
	}
	public async ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(string messageId, string reason, int attemptCount, OutboxWriteAuthority authority, CancellationToken cancellationToken)
	{
		var result = await _fencedDead.MarkDeadLetteredAsync(messageId, reason, attemptCount, authority, cancellationToken);
		Observe(result, messageId, authority.ClaimIdentity, authority.FencingToken, sent: false, attemptCount, reason);
		return result;
	}
	public async ValueTask<OutboxCompletionOutcome> MarkFailedAsync(string messageId, string errorMessage, int retryCount, string claimIdentity, CancellationToken cancellationToken)
	{
		var result = await _claim.MarkFailedAsync(messageId, errorMessage, retryCount, claimIdentity, cancellationToken);
		if (result == OutboxCompletionOutcome.Applied) { _failures[messageId] = retryCount; }
		return result;
	}
	public async ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(string messageId, string errorMessage, int retryCount, DateTimeOffset nextAttemptAt, string claimIdentity, CancellationToken cancellationToken)
	{
		var result = await _claim.MarkFailedWithBackoffAsync(messageId, errorMessage, retryCount, nextAttemptAt, claimIdentity, cancellationToken);
		if (result == OutboxCompletionOutcome.Applied) { _failures[messageId] = retryCount; }
		return result;
	}
	public async ValueTask<OutboxCompletionOutcome> MarkFailedAsync(string messageId, string errorMessage, int retryCount, DateTimeOffset? nextAttemptAt, OutboxWriteAuthority authority, CancellationToken cancellationToken)
	{
		var result = await _fencedClaim.MarkFailedAsync(messageId, errorMessage, retryCount, nextAttemptAt, authority, cancellationToken);
		if (result == OutboxCompletionOutcome.Applied) { _failures[messageId] = retryCount; }
		return result;
	}

	internal static async Task AssertStateAsync(IServiceProvider provider, OutboundMessage input, OutboxStatus expected, string? why = null)
	{
		var observed = provider.GetRequiredService<AwsPersistedOutboxObservation>();
		if (expected == OutboxStatus.Failed)
		{
			(await FailedAsync(provider, input)).Status.ShouldBe(expected, why);
			observed._terminals.ContainsKey(input.Id).ShouldBeFalse();
			return;
		}
		var terminal = observed.Terminal(input.Id);
		terminal.Sent.ShouldBe(expected == OutboxStatus.Sent, why);
		var acquired = observed._claims[(input.Id, terminal.Claim)];
		acquired.Payload.ShouldBe(input.Payload);
		acquired.MessageType.ShouldBe(input.MessageType);
		var stats = await Admin(provider).GetAllTenantsStatisticsAsync(TestContext.Current.CancellationToken);
		stats.StagedMessageCount.ShouldBe(0);
		stats.FailedMessageCount.ShouldBe(0);
		stats.SentMessageCount.ShouldBe(expected == OutboxStatus.Sent ? 1 : 0);
		(await provider.GetRequiredService<IOutboxStore>().GetUnsentMessagesAsync(1, TestContext.Current.CancellationToken))
			.ShouldBeEmpty("the real claimIdentity selector must not re-admit an acknowledged terminal transition");
		var queue = (AwsEventBridgePersistedAcceptanceShould.RetainingQueue)provider.GetRequiredService<global::Excalibur.Dispatch.ErrorHandling.IDeadLetterQueue>();
		if (terminal.Sent) { queue.Entries.ShouldBeEmpty(); }
		else
		{
			var retained = queue.Entries.ShouldHaveSingleItem();
			retained.Payload.ShouldBe(input.Payload);
			retained.OriginalAttempts.ShouldBe(terminal.Count);
			queue.OriginalMessageIds.ShouldBe([input.Id]);
		}
	}
	internal static async Task AssertRetryCountAsync(IServiceProvider provider, OutboundMessage input, int expected, string? why = null)
	{
		var observed = provider.GetRequiredService<AwsPersistedOutboxObservation>();
		if (observed._terminals.TryGetValue(input.Id, out var terminal)) { terminal.Count.ShouldBe(expected, why); return; }
		var failed = await Admin(provider).GetAllTenantsFailedMessagesAsync(0, null, 10, TestContext.Current.CancellationToken);
		var actual = failed.SingleOrDefault(row => row.Id == input.Id);
		if (actual is not null) { actual.RetryCount.ShouldBe(expected, why); return; }
		// Caller cancellation: the actual acquired snapshot was zero and no failure
		// or terminal write was Applied; independent statistics still report staged.
		observed._failures.ContainsKey(input.Id).ShouldBeFalse();
		observed._claims.Where(pair => pair.Key.Id == input.Id).ShouldHaveSingleItem().Value.Count.ShouldBe(expected, why);
		(await Admin(provider).GetAllTenantsStatisticsAsync(TestContext.Current.CancellationToken)).StagedMessageCount.ShouldBe(1);
	}
	internal static async Task AssertNotStateAsync(IServiceProvider provider, OutboundMessage input, OutboxStatus excluded)
	{
		var observed = provider.GetRequiredService<AwsPersistedOutboxObservation>();
		if (observed._terminals.TryGetValue(input.Id, out var terminal))
		{
			terminal.Sent.ShouldNotBe(excluded == OutboxStatus.Sent);
			return;
		}
		var stats = await Admin(provider).GetAllTenantsStatisticsAsync(TestContext.Current.CancellationToken);
		(stats.StagedMessageCount + stats.FailedMessageCount).ShouldBe(1);
	}
	internal static async Task<string?> ErrorAsync(IServiceProvider provider, OutboundMessage input)
	{
		var observed = provider.GetRequiredService<AwsPersistedOutboxObservation>();
		return observed._terminals.TryGetValue(input.Id, out var terminal) ? terminal.Reason : (await FailedAsync(provider, input)).LastError;
	}
	internal bool HasTerminal(string messageId) => _terminals.ContainsKey(messageId);
	internal static IOutboxStoreAdmin Admin(IServiceProvider provider) =>
		(IOutboxStoreAdmin)provider.GetRequiredService<IOutboxStore>().GetService(typeof(IOutboxStoreAdmin))!;
	private static async Task<OutboundMessage> FailedAsync(IServiceProvider provider, OutboundMessage input)
	{
		var row = (await Admin(provider).GetAllTenantsFailedMessagesAsync(0, null, 10, TestContext.Current.CancellationToken)).Single(row => row.Id == input.Id);
		row.Payload.ShouldBe(input.Payload);
		row.MessageType.ShouldBe(input.MessageType);
		return row;
	}
	private TerminalEvidence Terminal(string messageId)
	{
		_terminals.TryGetValue(messageId, out var result).ShouldBeTrue("the real strong claimIdentity completion must acknowledge Applied for the actual acquired authority");
		result.ShouldNotBeNull();
		result.Id.ShouldBe(messageId);
		result.Claim.ShouldNotBeNullOrWhiteSpace();
		_claims.TryGetValue((messageId, result.Claim), out var acquisition).ShouldBeTrue();
		acquisition.ShouldNotBeNull();
		acquisition.Fence.ShouldBe(result.Fence);
		return result;
	}
	private IEnumerable<OutboundMessage> Capture(IEnumerable<OutboundMessage> messages, long? fencingToken)
	{
		var rows = messages.ToArray();
		foreach (var row in rows) { _claims[(row.Id, row.DispatcherId!)] = new ClaimEvidence(row.RetryCount, fencingToken, row.MessageType, row.Payload.ToArray()); }
		return rows;
	}
	private void Observe(OutboxCompletionOutcome result, string messageId, string claimIdentity, long? fencingToken, bool sent, int? attemptCount, string? reason)
	{
		if (result == OutboxCompletionOutcome.Applied && _claims.TryGetValue((messageId, claimIdentity), out var acquired) && acquired.Fence == fencingToken)
		{
			_terminals[messageId] = new TerminalEvidence(messageId, claimIdentity, fencingToken, sent, attemptCount is { } supplied ? Math.Max(acquired.Count, supplied) : acquired.Count, reason);
		}
	}
	private T Require<T>() where T : class => Inner.GetService(typeof(T)) as T ?? throw new InvalidOperationException("The actual fixture store lacks " + typeof(T).Name);
	private sealed record ClaimEvidence(int Count, long? Fence, string MessageType, byte[] Payload);
	private sealed record TerminalEvidence(string Id, string Claim, long? Fence, bool Sent, int Count, string? Reason);
}
