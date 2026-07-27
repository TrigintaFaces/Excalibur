// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Options that compose the outbox, inbox, and deduplication primitives into a single turnkey exactly-once
/// messaging registration (<c>AddExactlyOnceMessaging</c>), with a documented delivery-guarantee boundary.
/// </summary>
/// <remarks>
/// <para>
/// <b>Guarantee boundary.</b> The delivery guarantee depends on the registered inbox store's capabilities:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Exactly-once</b> when the inbox store implements <see cref="ITransactionalInboxStore"/> (it processes the
/// message and marks it processed inside a single enlisting transaction, so a crash can neither drop nor
/// double-apply the effect).
/// </description></item>
/// <item><description>
/// <b>At-least-once with durable deduplication</b> otherwise: the atomic claim protocol
/// (<see cref="IClaimableInboxStore"/>) suppresses concurrent duplicates and the dedup store suppresses
/// redelivered ones for <see cref="DeduplicationTimeToLive"/>, but a crash between handler success and the
/// processed-mark redelivers — so handlers MUST be idempotent.
/// </description></item>
/// </list>
/// <para>
/// Set <see cref="RequireTransactionalExactlyOnce"/> to fail fast at startup unless the inbox store can provide
/// the transactional guarantee, rather than silently running with the weaker (at-least-once) contract.
/// </para>
/// </remarks>
public sealed class ExactlyOnceOptions
{
	/// <summary>
	/// Gets or sets the time-to-live for deduplication entries. When <see langword="null"/> (the default), the
	/// value is tied to the inbox message time-to-live
	/// (<see cref="InboxOptions.DefaultMessageTimeToLive"/>) so a message cannot be re-admitted by the inbox
	/// after its dedup record has expired (which would defeat suppression). Set it explicitly only to override
	/// that coupling.
	/// </summary>
	/// <value>The deduplication retention window, or <see langword="null"/> to track the inbox TTL.</value>
	public TimeSpan? DeduplicationTimeToLive { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether registration requires an inbox store that supports transactional
	/// exactly-once processing (<see cref="ITransactionalInboxStore"/>). When <see langword="true"/>, startup
	/// fails fast if the registered store cannot provide it; when <see langword="false"/> (the default), a
	/// non-transactional store is accepted under the documented at-least-once-with-deduplication boundary.
	/// </summary>
	/// <value><see langword="true"/> to enforce transactional exactly-once at startup; otherwise <see langword="false"/>.</value>
	public bool RequireTransactionalExactlyOnce { get; set; }

	/// <summary>
	/// Validates the options, returning an error message describing the first failure, or <see langword="null"/>
	/// when the options are valid.
	/// </summary>
	/// <param name="options">The options to validate.</param>
	/// <returns>An error message, or <see langword="null"/> if valid.</returns>
	public static string? Validate(ExactlyOnceOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.DeduplicationTimeToLive is { } ttl && ttl <= TimeSpan.Zero)
		{
			return "ExactlyOnceOptions.DeduplicationTimeToLive must be greater than zero when specified.";
		}

		return null;
	}
}
