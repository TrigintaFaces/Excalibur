// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Consul;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.Consul;

/// <summary>
/// The grace period is the framework-wide upper bound on how long a candidate may believe it holds
/// leadership without a confirmed exchange with the coordination store. These arms assert that bound is
/// enforced on the Consul provider, in both directions.
/// </summary>
/// <remarks>
/// Consul's own session TTL bounds when a DIFFERENT candidate may take the lock. It says nothing about
/// how long THIS candidate keeps reporting leadership while it cannot reach the server, so a
/// server-side TTL is not a substitute for the client-side bound asserted here.
/// <para>
/// The timer callbacks are invoked directly rather than waited for, so no arm depends on wall-clock
/// timing; elapsed time is supplied by a controllable time provider.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConsulGracePeriodShould
{
	private static readonly TimeSpan Grace = TimeSpan.FromSeconds(5);

	[Fact]
	public async Task KeepLeadership_WhenAReadFailsAndTheGraceBoundHasNotElapsed()
	{
		// Arrange -- leadership held, then Consul becomes unreachable for reads.
		var (sut, client, time) = await CreateLeaderAsync();
		FailConsulReachability(client);

		// Act -- one monitor tick, well inside the grace bound.
		time.Advance(Grace - TimeSpan.FromSeconds(1));
		await InvokeAsync(sut, "MonitorLeadershipAsync");

		// Assert -- a failed read says nothing about who holds the lock, so leadership stands. Dropping
		// it here would be a false relinquish with no grace at all.
		sut.IsLeader.ShouldBeTrue();

		await sut.DisposeAsync();
	}

	[Fact]
	public async Task RelinquishLeadership_WhenReadsKeepFailingPastTheGraceBound()
	{
		// Arrange
		var (sut, client, time) = await CreateLeaderAsync();
		FailConsulReachability(client);

		// Act -- a tick inside the bound, then one past it.
		time.Advance(Grace - TimeSpan.FromSeconds(1));
		await InvokeAsync(sut, "MonitorLeadershipAsync");
		sut.IsLeader.ShouldBeTrue("still inside the bound");

		time.Advance(TimeSpan.FromSeconds(2));
		await InvokeAsync(sut, "MonitorLeadershipAsync");

		// Assert
		sut.IsLeader.ShouldBeFalse();

		await sut.DisposeAsync();
	}

	[Fact]
	public async Task RelinquishLeadership_WhenRenewalsKeepFailingPastTheGraceBound()
	{
		// Arrange -- renewals throw, which is ambiguous about ownership: the session may be alive, or it
		// may already have lapsed server-side and been handed on.
		var (sut, client, time) = await CreateLeaderAsync();
		_ = A.CallTo(() => client.Session.Renew(A<string>._)).ThrowsAsync(new InvalidOperationException("unreachable"));

		// Act
		time.Advance(Grace - TimeSpan.FromSeconds(1));
		await InvokeAsync(sut, "RenewSessionAsync");
		sut.IsLeader.ShouldBeTrue("a transient renewal fault inside the bound must not relinquish");

		time.Advance(TimeSpan.FromSeconds(2));
		await InvokeAsync(sut, "RenewSessionAsync");

		// Assert
		sut.IsLeader.ShouldBeFalse();

		await sut.DisposeAsync();
	}

	[Fact]
	public async Task RestartTheGraceBound_WhenARenewalSucceeds()
	{
		// Arrange
		var (sut, client, time) = await CreateLeaderAsync();

		// Act -- a successful renewal near the bound, then time past what would have been the deadline.
		time.Advance(Grace - TimeSpan.FromSeconds(1));
		await InvokeAsync(sut, "RenewSessionAsync");

		_ = A.CallTo(() => client.Session.Renew(A<string>._)).ThrowsAsync(new InvalidOperationException("unreachable"));
		time.Advance(TimeSpan.FromSeconds(2));
		await InvokeAsync(sut, "RenewSessionAsync");

		// Assert -- the bound is measured from the last CONFIRMED exchange, not from acquisition, so the
		// successful renewal reset it and this fault is still inside the window.
		sut.IsLeader.ShouldBeTrue();

		await sut.DisposeAsync();
	}

	[Fact]
	public async Task RelinquishLeadershipImmediately_WhenConsulReportsTheSessionGone()
	{
		// Arrange -- a null renew response is Consul stating the session is gone. That is definitive, not
		// ambiguous, so the grace period must not delay the relinquish.
		var (sut, client, _) = await CreateLeaderAsync();
		_ = A.CallTo(() => client.Session.Renew(A<string>._))
			.Returns(Task.FromResult(new WriteResult<SessionEntry> { Response = null }));
		_ = A.CallTo(() => client.KV.Acquire(A<KVPair>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<bool> { Response = false }));

		// Act -- no time advanced at all.
		await InvokeAsync(sut, "RenewSessionAsync");

		// Assert
		sut.IsLeader.ShouldBeFalse();

		await sut.DisposeAsync();
	}

	// ---------------------------------------------------------------------------------------------

	private static async Task<(ConsulLeaderElection Sut, IConsulClient Client, FakeTimeProvider Time)> CreateLeaderAsync()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ConsulLeaderElectionOptions
		{
			ConsulAddress = "http://localhost:8500",
			InstanceId = "candidate-a",
			GracePeriod = Grace,

			// Far longer than any arm, so the real timers never fire and every tick under test is the one
			// the test invokes.
			RenewInterval = TimeSpan.FromHours(1),
		});

		var client = A.Fake<IConsulClient>();
		_ = A.CallTo(() => client.Session.Create(A<SessionEntry>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<string> { Response = "session-1" }));
		_ = A.CallTo(() => client.Session.Renew(A<string>._))
			.Returns(Task.FromResult(new WriteResult<SessionEntry> { Response = new SessionEntry() }));
		_ = A.CallTo(() => client.KV.Acquire(A<KVPair>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<bool> { Response = true }));
		_ = A.CallTo(() => client.KV.Put(A<KVPair>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new WriteResult<bool> { Response = true }));

		var time = new FakeTimeProvider();
		var sut = new ConsulLeaderElection(
			"test-resource", options, client, NullLogger<ConsulLeaderElection>.Instance, fencingTokenProvider: null, time);

		await sut.StartAsync(CancellationToken.None);
		sut.IsLeader.ShouldBeTrue("arrangement failed: the candidate never acquired leadership");
		return (sut, client, time);
	}

	/// <summary>
	/// Models an unreachable Consul: reads AND acquires fail. Failing only the read would let the
	/// monitor's "no leader, so try to acquire" path re-acquire instantly and mask the defect under
	/// test -- a partitioned candidate cannot acquire either.
	/// </summary>
	private static void FailConsulReachability(IConsulClient client)
	{
		_ = A.CallTo(() => client.KV.Get(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("unreachable"));
		_ = A.CallTo(() => client.KV.Acquire(A<KVPair>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("unreachable"));
	}

	private static Task InvokeAsync(ConsulLeaderElection sut, string method) =>
		(Task)typeof(ConsulLeaderElection)
			.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
			.Invoke(sut, null)!;
}
