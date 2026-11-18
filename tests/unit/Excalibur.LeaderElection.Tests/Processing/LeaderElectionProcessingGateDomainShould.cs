// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.LeaderElection.Processing;

namespace Excalibur.LeaderElection.Tests.Processing;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Outbox")]
public sealed class LeaderElectionProcessingGateDomainShould
{
	[Fact]
	public void ExposeTheExactSharedInnerWithoutTransferringOwnership()
	{
		var inner = Election();
		var first = new LeaderElectionProcessingGate(inner);
		var second = new LeaderElectionProcessingGate(inner);
		((ILeaderProcessingAuthorityDomain)first).AuthorityDomain.ShouldBeSameAs(inner);
		((ILeaderProcessingAuthorityDomain)second).AuthorityDomain.ShouldBeSameAs(inner);
		ReferenceEquals(first.AuthorityDomain, second.AuthorityDomain).ShouldBeTrue();
		A.CallTo(() => inner.DisposeAsync()).MustNotHaveHappened();
		A.CallTo(() => inner.StartAsync(A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => inner.StopAsync(A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public void KeepDifferentInnerReferencesDistinctDespiteHostileEqualityAndEqualTokens()
	{
		var firstInner = Election();
		var secondInner = Election();
		A.CallTo(() => firstInner.Equals(A<object>._)).Returns(true);
		A.CallTo(() => secondInner.Equals(A<object>._)).Returns(true);
		A.CallTo(() => firstInner.GetHashCode()).Returns(0);
		A.CallTo(() => secondInner.GetHashCode()).Returns(0);
		firstInner.Equals(secondInner).ShouldBeTrue();
		var first = new LeaderElectionProcessingGate(firstInner);
		var second = new LeaderElectionProcessingGate(secondInner);
		ReferenceEquals(first.AuthorityDomain, second.AuthorityDomain).ShouldBeFalse();
		first.FencingToken.ShouldBe(7);
		second.FencingToken.ShouldBe(7);
		first.ShouldProcess.ShouldBeTrue();
		second.ShouldProcess.ShouldBeTrue();
	}

	private static ILeaderElection Election()
	{
		var election = A.Fake<ILeaderElection>();
		var terminal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => election.NotificationsCompleted).Returns(terminal.Task);
		A.CallTo(() => election.DisposeAsync()).ReturnsLazily(() =>
		{
			terminal.TrySetResult();
			return ValueTask.CompletedTask;
		});
		A.CallTo(() => election.CurrentLeadership).Returns(new Leadership(7, DateTimeOffset.UnixEpoch));
		return election;
	}
}
