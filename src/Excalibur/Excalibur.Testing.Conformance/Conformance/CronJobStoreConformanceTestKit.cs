// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ICronJobStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your cron job store implementation conforms to the ICronJobStore contract.
/// </para>
/// <para>
/// The test kit verifies core cron job store operations including add, update, remove,
/// retrieval, due jobs, tags, execution recording, enable/disable, and history.
/// </para>
/// <para>
/// <strong>IMPORTANT:</strong> ICronJobStore has several key behaviors:
/// <list type="bullet">
/// <item><description><c>AddJobAsync</c> THROWS InvalidOperationException on duplicate (not upsert)</description></item>
/// <item><description><c>UpdateJobAsync</c> is UPSERT - creates if not exists, sets LastModifiedUtc</description></item>
/// <item><description><c>RemoveJobAsync</c> also removes associated execution history</description></item>
/// <item><description><c>GetDueJobsAsync</c> has complex filter: enabled + NextRunUtc &lt;= cutoff + ShouldRunAt</description></item>
/// <item><description><c>GetJobsByTagAsync</c> is case-insensitive</description></item>
/// <item><description><c>RecordExecutionAsync</c> updates BOTH job stats AND execution history</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerCronJobStoreConformanceTests : CronJobStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override ICronJobStore CreateStore() =&gt;
///         new SqlServerCronJobStore(_fixture.ConnectionString);
///
///     protected override async Task CleanupAsync() =&gt;
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class CronJobStoreConformanceTestKit
{
	/// <summary>
	/// Creates a fresh cron job store instance for testing.
	/// </summary>
	/// <returns>An ICronJobStore implementation to test.</returns>
	protected abstract ICronJobStore CreateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Creates a test recurring cron job with the given parameters.
	/// </summary>
	/// <param name="id">Optional job identifier. If not provided, a new GUID is generated.</param>
	/// <param name="name">Optional job name.</param>
	/// <param name="isEnabled">Whether the job is enabled. Default is true.</param>
	/// <param name="nextRunUtc">Optional next run time.</param>
	/// <param name="tags">Optional tags for the job.</param>
	/// <returns>A test recurring cron job.</returns>
	protected virtual RecurringCronJob CreateRecurringCronJob(
		string? id = null,
		string? name = null,
		bool isEnabled = true,
		DateTimeOffset? nextRunUtc = null,
		HashSet<string>? tags = null) =>
		new()
		{
			Id = id ?? GenerateJobId(),
			Name = name ?? "TestJob",
			CronExpression = "0 * * * *",
			MessageTypeName = "TestMessage",
			MessagePayload = "{}",
			IsEnabled = isEnabled,
			NextRunUtc = nextRunUtc,
			Tags = tags ?? [],
		};

	/// <summary>
	/// Generates a unique job ID for test isolation.
	/// </summary>
	/// <returns>A unique job identifier.</returns>
	protected virtual string GenerateJobId() => Guid.NewGuid().ToString("N");

	#region Add Tests

	/// <summary>
	/// Verifies that adding a new job persists it successfully.
	/// </summary>
	public virtual async Task AddJobAsync_ShouldPersistJob()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob();

		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Job with Id {job.Id} was not found after AddJobAsync");
		}

		if (retrieved.Name != job.Name)
		{
			throw new TestFixtureAssertionException(
				$"Name mismatch. Expected: {job.Name}, Actual: {retrieved.Name}");
		}

		if (retrieved.CronExpression != job.CronExpression)
		{
			throw new TestFixtureAssertionException(
				$"CronExpression mismatch. Expected: {job.CronExpression}, Actual: {retrieved.CronExpression}");
		}
	}

	/// <summary>
	/// Verifies that adding a null job throws ArgumentNullException.
	/// </summary>
	public virtual async Task AddJobAsync_WithNullJob_ShouldThrow()
	{
		var store = CreateStore();

		try
		{
			await store.AddJobAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected ArgumentNullException but no exception was thrown");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that adding a job with duplicate ID throws InvalidOperationException.
	/// </summary>
	public virtual async Task AddJobAsync_DuplicateId_ShouldThrowInvalidOperationException()
	{
		var store = CreateStore();
		var id = GenerateJobId();
		var job1 = CreateRecurringCronJob(id: id);
		var job2 = CreateRecurringCronJob(id: id);

		await store.AddJobAsync(job1, CancellationToken.None).ConfigureAwait(false);

		try
		{
			await store.AddJobAsync(job2, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected InvalidOperationException for duplicate job ID but no exception was thrown");
		}
		catch (InvalidOperationException)
		{
			// Expected - AddJobAsync throws on duplicate, NOT upsert
		}
	}

	#endregion

	#region Update Tests

	/// <summary>
	/// Verifies that updating a job modifies it successfully.
	/// </summary>
	public virtual async Task UpdateJobAsync_ShouldModifyJob()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob();

		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		job.Name = "UpdatedJobName";
		job.Description = "Updated description";

		await store.UpdateJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Job should remain in store after UpdateJobAsync");
		}

		if (retrieved.Name != "UpdatedJobName")
		{
			throw new TestFixtureAssertionException(
				$"Name should be 'UpdatedJobName' after update, got '{retrieved.Name}'");
		}

		if (retrieved.Description != "Updated description")
		{
			throw new TestFixtureAssertionException(
				$"Description should be 'Updated description' after update, got '{retrieved.Description}'");
		}
	}

	/// <summary>
	/// Verifies that UpdateJobAsync sets LastModifiedUtc automatically.
	/// </summary>
	public virtual async Task UpdateJobAsync_ShouldSetLastModifiedUtc()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob();

		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var beforeUpdate = DateTimeOffset.UtcNow;
		await Task.Delay(10).ConfigureAwait(false); // Small delay to ensure time difference

		job.Name = "ModifiedName";
		await store.UpdateJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Job should remain in store after UpdateJobAsync");
		}

		if (retrieved.LastModifiedUtc is null)
		{
			throw new TestFixtureAssertionException(
				"LastModifiedUtc should be set after UpdateJobAsync");
		}

		if (retrieved.LastModifiedUtc < beforeUpdate)
		{
			throw new TestFixtureAssertionException(
				"LastModifiedUtc should be after the update time");
		}
	}

	/// <summary>
	/// Verifies that UpdateJobAsync creates job if not exists (upsert).
	/// </summary>
	public virtual async Task UpdateJobAsync_NonExistent_ShouldUpsert()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob();

		// Update without prior Add - should act as upsert
		await store.UpdateJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"UpdateJobAsync should create job if it doesn't exist (upsert behavior)");
		}

		if (retrieved.Name != job.Name)
		{
			throw new TestFixtureAssertionException(
				$"Name mismatch after upsert. Expected: {job.Name}, Actual: {retrieved.Name}");
		}
	}

	#endregion

	#region Remove Tests

	/// <summary>
	/// Verifies that RemoveJobAsync returns true and removes the job.
	/// </summary>
	public virtual async Task RemoveJobAsync_ShouldReturnTrueAndRemove()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob();

		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var result = await store.RemoveJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		if (!result)
		{
			throw new TestFixtureAssertionException(
				"RemoveJobAsync should return true for existing job");
		}

		var retrieved = await store.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is not null)
		{
			throw new TestFixtureAssertionException(
				"Job should not be retrievable after RemoveJobAsync");
		}
	}

	/// <summary>
	/// Verifies that RemoveJobAsync returns false for non-existent job.
	/// </summary>
	public virtual async Task RemoveJobAsync_NonExistent_ShouldReturnFalse()
	{
		var store = CreateStore();
		var nonExistentId = GenerateJobId();

		var result = await store.RemoveJobAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);

		if (result)
		{
			throw new TestFixtureAssertionException(
				"RemoveJobAsync should return false for non-existent job");
		}
	}

	/// <summary>
	/// Verifies that RemoveJobAsync also removes associated execution history.
	/// </summary>
	public virtual async Task RemoveJobAsync_ShouldAlsoRemoveHistory()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob();

		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		// Record some execution history
		await store.RecordExecutionAsync(job.Id, true, CancellationToken.None).ConfigureAwait(false);
		await store.RecordExecutionAsync(job.Id, false, CancellationToken.None, "Error").ConfigureAwait(false);

		// Verify history exists
		var historyBefore = await store.GetJobHistoryAsync(job.Id, CancellationToken.None, 10).ConfigureAwait(false);
		if (!historyBefore.Any())
		{
			throw new TestFixtureAssertionException(
				"History should exist before removal");
		}

		// Remove the job
		_ = await store.RemoveJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		// Verify history is also removed
		var historyAfter = await store.GetJobHistoryAsync(job.Id, CancellationToken.None, 10).ConfigureAwait(false);
		if (historyAfter.Any())
		{
			throw new TestFixtureAssertionException(
				"History should be removed when job is removed");
		}
	}

	#endregion

	#region Retrieval Tests

	/// <summary>
	/// Verifies that GetJobAsync returns the job when it exists.
	/// </summary>
	public virtual async Task GetJobAsync_ExistingJob_ShouldReturnJob()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob();

		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"GetJobAsync should return job for Id {job.Id}");
		}

		if (retrieved.Id != job.Id)
		{
			throw new TestFixtureAssertionException(
				$"Id mismatch. Expected: {job.Id}, Actual: {retrieved.Id}");
		}
	}

	/// <summary>
	/// Verifies that GetJobAsync returns null for non-existent job.
	/// </summary>
	public virtual async Task GetJobAsync_NonExistent_ShouldReturnNull()
	{
		var store = CreateStore();
		var nonExistentId = GenerateJobId();

		var retrieved = await store.GetJobAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is not null)
		{
			throw new TestFixtureAssertionException(
				"GetJobAsync should return null for non-existent job");
		}
	}

	/// <summary>
	/// Verifies that GetActiveJobsAsync returns only enabled jobs.
	/// </summary>
	public virtual async Task GetActiveJobsAsync_ShouldReturnOnlyEnabled()
	{
		var store = CreateStore();

		var enabledJob = CreateRecurringCronJob(isEnabled: true, name: "EnabledJob");
		var disabledJob = CreateRecurringCronJob(isEnabled: false, name: "DisabledJob");

		await store.AddJobAsync(enabledJob, CancellationToken.None).ConfigureAwait(false);
		await store.AddJobAsync(disabledJob, CancellationToken.None).ConfigureAwait(false);

		var activeJobs = await store.GetActiveJobsAsync(CancellationToken.None).ConfigureAwait(false);
		var activeList = activeJobs.ToList();

		if (!activeList.Any(j => j.Id == enabledJob.Id))
		{
			throw new TestFixtureAssertionException(
				"Enabled job should be returned by GetActiveJobsAsync");
		}

		if (activeList.Any(j => j.Id == disabledJob.Id))
		{
			throw new TestFixtureAssertionException(
				"Disabled job should NOT be returned by GetActiveJobsAsync");
		}
	}

	#endregion

	#region Due Jobs Tests

	/// <summary>
	/// Verifies that GetDueJobsAsync returns due jobs.
	/// </summary>
	public virtual async Task GetDueJobsAsync_ShouldReturnDueJobs()
	{
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		var dueJob = CreateRecurringCronJob(
			isEnabled: true,
			nextRunUtc: now.AddMinutes(-5)); // Past due

		var futureJob = CreateRecurringCronJob(
			isEnabled: true,
			nextRunUtc: now.AddMinutes(30)); // Not yet due

		await store.AddJobAsync(dueJob, CancellationToken.None).ConfigureAwait(false);
		await store.AddJobAsync(futureJob, CancellationToken.None).ConfigureAwait(false);

		var dueJobs = await store.GetDueJobsAsync(now, CancellationToken.None).ConfigureAwait(false);
		var dueList = dueJobs.ToList();

		if (!dueList.Any(j => j.Id == dueJob.Id))
		{
			throw new TestFixtureAssertionException(
				"Due job should be returned by GetDueJobsAsync");
		}

		if (dueList.Any(j => j.Id == futureJob.Id))
		{
			throw new TestFixtureAssertionException(
				"Future job should NOT be returned by GetDueJobsAsync");
		}
	}

	/// <summary>
	/// Verifies that GetDueJobsAsync excludes disabled jobs.
	/// </summary>
	public virtual async Task GetDueJobsAsync_ShouldExcludeDisabled()
	{
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		var disabledDueJob = CreateRecurringCronJob(
			isEnabled: false,
			nextRunUtc: now.AddMinutes(-5)); // Past due but disabled

		await store.AddJobAsync(disabledDueJob, CancellationToken.None).ConfigureAwait(false);

		var dueJobs = await store.GetDueJobsAsync(now, CancellationToken.None).ConfigureAwait(false);
		var dueList = dueJobs.ToList();

		if (dueList.Any(j => j.Id == disabledDueJob.Id))
		{
			throw new TestFixtureAssertionException(
				"Disabled job should NOT be returned by GetDueJobsAsync even if past due");
		}
	}

	/// <summary>
	/// Verifies that GetDueJobsAsync respects StartDate and EndDate bounds.
	/// </summary>
	public virtual async Task GetDueJobsAsync_ShouldRespectStartEndDates()
	{
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		// Job that hasn't started yet (StartDate in future)
		var notStartedJob = CreateRecurringCronJob(
			isEnabled: true,
			nextRunUtc: now.AddMinutes(-5));
		notStartedJob.StartDate = now.AddDays(1); // Won't start until tomorrow

		// Job that has ended (EndDate in past)
		var endedJob = CreateRecurringCronJob(
			isEnabled: true,
			nextRunUtc: now.AddMinutes(-5));
		endedJob.EndDate = now.AddDays(-1); // Ended yesterday

		// Job within valid date range
		var validJob = CreateRecurringCronJob(
			isEnabled: true,
			nextRunUtc: now.AddMinutes(-5));
		validJob.StartDate = now.AddDays(-7);
		validJob.EndDate = now.AddDays(7);

		await store.AddJobAsync(notStartedJob, CancellationToken.None).ConfigureAwait(false);
		await store.AddJobAsync(endedJob, CancellationToken.None).ConfigureAwait(false);
		await store.AddJobAsync(validJob, CancellationToken.None).ConfigureAwait(false);

		var dueJobs = await store.GetDueJobsAsync(now, CancellationToken.None).ConfigureAwait(false);
		var dueList = dueJobs.ToList();

		if (dueList.Any(j => j.Id == notStartedJob.Id))
		{
			throw new TestFixtureAssertionException(
				"Job with future StartDate should NOT be returned by GetDueJobsAsync");
		}

		if (dueList.Any(j => j.Id == endedJob.Id))
		{
			throw new TestFixtureAssertionException(
				"Job with past EndDate should NOT be returned by GetDueJobsAsync");
		}

		if (!dueList.Any(j => j.Id == validJob.Id))
		{
			throw new TestFixtureAssertionException(
				"Job within valid date range should be returned by GetDueJobsAsync");
		}
	}

	#endregion

	#region Tag Tests

	/// <summary>
	/// Verifies that GetJobsByTagAsync returns matching jobs (case-insensitive).
	/// </summary>
	public virtual async Task GetJobsByTagAsync_ShouldReturnMatchingJobs_CaseInsensitive()
	{
		var store = CreateStore();

		var taggedJob = CreateRecurringCronJob(tags: ["MyTag", "AnotherTag"]);
		var untaggedJob = CreateRecurringCronJob(tags: ["DifferentTag"]);

		await store.AddJobAsync(taggedJob, CancellationToken.None).ConfigureAwait(false);
		await store.AddJobAsync(untaggedJob, CancellationToken.None).ConfigureAwait(false);

		// Search with different case - should still match
		var results = await store.GetJobsByTagAsync("mytag", CancellationToken.None).ConfigureAwait(false);
		var resultsList = results.ToList();

		if (!resultsList.Any(j => j.Id == taggedJob.Id))
		{
			throw new TestFixtureAssertionException(
				"Job with matching tag (case-insensitive) should be returned");
		}

		if (resultsList.Any(j => j.Id == untaggedJob.Id))
		{
			throw new TestFixtureAssertionException(
				"Job without matching tag should NOT be returned");
		}
	}

	/// <summary>
	/// Verifies that GetJobsByTagAsync returns empty for no matches.
	/// </summary>
	public virtual async Task GetJobsByTagAsync_NoMatches_ShouldReturnEmpty()
	{
		var store = CreateStore();

		var job = CreateRecurringCronJob(tags: ["SomeTag"]);
		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var results = await store.GetJobsByTagAsync("NonExistentTag", CancellationToken.None).ConfigureAwait(false);

		if (results.Any())
		{
			throw new TestFixtureAssertionException(
				"GetJobsByTagAsync should return empty when no jobs match the tag");
		}
	}

	#endregion

	#region Enable/Disable Tests

	/// <summary>
	/// Verifies that SetJobEnabledAsync updates the job and returns true.
	/// </summary>
	public virtual async Task SetJobEnabledAsync_ShouldUpdateAndReturnTrue()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob(isEnabled: true);

		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var result = await store.SetJobEnabledAsync(job.Id, false, CancellationToken.None).ConfigureAwait(false);

		if (!result)
		{
			throw new TestFixtureAssertionException(
				"SetJobEnabledAsync should return true for existing job");
		}

		var retrieved = await store.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Job should remain in store after SetJobEnabledAsync");
		}

		if (retrieved.IsEnabled)
		{
			throw new TestFixtureAssertionException(
				"IsEnabled should be false after SetJobEnabledAsync(false)");
		}
	}

	/// <summary>
	/// Verifies that SetJobEnabledAsync returns false for non-existent job.
	/// </summary>
	public virtual async Task SetJobEnabledAsync_NonExistent_ShouldReturnFalse()
	{
		var store = CreateStore();
		var nonExistentId = GenerateJobId();

		var result = await store.SetJobEnabledAsync(nonExistentId, true, CancellationToken.None).ConfigureAwait(false);

		if (result)
		{
			throw new TestFixtureAssertionException(
				"SetJobEnabledAsync should return false for non-existent job");
		}
	}

	#endregion

	#region Execution Tests

	/// <summary>
	/// Verifies that RecordExecutionAsync updates stats and adds history on success.
	/// </summary>
	public virtual async Task RecordExecutionAsync_Success_ShouldUpdateStatsAndAddHistory()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob();

		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		await store.RecordExecutionAsync(job.Id, true, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Job should remain in store after RecordExecutionAsync");
		}

		if (retrieved.RunCount != 1)
		{
			throw new TestFixtureAssertionException(
				$"RunCount should be 1 after successful execution, got {retrieved.RunCount}");
		}

		if (retrieved.FailureCount != 0)
		{
			throw new TestFixtureAssertionException(
				$"FailureCount should be 0 after successful execution, got {retrieved.FailureCount}");
		}

		if (retrieved.LastRunUtc is null)
		{
			throw new TestFixtureAssertionException(
				"LastRunUtc should be set after RecordExecutionAsync");
		}

		// Check history was added
		var history = await store.GetJobHistoryAsync(job.Id, CancellationToken.None, 10).ConfigureAwait(false);
		var historyList = history.ToList();

		if (historyList.Count != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected 1 history entry, got {historyList.Count}");
		}

		if (!historyList[0].Success)
		{
			throw new TestFixtureAssertionException(
				"History entry should have Success=true");
		}
	}

	/// <summary>
	/// Verifies that RecordExecutionAsync increments failure count on failure.
	/// </summary>
	public virtual async Task RecordExecutionAsync_Failure_ShouldIncrementFailureCount()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob();

		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		await store.RecordExecutionAsync(job.Id, false, CancellationToken.None, "Test error message").ConfigureAwait(false);

		var retrieved = await store.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Job should remain in store after RecordExecutionAsync");
		}

		if (retrieved.RunCount != 1)
		{
			throw new TestFixtureAssertionException(
				$"RunCount should be 1 after failed execution, got {retrieved.RunCount}");
		}

		if (retrieved.FailureCount != 1)
		{
			throw new TestFixtureAssertionException(
				$"FailureCount should be 1 after failed execution, got {retrieved.FailureCount}");
		}

		if (retrieved.LastError != "Test error message")
		{
			throw new TestFixtureAssertionException(
				$"LastError should be 'Test error message', got '{retrieved.LastError}'");
		}

		// Check history was added with error
		var history = await store.GetJobHistoryAsync(job.Id, CancellationToken.None, 10).ConfigureAwait(false);
		var historyList = history.ToList();

		if (historyList.Count != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected 1 history entry, got {historyList.Count}");
		}

		if (historyList[0].Success)
		{
			throw new TestFixtureAssertionException(
				"History entry should have Success=false");
		}

		if (historyList[0].Error != "Test error message")
		{
			throw new TestFixtureAssertionException(
				$"History entry error should be 'Test error message', got '{historyList[0].Error}'");
		}
	}

	/// <summary>
	/// Verifies that GetJobHistoryAsync returns recent entries with limit.
	/// </summary>
	public virtual async Task GetJobHistoryAsync_ShouldReturnRecentWithLimit()
	{
		var store = CreateStore();
		var job = CreateRecurringCronJob();

		await store.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		// Record multiple executions
		for (var i = 0; i < 5; i++)
		{
			await store.RecordExecutionAsync(job.Id, true, CancellationToken.None).ConfigureAwait(false);
			await Task.Delay(10).ConfigureAwait(false); // Small delay to ensure different timestamps
		}

		// Request only 3 entries
		var history = await store.GetJobHistoryAsync(job.Id, CancellationToken.None, 3).ConfigureAwait(false);
		var historyList = history.ToList();

		if (historyList.Count != 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected 3 history entries with limit=3, got {historyList.Count}");
		}

		// Verify they are ordered by StartedUtc DESC (most recent first)
		for (var i = 0; i < historyList.Count - 1; i++)
		{
			if (historyList[i].StartedUtc < historyList[i + 1].StartedUtc)
			{
				throw new TestFixtureAssertionException(
					"History entries should be ordered by StartedUtc descending (most recent first)");
			}
		}
	}

	#endregion
}
