// Copyright (c) Excalibur contributors. All rights reserved.

namespace Excalibur.Dispatch;

/// <summary>
/// Optional diagnostic and recovery surface for a store's leadership-fencing high-water mark.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IFencedOutboxStore" />'s claim is specified never to throw on a stale token — it returns an
/// empty set instead, so a superseded leader's drain does not crash-loop. That makes an empty claim
/// ambiguous from the outside: it means either "no work is due" or "your fencing token is stale", and
/// nothing about the claim itself distinguishes the two. This interface exists to remove that ambiguity
/// and to give an operator a way to recover from a poisoned fence — most commonly a fencing-token
/// generator whose backing store was re-created (a namespace teardown, a deleted coordination lease),
/// which restarts token issuance below the outbox's already-recorded high-water and leaves the outbox
/// silently undrainable, with no error anywhere, forever.
/// </para>
/// <para>
/// Discovered the same way every other optional outbox capability is: <c>store.GetService(typeof(IFencedOutboxStoreDiagnostics))</c>,
/// never a cast — a decorated store answers through its own capability seam.
/// </para>
/// </remarks>
public interface IFencedOutboxStoreDiagnostics
{
	/// <summary>
	/// Reads the store's currently recorded fencing high-water mark.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns>
	/// The recorded high-water mark, or <see langword="null" /> when no fencing token has ever been
	/// presented to this store.
	/// </returns>
	Task<long?> GetFencingHighWaterAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Administratively sets the store's fencing high-water mark.
	/// </summary>
	/// <param name="newHighWater"> The high-water mark to record. </param>
	/// <param name="force">
	/// When <see langword="false" /> (the default an operator should reach for), the store MUST refuse a
	/// <paramref name="newHighWater" /> strictly below the currently recorded value and throw
	/// <see cref="InvalidOperationException" /> without changing anything. When <see langword="true" />,
	/// the store MUST apply <paramref name="newHighWater" /> unconditionally, including a lowering.
	/// </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous reset operation. </returns>
	/// <remarks>
	/// <b>Lowering the high-water is unsafe by design.</b> The high-water exists to refuse a superseded
	/// leader; lowering it re-admits one — a leader whose token is now below the (lowered) high-water reads
	/// as current again and can claim and complete messages a fresher leader already owns. This is the same
	/// split-brain the fence exists to prevent, self-inflicted through the recovery surface meant to fix a
	/// stuck fence. <paramref name="force" /> exists for the one case where that risk is understood and
	/// accepted (recovering a genuinely poisoned fence, with independent confirmation no live claim
	/// conflicts with the new value) — never as a routine unstick button.
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// <paramref name="newHighWater" /> is below the recorded high-water mark and <paramref name="force" />
	/// is <see langword="false" />.
	/// </exception>
	Task ResetFencingHighWaterAsync(long newHighWater, bool force, CancellationToken cancellationToken);
}
