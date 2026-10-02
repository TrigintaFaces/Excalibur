// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance.CryptoShredding;

/// <summary>
/// A destruction ledger for a deployment that performs no erasure: it holds no rows, so it reports every
/// key generation as NOT destroyed.
/// </summary>
/// <remarks>
/// <para>
/// This exists for one real scenario: encrypting personal data at rest — in the event store, the inbox, the
/// outbox — <b>without</b> running the GDPR erasure subsystem. Such a deployment never destroys a key, so no
/// generation is ever destroyed, so "not destroyed" is the true answer for every generation rather than a
/// placeholder. Reads decrypt normally and no tombstone is ever produced.
/// </para>
/// <para>
/// <b>It must be registered deliberately and is never a default.</b> That is the whole point of its
/// existence as a named type. An always-false ledger is <i>safe</i> — it cannot fabricate an erasure, which
/// is the catastrophic direction — and that is precisely why silently defaulting to one would be wrong: a
/// deployment that DOES erase, but whose erasure store was never registered, would get a configuration that
/// never tombstones anything, forever, with nothing to notice. The consumer would read their own erased
/// subjects' data back in the clear and no component would report a problem. So the choice is made in the
/// consumer's own registration code, where it is visible in review, rather than inferred from an absence by
/// this framework.
/// </para>
/// <para>
/// <b>Do not register this alongside an erasure store.</b> The two answer the same question differently, and
/// which one wins would depend on registration order. If this deployment erases, the erasure store supplies
/// the ledger and this type has no part to play.
/// </para>
/// </remarks>
internal sealed class NoErasureKeyDestructionLedger : IKeyDestructionLedger
{
	/// <inheritdoc />
	/// <remarks>
	/// Always <see langword="false" />. Not "unknown", and not an exception: this deployment has destroyed
	/// nothing, so the honest answer about every generation is that its material was not destroyed here.
	/// </remarks>
	public ValueTask<bool> IsGenerationDestroyedAsync(
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);
		cancellationToken.ThrowIfCancellationRequested();

		return ValueTask.FromResult(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Refused, loudly. A caller asserting a destruction has contradicted the registration that chose this
	/// ledger — they are telling the framework that material WAS destroyed, in a deployment declared to
	/// perform no erasure. Accepting it would mean either silently discarding a compliance assertion the
	/// caller owns, or holding a row in a store that nothing persists; the first loses evidence and the
	/// second loses it at the next restart. Both are worse than saying so.
	/// </remarks>
	public Task RecordDestroyedGenerationAsync(
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyHandle);
		ArgumentException.ThrowIfNullOrEmpty(keyGeneration);

		throw new InvalidOperationException(
			"This deployment is registered as performing no erasure, so there is nowhere durable to record a "
			+ "destruction, and a destruction that is not recorded cannot be reported as an erasure. Register "
			+ "an erasure store instead -- AddInMemoryErasureStore(), AddPostgresErasureStore() or "
			+ "AddSqlServerErasureStore() -- which supplies a ledger that persists the row this call is "
			+ "asserting.");
	}
}
