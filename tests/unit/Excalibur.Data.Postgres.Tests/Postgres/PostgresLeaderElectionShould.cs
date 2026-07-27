// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Postgres;
using Excalibur.Dispatch.LeaderElection;

using Npgsql;


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

	/// <summary>
	///     Binds a private method by its exact signature.
	/// </summary>
	/// <remarks>
	///     Reflection binds by string, so the compiler cannot police this call site: a private member's arity may drift
	///     while every consumer still compiles. Binding by explicit parameter types turns that drift into a resolution
	///     failure with a self-describing message here, instead of an opaque
	///     <see cref="System.Reflection.TargetParameterCountException" /> raised at invoke time.
	/// </remarks>
	private static System.Reflection.MethodInfo GetPrivateMethod(string name, params Type[] parameterTypes)
	{
		var method = typeof(PostgresLeaderElection).GetMethod(
			name,
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
			binder: null,
			types: parameterTypes,
			modifiers: null);

		method.ShouldNotBeNull(
			$"PostgresLeaderElection.{name}({string.Join(", ", parameterTypes.Select(static t => t.Name))}) was not found. "
			+ "The private signature changed; this reflection-bound test must be updated in lockstep.");

		return method;
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

		const long fencingToken = 4242L;

		var becomeLeader = GetPrivateMethod("BecomeLeader", typeof(long));

		_ = becomeLeader.Invoke(election, [fencingToken]);

		election.IsLeader.ShouldBeTrue();
		election.CurrentLeaderId.ShouldBe(election.CandidateId);
		election.CurrentLeadership.ShouldNotBeNull();
		election.CurrentLeadership!.Value.FencingToken.ShouldBe(fencingToken);
		becameLeaderRaised.ShouldBeTrue();
		leaderChangedRaised.ShouldBeTrue();
	}

	[Fact]
	public async Task BecomeLeader_PrivateMethod_IsIdempotentWhenAlreadyLeader()
	{
		await using var election = CreateElection();
		var becameLeaderCount = 0;

		election.BecameLeader += (_, _) => becameLeaderCount++;

		const long firstToken = 7L;
		const long secondToken = 99L;

		var becomeLeader = GetPrivateMethod("BecomeLeader", typeof(long));

		_ = becomeLeader.Invoke(election, [firstToken]);
		_ = becomeLeader.Invoke(election, [secondToken]);

		becameLeaderCount.ShouldBe(1);
		election.IsLeader.ShouldBeTrue();

		// Idempotence must protect the fencing token too: a second acquisition attempt by an already-elected
		// candidate must not silently re-stamp leadership with a newer token, or a stale writer could be fenced
		// out by a token its own leader had quietly advanced past.
		election.CurrentLeadership.ShouldNotBeNull();
		election.CurrentLeadership!.Value.FencingToken.ShouldBe(firstToken);
	}

	[Fact]
	public async Task LoseLeadership_PrivateMethod_ClearsLeaderAndRaisesEvents()
	{
		await using var election = CreateElection();
		var lostLeadershipRaised = false;
		var leaderChangedRaised = false;

		election.LostLeadership += (_, _) => lostLeadershipRaised = true;

		var becomeLeader = GetPrivateMethod("BecomeLeader", typeof(long));
		_ = becomeLeader.Invoke(election, [11L]);

		election.LeaderChanged += (_, args) =>
		{
			leaderChangedRaised = true;
			args.NewLeaderId.ShouldBeNull();
		};

		var loseLeadership = GetPrivateMethod("LoseLeadershipAsync");
		await ((Task)loseLeadership.Invoke(election, null)!).ConfigureAwait(false);

		election.IsLeader.ShouldBeFalse();
		election.CurrentLeaderId.ShouldBeNull();
		election.CurrentLeadership.ShouldBeNull();
		lostLeadershipRaised.ShouldBeTrue();
		leaderChangedRaised.ShouldBeTrue();
	}

	[Fact]
	public async Task LoseLeadership_PrivateMethod_NoOpWhenNotLeader()
	{
		await using var election = CreateElection();
		var lostLeadershipRaised = false;
		election.LostLeadership += (_, _) => lostLeadershipRaised = true;

		var loseLeadership = GetPrivateMethod("LoseLeadershipAsync");
		await ((Task)loseLeadership.Invoke(election, null)!).ConfigureAwait(false);

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

