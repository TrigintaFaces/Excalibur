// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing;

/// <summary>
/// What a positioned projection write did.
/// </summary>
public enum ProjectionAdvanceOutcome
{
	/// <summary>The state and its position were written.</summary>
	Applied,

	/// <summary>
	/// The stored position was not the one the caller expected, so nothing was written.
	/// </summary>
	Superseded,

	/// <summary>
	/// The projection does not exist. It was deleted after the caller read it.
	/// </summary>
	/// <remarks>
	/// Distinct from <see cref="Superseded"/> on purpose, and the distinction is load-bearing: the
	/// correct responses are opposite. A superseded write is re-read and retried; a vanished projection
	/// MUST NOT be recreated, because deletion is how erasure removes personal data and re-folding the
	/// event stream would reinstate it.
	/// </remarks>
	Vanished,

	/// <summary>
	/// The stored state is not a fold over any prefix, so nothing was written and retrying cannot help.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The row holds <see cref="ProjectionPositionKind.Unplaceable"/>: an unconditional
	/// <see cref="IProjectionStore{TProjection}.UpsertAsync"/> replaced its state with a value the store
	/// cannot relate to the stream. Adopting it would fold this batch onto a state whose prefix nobody
	/// knows and then stamp this batch's position, making the row assert a prefix it does not hold.
	/// </para>
	/// <para>
	/// <b>This is terminal for the writer, and it is NOT a retry.</b> Re-reading yields the same row and
	/// the same refusal, so a caller that treats it as <see cref="Superseded"/> redelivers forever. The
	/// projection must be rebuilt from the stream before it can be advanced again; the writer's job here
	/// is to refuse to lie and to say which projection needs it, never to repair the row itself.
	/// </para>
	/// </remarks>
	Unplaceable,
}

/// <summary>
/// The outcome of a positioned write, with the position the store actually holds.
/// </summary>
/// <param name="Outcome">What the write did.</param>
/// <param name="CurrentPosition">
/// The position the store holds now. Meaningful on <see cref="ProjectionAdvanceOutcome.Superseded"/>,
/// where it is what lets the caller stop.
/// </param>
/// <remarks>
/// <b>Why the position is carried rather than looked up.</b> Without it a superseded caller cannot tell
/// "someone is ahead of me, re-read and retry" from "this batch is already applied, move on", so it
/// retries forever on the second. Every provider has the value in hand at the moment it refuses — a
/// conditional-check failure returns it, and the stores that must read before writing have already read
/// it — so carrying it costs nothing and removes a round trip on the contended path.
/// </remarks>
public readonly record struct ProjectionAdvanceResult(
	ProjectionAdvanceOutcome Outcome,
	long? CurrentPosition);

/// <summary>
/// What a re-fold did — rewriting a projection's state at the position it already holds.
/// </summary>
/// <remarks>
/// Same member names as <see cref="ProjectionAdvanceOutcome"/> on purpose: they describe the same
/// store decisions. They are a separate enum because they are consumed by a different settle rule,
/// and <see cref="ProjectionRefoldResult"/> exists to make mixing the two a compile error rather than
/// a silent compliance failure. See that type's remarks.
/// </remarks>
public enum ProjectionRefoldOutcome
{
	/// <summary>The state was rewritten. The position is unchanged.</summary>
	Applied,

	/// <summary>
	/// The stored position is not the one the caller named, so nothing was written.
	/// </summary>
	/// <remarks>
	/// <b>For a re-fold this is NEVER settled, whatever position the store holds.</b> See
	/// <see cref="ProjectionRefoldResult"/>.
	/// </remarks>
	Superseded,

	/// <summary>The projection does not exist, and a re-fold MUST NOT create it.</summary>
	/// <remarks>
	/// Deletion is how erasure removes personal data. Re-creating the row from a replay would
	/// reinstate the data the deletion removed, so a vanished row is left alone and the caller stops.
	/// </remarks>
	Vanished,

	/// <summary>
	/// The row's position cannot be matched, so the state's relationship to the stream is unknown and
	/// no re-fold can be placed against it. The projection must be rebuilt.
	/// </summary>
	/// <remarks>
	/// Terminal: retrying is futile because nothing about the row will change on its own. The caller
	/// escalates rather than looping.
	/// </remarks>
	RequiresRebuild,
}

/// <summary>
/// The outcome of a re-fold. A SEPARATE type from <see cref="ProjectionAdvanceResult"/>, deliberately.
/// </summary>
/// <param name="Outcome">What the re-fold did.</param>
/// <param name="CurrentPosition">The position the store holds now.</param>
/// <remarks>
/// <para>
/// <b>Why this is not a <see cref="ProjectionAdvanceResult"/>, which carries the same values.</b>
/// Because the two are settled by opposite rules, and the mistake is invisible.
/// </para>
/// <para>
/// For an ADVANCE, <c>Superseded</c> with a position at or beyond the one attempted means another
/// writer is ahead, the work is already done, and the caller should stop. For a RE-FOLD that is exactly
/// backwards: the writer who is ahead folded from the PRE-CHANGE stream, so being ahead is precisely
/// the condition under which the stale state has been reintroduced. A re-fold that stops there has
/// silently undone an erasure and reported success.
/// </para>
/// <para>
/// <b>That mistake has already shipped once</b>, which is why the type exists rather than a comment:
/// recovery passed a re-fold's outcome to the advance predicate, the predicate read it as settled, and
/// a projection kept an erased subject's data while the service logged a successful recovery. Passing
/// the wrong result type now fails to compile.
/// </para>
/// <para>
/// Note the tell: the re-fold rule needs no attempted position and the advance rule does. They are not
/// one function parameterised — they are different functions, and the signatures say so.
/// </para>
/// </remarks>
public readonly record struct ProjectionRefoldResult(
	ProjectionRefoldOutcome Outcome,
	long? CurrentPosition);

/// <summary>
/// A projection whose stored state carries the position of the last event folded into it, so a write
/// can be conditional on that position.
/// </summary>
/// <typeparam name="TProjection">The projection type. Must be a reference type.</typeparam>
/// <remarks>
/// <para>
/// <b>The invariant this exists to hold.</b> For a projection id <c>x</c>, with <c>P(x)</c> the stored
/// position:
/// </para>
/// <code>
/// state(x) = fold(apply, init, { e in stream : pos(e) &lt;= P(x) })
/// </code>
/// <para>
/// <c>P(x)</c> is not a number the writer chose. It is an assertion about which prefix of the stream is
/// folded into <c>state(x)</c>, and every write must make it true at the instant it commits. Four
/// obligations follow, and they are the whole contract:
/// </para>
/// <list type="number">
/// <item>a write advances the position only from the value its state was READ at — this orders
/// concurrent writers against each other;</item>
/// <item>a write advances the position only FORWARDS — this orders a writer against its own past, which
/// is what a redelivery is;</item>
/// <item>state and position are read together and written together — a split read lets the expected
/// value certify a prefix the state does not contain, and the comparison then ACCEPTS;</item>
/// <item>nothing writes the state while leaving the position behind.</item>
/// </list>
/// <para>
/// <b>Obligation 2 is not redundant with obligation 1, and this is the subtle part.</b> A caller obtains
/// its expected position by reading it, so on a redelivery the comparison is satisfied by construction —
/// the same writer, twice, is not a second writer. Only the forward-only rule refuses it.
/// </para>
/// <para>
/// <b>And the caller owes a filter.</b> Having read the position, it folds ONLY events above it.
/// Without that the store refuses the whole batch as non-advancing and the caller makes no progress;
/// with it, the events already folded are skipped and the rest are applied.
/// </para>
/// <para>
/// <b>This capability is not an optimisation, and it must never be silently absent.</b> The four
/// capabilities that preceded it compute what the base surface can compute, faster; falling back to the
/// base costs a round trip and nothing else. Falling back from this one reaches the unconditional
/// <see cref="IProjectionStore{TProjection}.UpsertAsync"/>, which is a DIFFERENT and weaker answer — the
/// double application this contract exists to make impossible. A caller that cannot obtain this
/// capability must fail, never degrade.
/// </para>
/// </remarks>
public interface IPositionedProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Reads the projection and the position of the last event folded into it, as one observation.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The projection and its position. The projection is <see langword="null"/> when none exists.
	/// The position is one of three states rather than a number with a missing case -- see
	/// <see cref="ProjectionPosition"/>. The distinction a caller must act on is between
	/// <see cref="ProjectionPositionKind.Unnumbered"/>, which a positioned writer MAY adopt, and
	/// <see cref="ProjectionPositionKind.Unplaceable"/>, which it must refuse.
	/// </returns>
	/// <remarks>
	/// <b>One call, deliberately.</b> Reading the state and the position separately admits a writer
	/// between them, after which the position the caller holds certifies a prefix its state does not
	/// contain — and the conditional write then accepts, losing every event in the gap. No caller can be
	/// correct on a split read, however atomic the write is.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(
		string id,
		CancellationToken cancellationToken);

	/// <summary>
	/// Writes a state that is a complete fold over a prefix which has no global position number.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="projection">The folded state.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes when the write is durable.</returns>
	/// <remarks>
	/// <para>
	/// <b>This exists so a caller with no position number does not have to reach for the blind surface.</b>
	/// The save path folds events that carry no global position yet; its state IS complete, only its
	/// coordinate is unknown. Before this member the only way to write that was
	/// <see cref="IProjectionStore{TProjection}.UpsertAsync"/>, which records
	/// <see cref="ProjectionPositionKind.Unplaceable"/> -- a strictly stronger and wrong claim, which a
	/// later positioned writer must refuse.
	/// </para>
	/// <para>
	/// <b>The caller states which of the three states it is producing; the store never infers it.</b>
	/// Inferring it would require the store to distinguish creating a row from updating one, in the same
	/// atomic action that writes the state -- and four of the document providers cannot do that at all,
	/// while the two that can could only do it with a second write whose crash window records a
	/// destroyed position as a never-established one. A store that must discriminate create from update
	/// to answer this has been asked the wrong question.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task UpsertUnnumberedAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken);

	/// <summary>
	/// Writes the projection and its new position together, only if the stored position is the one the
	/// caller expects and the new position is ahead of it.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="projection">The state to store.</param>
	/// <param name="expectedPosition">
	/// The position the caller read. <see langword="null"/> means the caller believes no projection
	/// exists, and MUST lose to a writer that has since created one.
	/// </param>
	/// <param name="newPosition">The position of the last event folded into <paramref name="projection"/>.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>What the write did, and the position the store holds.</returns>
	/// <remarks>
	/// <para>
	/// The state and the position are written in ONE indivisible action. A store that writes them
	/// separately has a window in which a crash leaves a position ahead of its state, and a projection
	/// whose position is ahead of its state is silently missing those events forever.
	/// </para>
	/// <para>
	/// The <see langword="null"/> case is insert-if-absent — a write whose condition is the ABSENCE of
	/// the row. It must never become an unconditional upsert, which would make the two prior states
	/// interchangeable and let a late writer reset a live projection.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="newPosition"/> is negative. A negative is not merely meaningless, it
	/// is UNREADABLE BACK: only two of the eight providers fold a negative to "no position" on read, so
	/// on the other six a planted negative returns as an ordinary position and the write filter matches
	/// it. Refusing it at the entry point makes that inexpressible rather than defended unevenly.
	/// </exception>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
		string id,
		TProjection projection,
		long? expectedPosition,
		long newPosition,
		CancellationToken cancellationToken);

	/// <summary>
	/// Rewrites a projection's state at the position it ALREADY holds, without advancing it.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="projection">The re-folded state.</param>
	/// <param name="atPosition">The position the row is expected to hold, and will still hold after.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="ProjectionRefoldOutcome.Applied"/> when the state was rewritten;
	/// <see cref="ProjectionRefoldOutcome.Superseded"/> when the row holds a different position;
	/// <see cref="ProjectionRefoldOutcome.Vanished"/> when the row is gone;
	/// <see cref="ProjectionRefoldOutcome.RequiresRebuild"/> when the row's position cannot be matched.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>When you are ALLOWED to call this</b>, which the name carries because the type system cannot:
	/// the events at or below <paramref name="atPosition"/> have CHANGED, so the state no longer
	/// matches the prefix the position names. Erasure is the case that produces it — it mutates events
	/// in place, so the fold changes while every position stays fixed.
	/// </para>
	/// <para>
	/// <b>It is not a general "replace the state" operation and must not be used as one.</b> Advancing
	/// a projection with new events is <see cref="UpsertAtPositionAsync"/>; that operation refuses a
	/// non-advancing write precisely to reject a redelivered batch, and this one exists only because
	/// that refusal is wrong when the stream beneath the position was rewritten.
	/// </para>
	/// <para>
	/// <paramref name="atPosition"/> is not nullable. Re-folding "at no position" is meaningless, so
	/// the bad call is made inexpressible rather than validated. A row carrying no established position
	/// has nothing to match and correctly yields
	/// <see cref="ProjectionRefoldOutcome.RequiresRebuild"/>.
	/// </para>
	/// <para>
	/// <b>A re-fold MUST NOT create a row.</b> An absent row means the projection was deleted, deletion
	/// is how erasure removes personal data, and recreating it would reinstate what was erased.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="atPosition"/> is negative. See the note on
	/// <see cref="UpsertAtPositionAsync"/>: a negative cannot be read back correctly on six of the eight
	/// providers, so it is refused rather than stored.
	/// </exception>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task<ProjectionRefoldResult> RefoldAtPositionAsync(
		string id,
		TProjection projection,
		long atPosition,
		CancellationToken cancellationToken);
}
