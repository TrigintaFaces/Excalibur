// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Watch;

/// <summary>
/// Default implementation of <see cref="ILeaderElectionWatcher"/> that polls
/// <see cref="ILeaderElection"/> at a configurable interval and yields
/// <see cref="LeaderChangeEvent"/> instances via <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="Channel{T}"/> internally for the async enumerable pattern,
/// consistent with Microsoft's <c>ChannelReader&lt;T&gt;.ReadAllAsync()</c> approach.
/// The poll loop runs until the <see cref="CancellationToken"/> is triggered.
/// </para>
/// <para>
/// When <see cref="LeaderWatchOptions.IncludeHeartbeats"/> is <see langword="true"/>, an event
/// is emitted on every poll cycle regardless of whether the leader changed. When
/// <see langword="false"/> (default), only actual leader changes produce events.
/// </para>
/// </remarks>
public sealed partial class DefaultLeaderElectionWatcher : ILeaderElectionWatcher
{
	private readonly ILeaderElection _leaderElection;
	private readonly LeaderWatchOptions _options;
	private readonly ILogger<DefaultLeaderElectionWatcher> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultLeaderElectionWatcher"/> class.
	/// </summary>
	/// <param name="leaderElection">The leader election service to poll.</param>
	/// <param name="options">The watcher configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public DefaultLeaderElectionWatcher(
		ILeaderElection leaderElection,
		IOptions<LeaderWatchOptions> options,
		ILogger<DefaultLeaderElectionWatcher> logger)
	{
		ArgumentNullException.ThrowIfNull(leaderElection);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_leaderElection = leaderElection;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<LeaderChangeEvent> WatchAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var channel = Channel.CreateBounded<LeaderChangeEvent>(new BoundedChannelOptions(capacity: 16)
		{
			SingleWriter = true,
			SingleReader = true,
			FullMode = BoundedChannelFullMode.Wait,
		});

		// Start the polling loop in the background
		var pollTask = PollLeaderAsync(channel.Writer, cancellationToken);

		await foreach (var changeEvent in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return changeEvent;
		}

		// Await the poll task to surface any exceptions
		await pollTask.ConfigureAwait(false);
	}

	private async Task PollLeaderAsync(
		ChannelWriter<LeaderChangeEvent> writer,
		CancellationToken cancellationToken)
	{
		string? previousLeader = null;
		var isFirstPoll = true;

		try
		{
			LogWatchStarted(_options.PollInterval.TotalSeconds);

			while (!cancellationToken.IsCancellationRequested)
			{
				var currentLeader = _leaderElection.CurrentLeaderId;

				if (isFirstPoll)
				{
					// On first poll, always emit an event to report initial state
					var initialEvent = new LeaderChangeEvent(
						PreviousLeader: null,
						NewLeader: currentLeader,
						ChangedAt: DateTimeOffset.UtcNow,
						Reason: currentLeader is not null ? LeaderChangeReason.Elected : LeaderChangeReason.Expired);

					await writer.WriteAsync(initialEvent, cancellationToken).ConfigureAwait(false);
					previousLeader = currentLeader;
					isFirstPoll = false;

					LogLeaderChangeDetected(previousLeader: null, currentLeader);
				}
				else if (!string.Equals(currentLeader, previousLeader, StringComparison.Ordinal))
				{
					// Leader changed
					var reason = DetermineChangeReason(previousLeader, currentLeader);
					var changeEvent = new LeaderChangeEvent(
						PreviousLeader: previousLeader,
						NewLeader: currentLeader,
						ChangedAt: DateTimeOffset.UtcNow,
						Reason: reason);

					await writer.WriteAsync(changeEvent, cancellationToken).ConfigureAwait(false);

					LogLeaderChangeDetected(previousLeader, currentLeader);
					previousLeader = currentLeader;
				}
				else if (_options.IncludeHeartbeats)
				{
					// No change but heartbeats enabled
					var heartbeat = new LeaderChangeEvent(
						PreviousLeader: currentLeader,
						NewLeader: currentLeader,
						ChangedAt: DateTimeOffset.UtcNow,
						Reason: LeaderChangeReason.Elected);

					await writer.WriteAsync(heartbeat, cancellationToken).ConfigureAwait(false);
				}

				try
				{
					await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Expected when cancellation is requested; complete the channel gracefully
		}
#pragma warning disable CA1031 // Do not catch general exception types -- poll loop must complete the channel to avoid hanging readers
		catch (Exception ex)
#pragma warning restore CA1031
		{
			LogWatchError(ex);
			writer.TryComplete(ex);
			return;
		}
		finally
		{
			LogWatchStopped();
		}

		writer.TryComplete();
	}

	private static LeaderChangeReason DetermineChangeReason(string? previousLeader, string? newLeader)
	{
		if (previousLeader is null && newLeader is not null)
		{
			return LeaderChangeReason.Elected;
		}

		if (previousLeader is not null && newLeader is null)
		{
			return LeaderChangeReason.Expired;
		}

		// Previous leader was different from new leader
		return LeaderChangeReason.Elected;
	}

	// --- LoggerMessage source-generated methods (Event ID range 3620-3629 from Excalibur.* reserved range) ---

	[LoggerMessage(
		EventId = 3620,
		Level = LogLevel.Information,
		Message = "Leader election watcher started with poll interval of {PollIntervalSeconds}s.")]
	private partial void LogWatchStarted(double pollIntervalSeconds);

	[LoggerMessage(
		EventId = 3621,
		Level = LogLevel.Information,
		Message = "Leader change detected: '{PreviousLeader}' -> '{NewLeader}'.")]
	private partial void LogLeaderChangeDetected(string? previousLeader, string? newLeader);

	[LoggerMessage(
		EventId = 3622,
		Level = LogLevel.Error,
		Message = "Leader election watcher encountered an error.")]
	private partial void LogWatchError(Exception exception);

	[LoggerMessage(
		EventId = 3623,
		Level = LogLevel.Information,
		Message = "Leader election watcher stopped.")]
	private partial void LogWatchStopped();
}
