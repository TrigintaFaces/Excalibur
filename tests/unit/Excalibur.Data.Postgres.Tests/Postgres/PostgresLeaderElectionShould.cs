// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.LeaderElection.Postgres;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;



namespace Excalibur.Data.Tests.Postgres;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PostgresLeaderElectionShould
{
	private static PostgresLeaderElection CreateElection(
		string connectionString = "Host=localhost;Database=test;",
		long lockKey = 1)
	{
		var pgOptions = Microsoft.Extensions.Options.Options.Create(new PostgresLeaderElectionOptions
		{
			ConnectionString = connectionString,
			LockKey = lockKey
		});
		var electionOptions = Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions());
		return new PostgresLeaderElection(
			pgOptions, electionOptions, EnabledTestLogger.Create<PostgresLeaderElection>());
	}


	[Fact]
	public void ThrowWhenPgOptionsIsNull()
	{
		var electionOptions = Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions());

		Should.Throw<ArgumentNullException>(
			() => new PostgresLeaderElection(
				null!, electionOptions, EnabledTestLogger.Create<PostgresLeaderElection>()));
	}

	[Fact]
	public void ThrowWhenElectionOptionsIsNull()
	{
		var pgOptions = Microsoft.Extensions.Options.Options.Create(new PostgresLeaderElectionOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});

		Should.Throw<ArgumentNullException>(
			() => new PostgresLeaderElection(
				pgOptions, null!, EnabledTestLogger.Create<PostgresLeaderElection>()));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var pgOptions = Microsoft.Extensions.Options.Options.Create(new PostgresLeaderElectionOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});
		var electionOptions = Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions());

		Should.Throw<ArgumentNullException>(
			() => new PostgresLeaderElection(pgOptions, electionOptions, null!));
	}

	[Fact]
	public void ThrowWhenConnectionStringIsEmpty()
	{
		var pgOptions = Microsoft.Extensions.Options.Options.Create(new PostgresLeaderElectionOptions
		{
			ConnectionString = string.Empty
		});
		var electionOptions = Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions());

		Should.Throw<InvalidOperationException>(
			() => new PostgresLeaderElection(
				pgOptions, electionOptions, EnabledTestLogger.Create<PostgresLeaderElection>()));
	}

	[Fact]
	public async Task HaveCandidateId()
	{
		await using var election = CreateElection();
		election.CandidateId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task NotBeLeaderInitially()
	{
		await using var election = CreateElection();
		election.IsLeader.ShouldBeFalse();
	}

	[Fact]
	public async Task HaveNullCurrentLeaderIdInitially()
	{
		await using var election = CreateElection();
		election.CurrentLeaderId.ShouldBeNull();
	}

	[Fact]
	public async Task UseCandidateIdFromOptions()
	{
		var pgOptions = Microsoft.Extensions.Options.Options.Create(new PostgresLeaderElectionOptions
		{
			ConnectionString = "Host=localhost;Database=test;"
		});
		var electionOptions = Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions
		{
			InstanceId = "custom-instance-id"
		});

		await using var election = new PostgresLeaderElection(
			pgOptions, electionOptions, EnabledTestLogger.Create<PostgresLeaderElection>());

		election.CandidateId.ShouldBe("custom-instance-id");
	}

	[Fact]
	public async Task DisposeAsync_DoesNotThrow()
	{
		var election = CreateElection();
		await Should.NotThrowAsync(() => election.DisposeAsync().AsTask());
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		var election = CreateElection();
		await election.DisposeAsync();
		await Should.NotThrowAsync(() => election.DisposeAsync().AsTask());
	}

	[Fact]
	public async Task StartAsync_ThrowsWhenDisposed()
	{
		var election = CreateElection();
		await election.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => election.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task StopAsync_ThrowsWhenDisposed()
	{
		var election = CreateElection();
		await election.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => election.StopAsync(CancellationToken.None));
	}

	[Fact]
    public async Task PublicAcquisitionSetsStateAndRaisesEventsAfterFencing()
    {
        var connection = new PostgresTenureProtocolShould.Session();
        var clock = new PostgresTenureProtocolShould.ScheduledClock();
        var fence = A.Fake<IFencingTokenIssuer>();
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._)).Returns(new ValueTask<long>(4242));
        await using var election = PostgresTenureProtocolShould.Create(connection, clock, fence: fence);
        var became = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        election.BecameLeader += (_, _) => became.TrySetResult();
        election.LeaderChanged += (_, args) => { args.NewLeaderId.ShouldBe(election.CandidateId); changed.TrySetResult(); };
        await election.StartAsync(CancellationToken.None);
        await Task.WhenAll(became.Task, changed.Task).WaitAsync(TimeSpan.FromSeconds(5));
        election.IsLeader.ShouldBeTrue(); election.CurrentLeaderId.ShouldBe(election.CandidateId);
        election.CurrentLeadership.ShouldNotBeNull(); election.CurrentLeadership.Value.FencingToken.ShouldBe(4242);
    }

	[Fact]
    public async Task RepeatedPublicStartDoesNotRestampTheFenceOrPublishAnotherTenure()
    {
        var connection = new PostgresTenureProtocolShould.Session();
        var fence = A.Fake<IFencingTokenIssuer>(); var issued = 0;
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => new ValueTask<long>(Interlocked.Increment(ref issued) == 1 ? 7L : 99L));
        await using var election = PostgresTenureProtocolShould.Create(connection, new PostgresTenureProtocolShould.ScheduledClock(), fence: fence);
        var became = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var count = 0;
        election.BecameLeader += (_, _) => { Interlocked.Increment(ref count); became.TrySetResult(); };
        await election.StartAsync(CancellationToken.None); await became.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await election.StartAsync(CancellationToken.None);
        count.ShouldBe(1); issued.ShouldBe(1); connection.OpenCount.ShouldBe(1);
        election.CurrentLeadership!.Value.FencingToken.ShouldBe(7);
    }

	[Fact]
    public async Task PublicStopClearsOwnershipAndDeliversTheCommittedLoss()
    {
        var connection = new PostgresTenureProtocolShould.Session();
        await using var election = PostgresTenureProtocolShould.Create(connection, new PostgresTenureProtocolShould.ScheduledClock());
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        election.LostLeadership += (_, _) => lost.TrySetResult();
        election.LeaderChanged += (_, args) => { if (args.NewLeaderId is null) { changed.TrySetResult(); } };
        await election.StartAsync(CancellationToken.None); await election.StopAsync(CancellationToken.None);
        await Task.WhenAll(lost.Task, changed.Task).WaitAsync(TimeSpan.FromSeconds(5));
        election.IsLeader.ShouldBeFalse(); election.CurrentLeaderId.ShouldBeNull(); election.CurrentLeadership.ShouldBeNull();
        connection.DisposeCount.ShouldBe(1);
    }

	[Fact]
    public async Task PublicStopBeforeStartDoesNotCreateAnOwnershipSessionOrLossEvent()
    {
        var connection = new PostgresTenureProtocolShould.Session(); var created = 0; var losses = 0;
        await using var election = PostgresTenureProtocolShould.Create(connection, new PostgresTenureProtocolShould.ScheduledClock(),
            factory: _ => { Interlocked.Increment(ref created); return connection; });
        election.LostLeadership += (_, _) => Interlocked.Increment(ref losses);
        await election.StopAsync(CancellationToken.None);
        created.ShouldBe(0); losses.ShouldBe(0); election.IsLeader.ShouldBeFalse(); election.CurrentLeaderId.ShouldBeNull();
    }

	[Fact]
	public async Task StartAndStopAsync_HandleUnavailableDatabaseWithoutThrowing()
	{
		await using var election = CreateElection("Host=127.0.0.1;Port=1;Database=test;Timeout=1;");

		await Should.NotThrowAsync(() => election.StartAsync(CancellationToken.None));
		await Should.NotThrowAsync(() => election.StopAsync(CancellationToken.None));
	}

	[Fact]
	public async Task StartAsync_CalledTwice_SecondCallIsNoOp()
	{
		await using var election = CreateElection("Host=127.0.0.1;Port=1;Database=test;Timeout=1;");

		await election.StartAsync(CancellationToken.None);
		await Should.NotThrowAsync(() => election.StartAsync(CancellationToken.None));
		await election.StopAsync(CancellationToken.None);
	}

	[Fact]
    public async Task HaveNoOwnershipProofBeforeAnySessionIsAdmitted()
    {
        await using var election = CreateElection();
        election.IsLeader.ShouldBeFalse(); election.CurrentLeadership.ShouldBeNull(); election.CurrentLeaderId.ShouldBeNull();
    }

	[Fact]
    public async Task RetireAClosedSessionWithoutReopeningIt()
    {
        var connection = new PostgresTenureProtocolShould.Session(); var clock = new PostgresTenureProtocolShould.ScheduledClock();
        await using var election = PostgresTenureProtocolShould.Create(connection, clock);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        election.LostLeadership += (_, _) => lost.TrySetResult();
        await election.StartAsync(CancellationToken.None); await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        connection.Close(); clock.Advance(TimeSpan.FromSeconds(5)); await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        election.IsLeader.ShouldBeFalse(); connection.OpenCount.ShouldBe(1);
    }
}

