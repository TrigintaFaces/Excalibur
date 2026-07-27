// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.InMemory;

// Independent regression lock (author != implementer) for the 4cyuud fencing-token invariant:
//
//   "No fence" is expressed as a NULL FencingToken, NEVER an in-band 0. Leadership.FencingToken is long?;
//   null = the provider cannot (or does not) fence; a real token is >= 1, sourced only from a durable,
//   strictly-monotonic provider. An in-band 0 is the bug this locks against — a consumer that reads a 0 as
//   "a valid low token" would present it to a fencing store and defeat split-brain protection.
//
// InMemoryLeaderElection genuinely has NO fencing provider (single-process, no durable monotonic source), so
// it is the crisp unit witness for the "unavailable => null, never 0" half of the invariant: a leader here
// holds a real leadership record whose FencingToken is deliberately null. The ">= 1 when available + monotonic
// on takeover" half requires a durable provider and is covered by the MongoDb/SqlServer takeover integration
// locks.
//
// SAFETY + LIVENESS (testing-patterns §3): asserting only "token is null" is satisfied vacuously by a provider
// that reports NO leadership at all — so the liveness arm proves a leader genuinely HOLDS leadership (a non-null
// Leadership record) whose token is null, distinct from "not a leader, so no record."
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryLeaderElectionFencingTokenShould : UnitTestBase
{
	private readonly InMemoryLeaderElection _election;

	public InMemoryLeaderElectionFencingTokenShould()
	{
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "fencing-token-instance",
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromSeconds(5),
		});
		_election = new InMemoryLeaderElection($"fencing-resource-{Guid.NewGuid():N}", options, logger: null);
	}

	[Fact]
	public async Task ReportNullFencingToken_WhenLeader_BecauseFencingIsUnavailable_NeverInBandZero()
	{
		// Arrange + Act
		await _election.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// LIVENESS: the instance genuinely holds leadership — a non-null Leadership record. Without this, the
		// SAFETY assertion below is vacuous (a null token is trivially true when there is no leadership at all).
		_election.IsLeader.ShouldBeTrue("the sole candidate must acquire leadership");
		var leadership = _election.CurrentLeadership;
		leadership.ShouldNotBeNull(
			"a leader must hold a Leadership record — the point of this lock is that the record EXISTS and its " +
			"token is deliberately null, not that leadership is absent.");

		// SAFETY (the 4cyuud invariant): no fencing provider => token is NULL, never an in-band 0. A regression to
		// `new Leadership(FencingToken: 0, ...)` reintroduces the in-band-zero bug and fails here.
		leadership.Value.FencingToken.ShouldBeNull(
			"InMemoryLeaderElection has no durable monotonic fencing source, so 'no fence' MUST be null — never 0. " +
			"An in-band 0 reads to a consumer as a valid low token and would be presented to a fencing store, " +
			"defeating the split-brain guard. null is the honest 'unfenced' signal; 0 is a lie.");
	}

	[Fact]
	public void ReportNoLeadership_WhenNotLeader()
	{
		// LIVENESS boundary: a non-leader has NO leadership record (null), so the null-token assertion above is
		// about a real leader's record, not about the absence of leadership.
		_election.IsLeader.ShouldBeFalse("no StartAsync was called, so this instance is not a leader");
		_election.CurrentLeadership.ShouldBeNull(
			"a non-leader must report no leadership at all (null) — distinct from a leader whose FencingToken is null.");
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_election.Dispose();
		}

		base.Dispose(disposing);
	}
}
