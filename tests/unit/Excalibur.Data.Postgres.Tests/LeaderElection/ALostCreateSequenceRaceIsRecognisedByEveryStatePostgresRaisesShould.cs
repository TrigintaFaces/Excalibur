// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.LeaderElection.Postgres;

namespace Excalibur.Data.Postgres.Tests.LeaderElection;

/// <summary>
/// Locks the set of PostgreSQL error states that mean "another session created this sequence first", which
/// is what <see cref="PostgresFencingTokenProvider"/> retries on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a unit arm and not an integration one, which is the whole point of the file.</b> A
/// concurrent fencing mint DID surface this defect against a real server -- once, in a run of 3,836
/// integration tests. It cannot be relied on to surface it again: <c>CREATE SEQUENCE IF NOT EXISTS</c> only
/// errors when one session's existence check precedes another session's catalog insert, a window neither
/// session controls, and the integration arm passes against a mutant that disables the fix entirely.
/// An arm that passes over a disabled fix is not locking the behaviour, whatever it asserts.
/// </para>
/// <para>
/// <b>What was actually wrong was a constant, not a race.</b> The provider anticipated the race, explained
/// it in a comment, and retried -- filtering on <c>23505</c> alone. PostgreSQL raised <c>42P07</c>: a
/// sequence is a relation, so the duplicate-relation check can reject a lost create before the catalog's
/// unique index does. The handler existed, read correctly, and filtered on a state the server does not
/// always raise. So the state LIST is the thing to lock, and it is deterministic.
/// </para>
/// <para>
/// <b>RED input, stated so it is not left to a reader to guess:</b> remove either state from the predicate,
/// or change either constant's value, and the corresponding arm below fails. The earlier attempt to mutate
/// this by deleting a constant failed to COMPILE instead (unused field, warnings-as-errors) -- a mutant that
/// does not build has measured nothing, so the mutants here change values rather than remove members.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class ALostCreateSequenceRaceIsRecognisedByEveryStatePostgresRaisesShould
{
	/// <summary>
	/// 42P07 is duplicate_table, and it is the state a real concurrent mint produced. This is the arm whose
	/// absence let the defect ship.
	/// </summary>
	[Fact]
	public void RecogniseDuplicateTable_TheStateARealConcurrentMintRaised() =>
		PostgresFencingTokenProvider.IsLostCreateRace("42P07").ShouldBeTrue(
			"a sequence is a relation, so a lost CREATE SEQUENCE race can be rejected by the "
			+ "duplicate-relation check. This is the state observed from a real concurrent mint, and not "
			+ "recognising it means the retry written for the race never runs and the losing leader is "
			+ "denied a fencing token");

	/// <summary>
	/// 23505 is unique_violation on the catalog's name index -- the state the provider originally expected.
	/// Keeping it is not optional: the two are alternatives, not a correction.
	/// </summary>
	[Fact]
	public void RecogniseUniqueViolation_TheStateTheProviderOriginallyExpected() =>
		PostgresFencingTokenProvider.IsLostCreateRace("23505").ShouldBeTrue(
			"the catalog's unique index on relation name can reject the lost create instead. Adding the "
			+ "duplicate-relation state must not drop this one -- they are two ways the same race surfaces");

	/// <summary>
	/// LIVENESS. Without this, a predicate that returns true for everything satisfies both arms above while
	/// swallowing genuine failures into a retry that cannot help.
	/// </summary>
	/// <remarks>
	/// The states chosen are ones that must NOT be retried: a syntax error, a missing relation, a serialization
	/// failure, and the sequence-exhaustion state the provider translates into its own exhausted-token
	/// exception. Retrying any of them without the CREATE would either fail identically or, worse, mask a
	/// condition the caller must act on -- and sequence exhaustion masked as a retry is the split-brain case,
	/// because a wrapped fencing token is indistinguishable from a fresh one.
	/// </remarks>
	[Theory]
	[InlineData("42601")] // syntax_error
	[InlineData("42P01")] // undefined_table
	[InlineData("40001")] // serialization_failure
	[InlineData("2200H")] // sequence_generator_limit_exceeded -- translated, never retried
	[InlineData("")]
	[InlineData(null)]
	public void NotRecogniseAnythingElse(string? sqlState) =>
		PostgresFencingTokenProvider.IsLostCreateRace(sqlState).ShouldBeFalse(
			$"'{sqlState}' is not a lost create race, and retrying it without the CREATE cannot help. "
			+ "Sequence exhaustion in particular must reach its own translation: a reused fencing token is "
			+ "a split-brain, so it must never be swallowed by a retry");
}
