// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;

using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Proves that the tenant-isolation arms in <see cref="InboxStoreConformanceTestKit"/> actually BIND --
/// that each goes RED against a store carrying the defect it names, and GREEN against one that does not.
/// </summary>
/// <remarks>
/// <para>
/// The inbox is the one store where the tenant is part of a message's IDENTITY rather than merely carried
/// alongside it. Providers compose it into a document id, a sort key, a key prefix, or a primary-key
/// component. Until these arms existed the kit asserted none of that, so the requirement was imposed on
/// consumers and enforced against nobody -- including us.
/// </para>
/// <para>
/// The defect being detected fails on the SUCCESS path. A store keyed on (messageId, handlerType) alone
/// treats a second tenant's distinct message as a duplicate and drops it, reporting success. Nothing
/// errors, nothing retries, and the message is simply never processed.
/// </para>
/// <para>
/// The fakes implement <see cref="IInboxStore"/> DIRECTLY, inheriting no first-party base, so the arms bind
/// the interface's own requirement rather than re-testing an inherited convenience.
/// </para>
/// <para>
/// The verdict matrix each test below pins, one cell at a time:
/// </para>
/// <code>
///                   DedupClaim   IsProcSafety   IsProcLiveness   EntryCollide
/// Partitioned       GREEN        GREEN          GREEN            GREEN
/// Blind             RED          RED            green            RED
/// AnswersNothing    green        green          RED              RED
/// </code>
/// <para>
/// The lower-case greens are load-bearing. A blind store passes the liveness arm -- it answers everyone,
/// including the right caller. A store whose reads resolve nothing passes both safety cells perfectly.
/// Neither half detects the other's defect, which is why both are mandatory.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxStoreTenantArmsBindShould
{
	#region Deduplication claim

	/// <summary>
	/// DETECTION: the claim arm must FAIL against a store whose deduplication key omits the tenant.
	/// </summary>
	/// <remarks>
	/// This is the message-loss cell. The second tenant's claim is refused, so its message is treated as
	/// already handled and never runs.
	/// </remarks>
	[Fact]
	public async Task Red_DedupClaim_WhenTheStoreIsKeyedWithoutTheTenant()
	{
		var probe = new ArmProbe(FakeMode.Blind);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunDedupClaimArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"Cross-tenant deduplication collision",
			Case.Sensitive,
			"the arm must fail with the collision diagnostic, not some incidental error");
	}

	/// <summary>LIVENESS: the same arm must PASS against a store that partitions by tenant.</summary>
	[Fact]
	public Task Green_DedupClaim_WhenTheStoreIsPartitionedByTenant() =>
		new ArmProbe(FakeMode.Partitioned).RunDedupClaimArmAsync();

	/// <summary>
	/// Pins the claim arm's blind spot: it exercises only the write path, so a store whose READS resolve
	/// nothing satisfies it. That is why the read-path arms below cannot be dropped.
	/// </summary>
	[Fact]
	public Task Green_DedupClaim_EvenWhenEveryReadResolvesNothing() =>
		new ArmProbe(FakeMode.AnswersNothing).RunDedupClaimArmAsync();

	#endregion

	#region Processed-check confinement

	/// <summary>
	/// SAFETY-DETECTION: the processed-check arm must FAIL against a store keyed without the tenant.
	/// </summary>
	[Fact]
	public async Task Red_IsProcessedSafety_WhenTheStoreIsKeyedWithoutTheTenant()
	{
		var probe = new ArmProbe(FakeMode.Blind);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunIsProcessedArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("Tenant isolation violated", Case.Sensitive);
	}

	/// <summary>
	/// LIVENESS-DETECTION: the same arm must FAIL against a store that reports nothing as processed.
	/// </summary>
	/// <remarks>
	/// Isolating by answering nobody passes every safety assertion ever written. Here the consequence is
	/// that every message is re-processed forever, with no error raised anywhere -- so the safety half
	/// alone would certify an unusable store.
	/// </remarks>
	[Fact]
	public async Task Red_IsProcessedLiveness_WhenTheStoreReportsNothingProcessed()
	{
		var probe = new ArmProbe(FakeMode.AnswersNothing);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunIsProcessedArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("is not told so on a subsequent check", Case.Sensitive);
	}

	/// <summary>LIVENESS: the same arm must PASS against a store that partitions by tenant.</summary>
	[Fact]
	public Task Green_IsProcessed_WhenTheStoreIsPartitionedByTenant() =>
		new ArmProbe(FakeMode.Partitioned).RunIsProcessedArmAsync();

	#endregion

	#region Entry identity

	/// <summary>
	/// DETECTION: the entry arm must FAIL against a store whose entry key omits the tenant.
	/// </summary>
	/// <remarks>
	/// A distinct code path from the claim: providers compose the tenant where the entry is WRITTEN, and a
	/// store may scope the claim and not the entry.
	/// </remarks>
	[Fact]
	public async Task Red_EntryCollide_WhenTheEntryKeyOmitsTheTenant()
	{
		var probe = new ArmProbe(FakeMode.Blind);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunEntryArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("Cross-tenant entry collision", Case.Sensitive);
	}

	/// <summary>
	/// LIVENESS-DETECTION: the same arm must FAIL against a store whose reads resolve nothing.
	/// </summary>
	[Fact]
	public async Task Red_EntryCollide_WhenTheStoreCannotReadItsOwnEntryBack()
	{
		var probe = new ArmProbe(FakeMode.AnswersNothing);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunEntryArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("cannot read it back", Case.Sensitive);
	}

	/// <summary>LIVENESS: the same arm must PASS against a store that partitions by tenant.</summary>
	[Fact]
	public Task Green_EntryCollide_WhenTheStoreIsPartitionedByTenant() =>
		new ArmProbe(FakeMode.Partitioned).RunEntryArmAsync();

	#endregion

	#region Cross-contamination guard

	/// <summary>
	/// Pins that a store which partitions the CLAIM but not the ENTRY is caught. Scoping one path and not
	/// the other is the realistic partial implementation, and a suite exercising a single path cannot see
	/// it.
	/// </summary>
	[Fact]
	public async Task Red_EntryCollide_WhenOnlyTheClaimPathIsPartitioned()
	{
		var probe = new ArmProbe(FakeMode.ClaimPartitionedEntryBlind);

		// The claim arm passes -- that path IS partitioned.
		await probe.RunDedupClaimArmAsync().ConfigureAwait(false);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunEntryArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("Cross-tenant entry collision", Case.Sensitive);
	}

	#endregion

	#region Harness

	/// <summary>The single decision each fake varies.</summary>
	private enum FakeMode
	{
		/// <summary>Tenant is part of both the claim key and the entry key. The conformant shape.</summary>
		Partitioned,

		/// <summary>Keyed on (messageId, handlerType) alone -- claims AND entries.</summary>
		Blind,

		/// <summary>Partitioned writes, but every read resolves nothing.</summary>
		AnswersNothing,

		/// <summary>The claim path carries the tenant; the entry path does not.</summary>
		ClaimPartitionedEntryBlind,
	}

	/// <summary>
	/// Drives the real kit arms against a supplied fake. Subclassing is the only way in: calling the arms
	/// THROUGH the kit is the point -- a reimplemented copy would prove things about the copy.
	/// </summary>
	private sealed class ArmProbe(FakeMode mode) : InboxStoreConformanceTestKit
	{
		// ONE backing set shared by every store this probe hands out, so that even a kit which called
		// CreateStore more than once could not satisfy an isolation arm by instance separation.
		private readonly ConcurrentDictionary<string, InboxEntry> _entries = new(StringComparer.Ordinal);
		private readonly ConcurrentDictionary<string, bool> _claims = new(StringComparer.Ordinal);

		// The fake resolves its tenant through the SHIPPED ambient context, which is the obligation the
		// kit documents for consumers. A fake reading the ambient holder directly would prove the arms
		// work against a mechanism no consumer is told to use.
		protected override IInboxStore CreateStore() =>
			new FakeInboxStore(mode, new ConformanceAmbientTenantContext(), _entries, _claims);

		public Task RunDedupClaimArmAsync() =>
			TryMarkAsProcessed_SameMessageIdInAnotherTenant_MustNotBeSwallowedAsADuplicate();

		public Task RunIsProcessedArmAsync() =>
			IsProcessed_MustNotReportAnotherTenantsMessageAsProcessed();

		public Task RunEntryArmAsync() => CreateEntry_SameMessageIdInAnotherTenant_MustNotCollide();
	}

	/// <summary>A minimal inbox store whose partitioning decision is fixed by construction.</summary>
	private sealed class FakeInboxStore(
		FakeMode mode,
		ITenantContext tenantContext,
		ConcurrentDictionary<string, InboxEntry> entries,
		ConcurrentDictionary<string, bool> claims) : IInboxStore
	{
		private string Tenant => tenantContext.TenantId ?? TenantScope.UntenantedSentinel;

		/// <summary>
		/// THE ONE EXPRESSION UNDER EXPERIMENT for the claim path. Under <see cref="FakeMode.Blind"/> the
		/// tenant is absent, so both tenants' distinct messages address one slot and the second is refused.
		/// </summary>
		private string ClaimKey(string messageId, string handlerType) =>
			mode is FakeMode.Blind
				? $"{messageId}|{handlerType}"
				: $"{Tenant}|{messageId}|{handlerType}";

		/// <summary>
		/// The same expression for the entry path, varied independently so a store that scopes one path and
		/// not the other is expressible.
		/// </summary>
		private string EntryKey(string messageId, string handlerType) =>
			mode is FakeMode.Blind or FakeMode.ClaimPartitionedEntryBlind
				? $"{messageId}|{handlerType}"
				: $"{Tenant}|{messageId}|{handlerType}";

		public ValueTask<InboxEntry> CreateEntryAsync(
			string messageId,
			string handlerType,
			string messageType,
			byte[] payload,
			IDictionary<string, object> metadata,
			CancellationToken cancellationToken)
		{
			var entry = new InboxEntry
			{
				MessageId = messageId,
				HandlerType = handlerType,
				MessageType = messageType,
				Payload = payload,
				TenantId = Tenant,
			};

			if (!entries.TryAdd(EntryKey(messageId, handlerType), entry))
			{
				// The duplicate-key refusal a real store raises. Under a blind entry key this is what the
				// second tenant receives for its own, distinct message.
				throw new InvalidOperationException(
					$"An inbox entry already exists for message '{messageId}' and handler '{handlerType}'.");
			}

			return ValueTask.FromResult(entry);
		}

		public ValueTask<bool> TryMarkAsProcessedAsync(
			string messageId,
			string handlerType,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult(claims.TryAdd(ClaimKey(messageId, handlerType), true));

		public ValueTask<bool> IsProcessedAsync(
			string messageId,
			string handlerType,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult(
				mode is not FakeMode.AnswersNothing
				&& claims.ContainsKey(ClaimKey(messageId, handlerType)));

		public ValueTask<InboxEntry?> GetEntryAsync(
			string messageId,
			string handlerType,
			CancellationToken cancellationToken)
		{
			if (mode is FakeMode.AnswersNothing)
			{
				return ValueTask.FromResult<InboxEntry?>(null);
			}

			return ValueTask.FromResult(
				entries.TryGetValue(EntryKey(messageId, handlerType), out var entry) ? entry : null);
		}

		public ValueTask MarkProcessedAsync(
			string messageId,
			string handlerType,
			CancellationToken cancellationToken)
		{
			if (entries.TryGetValue(EntryKey(messageId, handlerType), out var entry))
			{
				entry.MarkProcessed();
			}

			_ = claims.TryAdd(ClaimKey(messageId, handlerType), true);

			return ValueTask.CompletedTask;
		}

		public ValueTask MarkFailedAsync(
			string messageId,
			string handlerType,
			string errorMessage,
			CancellationToken cancellationToken)
		{
			if (entries.TryGetValue(EntryKey(messageId, handlerType), out var entry))
			{
				entry.MarkFailed(errorMessage);
			}

			return ValueTask.CompletedTask;
		}
	}

	#endregion
}
