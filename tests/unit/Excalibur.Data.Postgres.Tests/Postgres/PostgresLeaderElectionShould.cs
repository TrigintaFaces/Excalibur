// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.LeaderElection;
using Excalibur.Dispatch.LeaderElection;

using Npgsql;


namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
	public async Task BecomeLeader_PrivateMethod_SetsStateAndRaisesEvents()
	{
		await using var election = CreateElection();
		var becameLeaderRaised = false;
		var leaderChangedRaised = false;

		election.BecameLeader += (_, _) => becameLeaderRaised = true;
		election.LeaderChanged += (_, args) =>
		{
			leaderChangedRaised = true;
			args.NewLeaderId.ShouldBe(election.CandidateId);
		};

		var becomeLeader = typeof(PostgresLeaderElection).GetMethod(
			"BecomeLeader",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

		becomeLeader.Invoke(election, null);

		election.IsLeader.ShouldBeTrue();
		election.CurrentLeaderId.ShouldBe(election.CandidateId);
		becameLeaderRaised.ShouldBeTrue();
		leaderChangedRaised.ShouldBeTrue();
	}

	[Fact]
	public async Task BecomeLeader_PrivateMethod_IsIdempotentWhenAlreadyLeader()
	{
		await using var election = CreateElection();
		var becameLeaderCount = 0;

		election.BecameLeader += (_, _) => becameLeaderCount++;

		var becomeLeader = typeof(PostgresLeaderElection).GetMethod(
			"BecomeLeader",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

		becomeLeader.Invoke(election, null);
		becomeLeader.Invoke(election, null);

		becameLeaderCount.ShouldBe(1);
		election.IsLeader.ShouldBeTrue();
	}

	[Fact]
	public async Task LoseLeadership_PrivateMethod_ClearsLeaderAndRaisesEvents()
	{
		await using var election = CreateElection();
		var lostLeadershipRaised = false;
		var leaderChangedRaised = false;

		election.LostLeadership += (_, _) => lostLeadershipRaised = true;

		var becomeLeader = typeof(PostgresLeaderElection).GetMethod(
			"BecomeLeader",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		becomeLeader.Invoke(election, null);

		election.LeaderChanged += (_, args) =>
		{
			leaderChangedRaised = true;
			args.NewLeaderId.ShouldBeNull();
		};

		var loseLeadership = typeof(PostgresLeaderElection).GetMethod(
			"LoseLeadership",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		loseLeadership.Invoke(election, null);

		election.IsLeader.ShouldBeFalse();
		election.CurrentLeaderId.ShouldBeNull();
		lostLeadershipRaised.ShouldBeTrue();
		leaderChangedRaised.ShouldBeTrue();
	}

	[Fact]
	public async Task LoseLeadership_PrivateMethod_NoOpWhenNotLeader()
	{
		await using var election = CreateElection();
		var lostLeadershipRaised = false;
		election.LostLeadership += (_, _) => lostLeadershipRaised = true;

		var loseLeadership = typeof(PostgresLeaderElection).GetMethod(
			"LoseLeadership",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		loseLeadership.Invoke(election, null);

		election.IsLeader.ShouldBeFalse();
		election.CurrentLeaderId.ShouldBeNull();
		lostLeadershipRaised.ShouldBeFalse();
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
	public async Task VerifyLockAsync_PrivateMethod_ReturnsFalse_WhenConnectionIsNull()
	{
		await using var election = CreateElection();
		var verifyLock = typeof(PostgresLeaderElection).GetMethod(
			"VerifyLockAsync",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

		var task = (Task<bool>)verifyLock.Invoke(election, [CancellationToken.None])!;
		var result = await task;

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyLockAsync_PrivateMethod_ReturnsFalse_WhenConnectionIsClosed()
	{
		await using var election = CreateElection();

		var connectionField = typeof(PostgresLeaderElection).GetField(
			"_connection",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		connectionField.SetValue(election, new NpgsqlConnection("Host=127.0.0.1;Port=1;Database=test;Timeout=1;"));

		var verifyLock = typeof(PostgresLeaderElection).GetMethod(
			"VerifyLockAsync",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

		var task = (Task<bool>)verifyLock.Invoke(election, [CancellationToken.None])!;
		var result = await task;

		result.ShouldBeFalse();
	}
}

