// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Watch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Tests.Watch;

/// <summary>
/// Unit tests for <see cref="DefaultLeaderElectionWatcher"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class DefaultLeaderElectionWatcherShould
{
	[Fact]
	public async Task EmitInitialEventOnFirstPoll()
	{
		// Arrange
		var le = A.Fake<ILeaderElection>();
		A.CallTo(() => le.CurrentLeaderId).Returns("node-1");

		var options = Options.Create(new LeaderWatchOptions { PollInterval = TimeSpan.FromMilliseconds(50) });
		var watcher = new DefaultLeaderElectionWatcher(le, options, NullLogger<DefaultLeaderElectionWatcher>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		var events = new List<LeaderChangeEvent>();

		// Act
		await foreach (var evt in watcher.WatchAsync(cts.Token))
		{
			events.Add(evt);
			break; // Only need the first event
		}

		// Assert
		events.Count.ShouldBe(1);
		events[0].PreviousLeader.ShouldBeNull();
		events[0].NewLeader.ShouldBe("node-1");
		events[0].Reason.ShouldBe(LeaderChangeReason.Elected);
	}

	[Fact]
	public async Task EmitEventWhenLeaderChanges()
	{
		// Arrange
		var callCount = 0;
		var le = A.Fake<ILeaderElection>();
		A.CallTo(() => le.CurrentLeaderId).ReturnsLazily(() =>
		{
			callCount++;
			return callCount <= 2 ? "node-1" : "node-2";
		});

		var options = Options.Create(new LeaderWatchOptions { PollInterval = TimeSpan.FromMilliseconds(50) });
		var watcher = new DefaultLeaderElectionWatcher(le, options, NullLogger<DefaultLeaderElectionWatcher>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
		var events = new List<LeaderChangeEvent>();

		// Act
		await foreach (var evt in watcher.WatchAsync(cts.Token))
		{
			events.Add(evt);
			if (events.Count >= 2)
			{
				break;
			}
		}

		// Assert
		events.Count.ShouldBeGreaterThanOrEqualTo(2);
		events[0].NewLeader.ShouldBe("node-1");
		events[1].PreviousLeader.ShouldBe("node-1");
		events[1].NewLeader.ShouldBe("node-2");
	}

	[Fact]
	public async Task EmitExpiredReasonWhenLeaderBecomesNull()
	{
		// Arrange
		var callCount = 0;
		var le = A.Fake<ILeaderElection>();
		A.CallTo(() => le.CurrentLeaderId).ReturnsLazily(() =>
		{
			callCount++;
			return callCount <= 2 ? "node-1" : null;
		});

		var options = Options.Create(new LeaderWatchOptions { PollInterval = TimeSpan.FromMilliseconds(50) });
		var watcher = new DefaultLeaderElectionWatcher(le, options, NullLogger<DefaultLeaderElectionWatcher>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
		var events = new List<LeaderChangeEvent>();

		// Act
		await foreach (var evt in watcher.WatchAsync(cts.Token))
		{
			events.Add(evt);
			if (events.Count >= 2)
			{
				break;
			}
		}

		// Assert
		events.Count.ShouldBeGreaterThanOrEqualTo(2);
		events[1].Reason.ShouldBe(LeaderChangeReason.Expired);
	}

	[Fact]
	public async Task EmitInitialExpiredWhenNoLeaderOnFirstPoll()
	{
		// Arrange
		var le = A.Fake<ILeaderElection>();
		A.CallTo(() => le.CurrentLeaderId).Returns((string?)null);

		var options = Options.Create(new LeaderWatchOptions { PollInterval = TimeSpan.FromMilliseconds(50) });
		var watcher = new DefaultLeaderElectionWatcher(le, options, NullLogger<DefaultLeaderElectionWatcher>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		var events = new List<LeaderChangeEvent>();

		// Act
		await foreach (var evt in watcher.WatchAsync(cts.Token))
		{
			events.Add(evt);
			break;
		}

		// Assert
		events.Count.ShouldBe(1);
		events[0].NewLeader.ShouldBeNull();
		events[0].Reason.ShouldBe(LeaderChangeReason.Expired);
	}

	[Fact]
	public async Task EmitHeartbeatsWhenEnabled()
	{
		// Arrange
		var le = A.Fake<ILeaderElection>();
		A.CallTo(() => le.CurrentLeaderId).Returns("node-1");

		var options = Options.Create(new LeaderWatchOptions
		{
			PollInterval = TimeSpan.FromMilliseconds(50),
			IncludeHeartbeats = true,
		});
		var watcher = new DefaultLeaderElectionWatcher(le, options, NullLogger<DefaultLeaderElectionWatcher>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		var events = new List<LeaderChangeEvent>();

		// Act
		await foreach (var evt in watcher.WatchAsync(cts.Token))
		{
			events.Add(evt);
			if (events.Count >= 3)
			{
				break;
			}
		}

		// Assert - should get initial event plus heartbeats
		events.Count.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public void ThrowWhenLeaderElectionIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new DefaultLeaderElectionWatcher(
			null!,
			Options.Create(new LeaderWatchOptions()),
			NullLogger<DefaultLeaderElectionWatcher>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new DefaultLeaderElectionWatcher(
			A.Fake<ILeaderElection>(),
			null!,
			NullLogger<DefaultLeaderElectionWatcher>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new DefaultLeaderElectionWatcher(
			A.Fake<ILeaderElection>(),
			Options.Create(new LeaderWatchOptions()),
			null!));
	}
}
