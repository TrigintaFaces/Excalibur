// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.CloudNative;

/// <summary>
/// Provides an <b>atomic claim</b> over a cloud-native outbox store, for deployments that poll the store
/// from more than one process instead of relying on the provider's change-feed trigger.
/// Implementations should implement this alongside <see cref="ICloudNativeOutboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a separate capability, and not a change to <see cref="ICloudNativeOutboxStore.GetPendingAsync"/>.</b>
/// That method is a read: it returns pending messages and modifies nothing, and callers depend on being able
/// to inspect a partition without consuming it. A claim is a write. The two are kept apart so that reading is
/// never accidentally destructive, and so that a store which cannot claim simply does not offer the capability
/// rather than offering a claim that is really a read.
/// </para>
/// <para>
/// <b>The property.</b> Two concurrent claimants never claim the same message: each call returns a set
/// <b>disjoint</b> from every set returned by a concurrent call. This is established by the provider's own
/// atomic primitive — a conditional write whose condition names the current lease — and never by
/// read-then-write, which cannot exclude a competitor that read the same state.
/// </para>
/// <para>
/// <b>The lease.</b> A claim stamps an owner and a claim instant on the message. A claimed message is not
/// claimable again until the lease expires, at which point any claimant may take it. Expiry is what makes a
/// claimant that dies mid-delivery release its messages rather than stranding them: nothing has to notice the
/// death, the lease simply ages out. The window is the store's configured lease timeout, and the model is the
/// one the relational providers use — a claim instant plus an owner, with the timeout supplied by
/// configuration rather than stored per message.
/// </para>
/// <para>
/// <b>Consumer obligation — the lease timeout bounds duplicates, so set it above your delivery duration.</b>
/// A claimant that is still delivering when its lease expires can have the message taken from under it and
/// delivered again. Handlers must be idempotent regardless; the timeout is what keeps the duplicate window
/// closed to normal operation rather than opening it on every slow send.
/// </para>
/// <para>
/// <b>Clocks.</b> Lease eligibility on these providers is decided against the claimant's clock, because
/// none of the three primitives involved exposes a server-side clock to a conditional write. A claimant whose
/// clock runs fast can therefore treat a live lease as expired. Keep hosts on NTP and treat the maximum skew
/// between any two claimants as part of the duplicate window, in addition to the lease timeout.
/// </para>
/// </remarks>
public interface ICloudNativeOutboxStoreClaim
{
	/// <summary>
	/// Atomically claims up to <paramref name="batchSize"/> unpublished messages from a partition, stamping
	/// each with a lease held by <paramref name="claimantId"/>.
	/// </summary>
	/// <param name="partitionKey">The partition to claim from.</param>
	/// <param name="batchSize">The maximum number of messages to claim.</param>
	/// <param name="claimantId">
	/// Identifies the claiming process. Recorded on each claimed message as the lease owner, so an operator
	/// can see which process holds a message and a stalled claimant can be attributed.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The messages this call won, each carrying the lease it was stamped with. Losing a race is the normal
	/// outcome under concurrency and is not an error: the contested message is simply absent from the result.
	/// An empty result therefore means "nothing was available to me", never "something went wrong".
	/// A call SHOULD return the messages it wins in creation order where the provider can support it — the
	/// same FIFO guarantee <see cref="ICloudNativeOutboxStore.GetPendingAsync"/> documents, including the
	/// same latency (never loss) caveat for a provider that must order candidates via a separately-maintained,
	/// eventually-consistent index rather than a strongly-consistent query.
	/// </returns>
	Task<CloudQueryResult<CloudOutboxMessage>> ClaimPendingAsync(
		IPartitionKey partitionKey,
		int batchSize,
		string claimantId,
		CancellationToken cancellationToken);
}
