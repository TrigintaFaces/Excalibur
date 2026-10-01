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
	/// The row carries no position to advance from, so nothing was written and retrying cannot help.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This says what the WRITE did, and deliberately nothing about what the row contains.</b> A
	/// positioned write advances from a number and the row has none, so there was nothing to match and
	/// nothing was written. Every part of that is true atomically at the instant of the refusal.
	/// </para>
	/// <para>
	/// <b>It does NOT tell you whether the stored state is a usable fold, and must not be read as doing
	/// so.</b> This result carries no state, and the position it was classified against was read at a
	/// DIFFERENT instant from the one the write was refused at -- several providers must read the row after
	/// the engine rejects the write, and a concurrent writer can change it in between. An outcome that
	/// characterised what the row CONTAINS would therefore attribute a property of the row-at-read-time to
	/// a write refused earlier, and it could already be false by the time a caller acted on it. That is the
	/// unordered-observation defect this whole contract exists to remove, so the outcome does not carry the
	/// claim. To learn what the row holds, read its position:
	/// <see cref="ProjectionPositionKind"/> reports that as a measured fact, together with the value, in
	/// one observation -- and it distinguishes a complete fold whose coordinate is unknown from a state
	/// that is no fold at all.
	/// </para>
	/// <para>
	/// <b>This is terminal for the writer, and it is NOT a retry.</b> Re-reading yields the same row and
	/// the same refusal, so a caller that treats it as <see cref="Superseded"/> redelivers forever. The
	/// repair is <see cref="IPositionedProjectionStore{TProjection}.RebuildAtPositionAsync"/>, which writes
	/// state and position together and so needs no prior prefix to be conditional on. The writer's job here
	/// is to refuse to lie and to say which projection needs rebuilding, never to repair the row itself.
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
/// What a rebuild did — writing a whole-stream fold over whatever the row held.
/// </summary>
/// <remarks>
/// Two members, and there is no third. The operation is unconditional on POSITION, so it cannot be
/// superseded; it IS the rebuild, so it cannot ask for one. What it can still find is nothing at all,
/// and that is the one thing it has to report.
/// </remarks>
public enum ProjectionRebuildOutcome
{
	/// <summary>The row existed and now holds the given state at the given position.</summary>
	Applied,

	/// <summary>The row is gone, nothing was written, and that is SETTLED rather than an error.</summary>
	/// <remarks>
	/// Deletion is how erasure removes personal data, so recreating the row would reinstate what was
	/// erased. The caller stops; it does not retry, and it does not fall back to a create.
	/// </remarks>
	Vanished,
}

/// <summary>
/// The outcome of a rebuild. A SEPARATE type from <see cref="ProjectionRefoldResult"/>, deliberately.
/// </summary>
/// <param name="Outcome">What the rebuild did.</param>
/// <remarks>
/// <para>
/// <b>Why not reuse <see cref="ProjectionRefoldResult"/>, which already carries a Vanished.</b> Two of
/// its four members are unreachable here — <c>Superseded</c> because this operation compares no
/// position, and <c>RequiresRebuild</c> because this operation IS the rebuild — so reusing it would
/// oblige every caller to write two branches that can never be taken, and a reader could not tell those
/// from branches that merely have not happened yet.
/// </para>
/// <para>
/// <b>Why no position is carried, where the other two results carry one.</b> There is nothing to report:
/// on <see cref="ProjectionRebuildOutcome.Applied"/> the stored position is the argument the caller just
/// passed, and on <see cref="ProjectionRebuildOutcome.Vanished"/> there is no row to hold one. A field
/// that no provider could fill with a measured value would read as measured.
/// </para>
/// </remarks>
public readonly record struct ProjectionRebuildResult(ProjectionRebuildOutcome Outcome);

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
	/// <see cref="ProjectionPosition"/>. Only <see cref="ProjectionPositionKind.Positioned"/> can be
	/// advanced from: the other two carry no number for a positioned write to match, so both are refused.
	/// Both are refused with <see cref="ProjectionAdvanceOutcome.Unplaceable"/>, which reports only that
	/// there was no number to advance from, and both are repaired by
	/// <see cref="RebuildAtPositionAsync"/>. WHICH of the two a row is in is a question about the row, and
	/// this read is where it is answered -- the write's result does not carry it, because the write's result
	/// is not an observation of the row.
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
	/// <see cref="ProjectionPositionKind.Unplaceable"/> -- a strictly stronger and wrong claim.
	/// </para>
	/// <para>
	/// <b>THE ROW THIS WRITES IS A DEAD END UNTIL IT IS REBUILT, and that is the contract rather than a
	/// defect.</b> It carries no position, so no positioned write can advance it --
	/// <see cref="UpsertAtPositionAsync"/> refuses it with
	/// <see cref="ProjectionAdvanceOutcome.Unplaceable"/> -- and no re-fold can take it either, because
	/// <see cref="RefoldAtPositionAsync"/> matches against a position this row does not have and answers
	/// <see cref="ProjectionRefoldOutcome.RequiresRebuild"/>. The single way to bring the row back under
	/// the guarantee is <see cref="RebuildAtPositionAsync"/>, which writes the state and the position
	/// together and therefore needs no prior prefix to be conditional on.
	/// </para>
	/// <para>
	/// So this member suits a caller that has folded a prefix whose events carry no global position numbers
	/// yet, and that ACCEPTS the row must later be rebuilt in order to become positioned. What it buys over
	/// the blind surface is that the row states honestly what it holds -- a complete fold whose coordinate
	/// is unknown, rather than a state related to no prefix at all. That is what lets the stored value be
	/// READ as a complete answer while the row waits for its rebuild, and
	/// <see cref="GetWithPositionAsync"/> is where a caller learns it -- a positioned write refuses both
	/// alike and reports only that there was no number to advance from.
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
	/// The <see langword="null"/> case is insert-if-absent and NOTHING ELSE -- a write whose only
	/// condition is the ABSENCE of the row. It must never become an unconditional upsert, and it must not
	/// match an EXISTING row however that row's own position reads: a caller that read no position knows
	/// nothing about which prefix the stored state covers, so claiming one over it is a guess. An existing
	/// row is therefore refused -- <see cref="ProjectionAdvanceOutcome.Superseded"/> when it holds a real
	/// position, and <see cref="ProjectionAdvanceOutcome.Unplaceable"/> when it holds none, whichever of
	/// the two no-number states that is. The latter is repaired by
	/// <see cref="RebuildAtPositionAsync"/>.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <para>
	/// Thrown when <paramref name="newPosition"/> is negative. A negative is not merely meaningless, it
	/// is UNREADABLE BACK: only two of the eight providers fold a negative to "no position" on read, so
	/// on the other six a planted negative returns as an ordinary position and the write filter matches
	/// it. Refusing it at the entry point makes that inexpressible rather than defended unevenly.
	/// </para>
	/// <para>
	/// Also thrown when <paramref name="expectedPosition"/> is negative. The negatives are the sentinel
	/// space for the states that carry no number, so a caller passing one would be naming a sentinel as
	/// the position it read -- which is how a refused numberless row becomes adoptable again through a
	/// different door. Only <see cref="ProjectionPosition.ExpectedPositionOrNull"/> produces a legal value
	/// here, and it never produces a negative one.
	/// </para>
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

	/// <summary>
	/// Overwrites an EXISTING projection's state AND position, for a caller that folded the whole stream
	/// from an empty seed.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="projection">The state, folded from empty over every event at or below <paramref name="newPosition"/>.</param>
	/// <param name="newPosition">The position that state is a complete fold up to.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="ProjectionRebuildOutcome.Applied"/> when the row existed and now holds the given state
	/// at the given position; <see cref="ProjectionRebuildOutcome.Vanished"/> when no row exists, in
	/// which case nothing was written and the caller stops.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>When you are ALLOWED to call this, and it is a property of the CALLER rather than of the stored
	/// value.</b> You may call it only when the state you are writing is a fold from an EMPTY seed over
	/// every event at or below <paramref name="newPosition"/>. A rebuild satisfies that by construction:
	/// it discards whatever was stored and replays the stream. Nothing else does.
	/// </para>
	/// <para>
	/// <b>Why this exists as its own member.</b> A caller that folded from empty needs no expected
	/// position -- there is no prior prefix for its state to be conditional on. That licence used to be
	/// borrowed by passing a null expected position to
	/// <see cref="UpsertAtPositionAsync"/>, which ALSO let callers who had folded onto EXISTING state
	/// claim it. Those callers cannot know what prefix the stored state covered, so the position they
	/// stamped could assert a prefix the state did not hold -- silently, and indistinguishably from a
	/// correct advance. Giving the one legitimate caller its own signature is what makes the illegitimate
	/// call inexpressible rather than merely discouraged.
	/// </para>
	/// <para>
	/// <b>It MUST NOT create the row, exactly as <see cref="RefoldAtPositionAsync"/> must not.</b> An
	/// absent row means the projection was DELETED, deletion is how erasure removes personal data, and a
	/// rebuild that recreated it would reinstate what the erasure removed -- a whole-stream replay is
	/// precisely the write that can reconstruct it. Every implementation is UPDATE-only: there is no
	/// create arm to avoid taking, and <see cref="ProjectionRebuildOutcome.Vanished"/> is how the store
	/// reports that it found nothing.
	/// </para>
	/// <para>
	/// <b>The projection's processor MUST be stopped before you call this.</b> The operation is
	/// unconditional on position, so it cannot detect a concurrent advance -- those are the same check,
	/// and giving it up is the deliberate price of being able to overwrite a row whose stored number is
	/// unusable. It overwrites whatever it finds and reports only whether the row still exists. A rebuild
	/// already requires a stopped processor, so this states the precondition the caller is already under
	/// rather than adding a check that cannot be built.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="newPosition"/> is negative. The negatives are the sentinel space for
	/// the states that carry no number, so accepting one would let a caller forge them.
	/// </exception>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task<ProjectionRebuildResult> RebuildAtPositionAsync(
		string id,
		TProjection projection,
		long newPosition,
		CancellationToken cancellationToken);
}
