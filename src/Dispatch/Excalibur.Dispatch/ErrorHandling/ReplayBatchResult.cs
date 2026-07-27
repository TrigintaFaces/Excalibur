namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// The outcome of a batch replay: how many entries were selected, how many were replayed, and whether the
/// selection was cut short by the caller's limit.
/// </summary>
/// <param name="Enumerated">The number of entries the filter selected, up to the caller's limit.</param>
/// <param name="Replayed">
/// The number of entries actually replayed. This can be lower than <paramref name="Enumerated"/> when an
/// individual entry could not be replayed; it is a subset of what was enumerated, never a wider claim.
/// </param>
/// <param name="Truncated">
/// <see langword="true"/> when the limit was reached and further entries may match the filter, so the batch
/// is <em>incomplete</em>; otherwise <see langword="false"/>.
/// </param>
/// <remarks>
/// <para>
/// A bare count cannot distinguish "1000 entries existed" from "1000 of 4000 were taken" — the operator
/// reads a truthful-looking number and acts on a silently incomplete job. Separating the three facts makes
/// that silence impossible to express: <see cref="Truncated"/> is the member that carries the warning, and
/// it has no value that means "I did not check".
/// </para>
/// <para>
/// <see cref="Truncated"/> reports that the <em>limit was reached</em>, which is not the same as proving
/// more entries remain — a filter matching exactly the limit reports <see langword="true"/> having drained
/// the queue. This is deliberate: the honest signal is "you cannot conclude this batch was complete", and
/// over-reporting that is safe where under-reporting it is not. Re-run until <see cref="Truncated"/> is
/// <see langword="false"/>.
/// </para>
/// </remarks>
public sealed record ReplayBatchResult(int Enumerated, int Replayed, bool Truncated);
