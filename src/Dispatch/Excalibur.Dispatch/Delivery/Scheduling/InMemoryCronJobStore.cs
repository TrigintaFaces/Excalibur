// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// In-memory implementation of ICronJobStore for development and testing.
/// </summary>
public sealed class InMemoryCronJobStore : ICronJobStore
{
	private readonly ConcurrentDictionary<string, RecurringCronJob> _jobs = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, List<JobExecutionHistory>> _history = new(StringComparer.Ordinal);
#if NET9_0_OR_GREATER

	private readonly Lock _historyLock = new();

#else

	private readonly object _historyLock = new();

#endif

	/// <inheritdoc />
	public Task AddJobAsync(RecurringCronJob job, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(job);

		if (!_jobs.TryAdd(job.Id, job))
		{
			throw new InvalidOperationException($"Job with ID '{job.Id}' already exists.");
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task UpdateJobAsync(RecurringCronJob job, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(job);

		job.LastModifiedUtc = DateTimeOffset.UtcNow;
		_jobs[job.Id] = job;

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<bool> RemoveJobAsync(string jobId, CancellationToken cancellationToken)
	{
		var removed = _jobs.TryRemove(jobId, out _);
		if (removed)
		{
			_ = _history.TryRemove(jobId, out _);
		}

		return Task.FromResult(removed);
	}

	/// <inheritdoc />
	public Task<RecurringCronJob?> GetJobAsync(string jobId, CancellationToken cancellationToken)
	{
		_ = _jobs.TryGetValue(jobId, out var job);
		return Task.FromResult(job);
	}

	/// <inheritdoc />
	public Task<IEnumerable<RecurringCronJob>> GetActiveJobsAsync(CancellationToken cancellationToken)
	{
		var activeJobs = new List<RecurringCronJob>(_jobs.Count);
		foreach (var job in _jobs.Values)
		{
			if (job.IsEnabled)
			{
				activeJobs.Add(job);
			}
		}

		activeJobs.Sort(static (left, right) => Nullable.Compare(left.NextRunUtc, right.NextRunUtc));

		return Task.FromResult<IEnumerable<RecurringCronJob>>(activeJobs);
	}

	/// <inheritdoc />
	public Task<IEnumerable<RecurringCronJob>> GetDueJobsAsync(DateTimeOffset cutoffTime, CancellationToken cancellationToken)
	{
		var dueJobs = new List<RecurringCronJob>(_jobs.Count);
		foreach (var job in _jobs.Values)
		{
			var nextRunUtc = job.NextRunUtc;
			if (!job.IsEnabled || !nextRunUtc.HasValue || nextRunUtc.Value > cutoffTime || !job.ShouldRunAt(cutoffTime))
			{
				continue;
			}

			dueJobs.Add(job);
		}

		dueJobs.Sort(static (left, right) =>
		{
			var priorityComparison = left.Priority.CompareTo(right.Priority);
			return priorityComparison != 0 ? priorityComparison : Nullable.Compare(left.NextRunUtc, right.NextRunUtc);
		});

		return Task.FromResult<IEnumerable<RecurringCronJob>>(dueJobs);
	}

	/// <inheritdoc />
	public Task<IEnumerable<RecurringCronJob>> GetJobsByTagAsync(string tag, CancellationToken cancellationToken)
	{
		var taggedJobs = new List<RecurringCronJob>();
		foreach (var job in _jobs.Values)
		{
			if (ContainsTagIgnoreCase(job.Tags, tag))
			{
				taggedJobs.Add(job);
			}
		}

		taggedJobs.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));

		return Task.FromResult<IEnumerable<RecurringCronJob>>(taggedJobs);
	}

	/// <inheritdoc />
	public Task UpdateNextRunTimeAsync(string jobId, DateTimeOffset? nextRunUtc, CancellationToken cancellationToken)
	{
		if (_jobs.TryGetValue(jobId, out var job))
		{
			job.NextRunUtc = nextRunUtc;
			job.LastModifiedUtc = DateTimeOffset.UtcNow;
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task RecordExecutionAsync(string jobId, bool success, CancellationToken cancellationToken, string? errorMessage = null)
	{
		if (_jobs.TryGetValue(jobId, out var job))
		{
			job.UpdateRunStatistics(success, errorMessage);

			// Record in history
			lock (_historyLock)
			{
				if (!_history.TryGetValue(jobId, out var history))
				{
					history = [];
					_history[jobId] = history;
				}

				history.Add(new JobExecutionHistory
				{
					JobId = jobId,
					StartedUtc = job.LastRunUtc ?? DateTimeOffset.UtcNow,
					CompletedUtc = DateTimeOffset.UtcNow,
					Success = success,
					Error = errorMessage,
				});

				// Keep only the last 1000 entries per job
				if (history.Count > 1000)
				{
					history.RemoveRange(0, history.Count - 1000);
				}
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<bool> SetJobEnabledAsync(string jobId, bool enabled, CancellationToken cancellationToken)

	{
		if (_jobs.TryGetValue(jobId, out var job))
		{
			job.IsEnabled = enabled;
			job.LastModifiedUtc = DateTimeOffset.UtcNow;
			return Task.FromResult(true);
		}

		return Task.FromResult(false);
	}

	/// <inheritdoc />
	public Task<IEnumerable<JobExecutionHistory>> GetJobHistoryAsync(string jobId, CancellationToken cancellationToken,
		int limit = 100)
	{
		if (limit <= 0)
		{
			return Task.FromResult<IEnumerable<JobExecutionHistory>>([]);
		}

		lock (_historyLock)
		{
			if (_history.TryGetValue(jobId, out var history))
			{
				return Task.FromResult<IEnumerable<JobExecutionHistory>>(GetMostRecentHistory(history, limit));
			}
		}

		return Task.FromResult<IEnumerable<JobExecutionHistory>>([]);
	}

	/// <summary>
	/// Clears all jobs and history from the store.
	/// </summary>
	public void Clear()
	{
		_jobs.Clear();
		_history.Clear();
	}

	private static bool ContainsTagIgnoreCase(IEnumerable<string> tags, string tag)
	{
		foreach (var candidate in tags)
		{
			if (string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private static List<JobExecutionHistory> GetMostRecentHistory(List<JobExecutionHistory> history, int limit)
	{
		if (history.Count <= limit)
		{
			var all = new List<JobExecutionHistory>(history);
			all.Sort(static (left, right) => right.StartedUtc.CompareTo(left.StartedUtc));
			return all;
		}

		var recent = new List<JobExecutionHistory>(limit);
		for (var i = 0; i < history.Count; i++)
		{
			var entry = history[i];
			var position = recent.Count;
			while (position > 0 && recent[position - 1].StartedUtc < entry.StartedUtc)
			{
				position--;
			}

			if (recent.Count < limit)
			{
				recent.Insert(position, entry);
				continue;
			}

			if (position >= limit)
			{
				continue;
			}

			recent.Insert(position, entry);
			recent.RemoveAt(limit);
		}

		return recent;
	}
}
