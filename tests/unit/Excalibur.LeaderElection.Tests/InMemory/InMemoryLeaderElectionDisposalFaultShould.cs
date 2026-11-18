// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>
/// Subscriber failures settle on NotificationsCompleted independently of joined resource disposal.
/// This prevents a queued subscriber failure from replacing a consumer exception during using cleanup,
/// while preserving the exact subscriber failure for explicit terminal observation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryLeaderElectionDisposalFaultShould
{
	private const string SubscriberFault = "subscriber threw from the leadership-loss handler";
	private const string ConsumerFault = "the fault that actually brought the consumer's code down";

	[Fact]
	public async Task NotPropagateASubscriberFault_WhenDisposeRunsItsCleanup()
	{
		var election = await CreateLeadingElectionAsync();
		var subscriberFailure = new InvalidOperationException(SubscriberFault);
        election.LostLeadership += (_, _) => throw subscriberFailure;

		Should.NotThrow(election.Dispose);
        var notification = await Record.ExceptionAsync(() => election.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        notification.ShouldBeSameAs(subscriberFailure, "notification faults are observable separately from resource disposal");

	}

	[Fact]
	public async Task NotPropagateASubscriberFault_WhenDisposeAsyncRunsItsCleanup()
	{
		var election = await CreateLeadingElectionAsync();
		var subscriberFailure = new InvalidOperationException(SubscriberFault);
        election.LostLeadership += (_, _) => throw subscriberFailure;

		await Should.NotThrowAsync(async () => await election.DisposeAsync());
        var notification = await Record.ExceptionAsync(() => election.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        notification.ShouldBeSameAs(subscriberFailure, "notification faults are observable separately from resource disposal");

	}

	[Fact]
	public async Task PreserveTheExceptionAlreadyInFlight_WhenAUsingBlockExitsAndDisposalCleanupFaults()
	{
		var election = await CreateLeadingElectionAsync();
		var subscriberFailure = new InvalidOperationException(SubscriberFault);
        election.LostLeadership += (_, _) => throw subscriberFailure;

		// The liveness arm: the consumer's own exception must reach the consumer, not the cleanup fault.
		var observed = Should.Throw<TimeoutException>(() =>
		{
			using (election)
			{
				throw new TimeoutException(ConsumerFault);
			}
		});

		observed.Message.ShouldBe(ConsumerFault);
        var notification = await Record.ExceptionAsync(() => election.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        notification.ShouldBeSameAs(subscriberFailure, "notification faults are observable separately from resource disposal");

	}

	[Fact]
	public async Task PreserveTheExceptionAlreadyInFlight_WhenAnAwaitUsingBlockExitsAndDisposalCleanupFaults()
	{
		var election = await CreateLeadingElectionAsync();
		var subscriberFailure = new InvalidOperationException(SubscriberFault);
        election.LostLeadership += (_, _) => throw subscriberFailure;

		var observed = await Should.ThrowAsync<TimeoutException>(async () =>
		{
			await using (election)
			{
				throw new TimeoutException(ConsumerFault);
			}
		});

		observed.Message.ShouldBe(ConsumerFault);
        var notification = await Record.ExceptionAsync(() => election.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        notification.ShouldBeSameAs(subscriberFailure, "notification faults are observable separately from resource disposal");

	}

	private static async Task<InMemoryLeaderElection> CreateLeadingElectionAsync()
	{
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "disposal-fault-candidate",
			LeaseDuration = TimeSpan.FromSeconds(15),
			RenewInterval = TimeSpan.FromMinutes(5),
		});

		// A private shared state per arm, so the arms cannot contend for one resource name.
		var election = new InMemoryLeaderElection(
			$"disposal-fault-resource-{Guid.NewGuid():N}",
			options,
			logger: null,
			sharedState: new InMemoryLeaderElectionSharedState());

		await election.StartAsync(CancellationToken.None);

		// Leadership is the precondition: disposal only raises LostLeadership for a candidate that holds
		// the resource, so an arm against a non-leader would pass without ever reaching the handler.
		election.IsLeader.ShouldBeTrue();
		return election;
	}
}
