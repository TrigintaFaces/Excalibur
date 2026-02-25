using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCronJobStoreShould
{
	private readonly InMemoryCronJobStore _store = new();
	private readonly CancellationToken _ct = CancellationToken.None;

	[Fact]
	public async Task AddJob_StoresAndRetrievesJob()
	{
		var job = new RecurringCronJob { CronExpression = "* * * * *" };

		await _store.AddJobAsync(job, _ct);

		var retrieved = await _store.GetJobAsync(job.Id, _ct);
		retrieved.ShouldNotBeNull();
		retrieved.CronExpression.ShouldBe("* * * * *");
	}

	[Fact]
	public async Task AddJob_ThrowsOnDuplicateId()
	{
		var job = new RecurringCronJob();
		await _store.AddJobAsync(job, _ct);

		await Should.ThrowAsync<InvalidOperationException>(
			() => _store.AddJobAsync(job, _ct));
	}

	[Fact]
	public async Task AddJob_ThrowsOnNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _store.AddJobAsync(null!, _ct));
	}

	[Fact]
	public async Task UpdateJob_UpdatesExistingJob()
	{
		var job = new RecurringCronJob { CronExpression = "0 * * * *" };
		await _store.AddJobAsync(job, _ct);

		job.CronExpression = "0 0 * * *";
		await _store.UpdateJobAsync(job, _ct);

		var retrieved = await _store.GetJobAsync(job.Id, _ct);
		retrieved!.CronExpression.ShouldBe("0 0 * * *");
		retrieved.LastModifiedUtc.ShouldNotBeNull();
	}

	[Fact]
	public async Task RemoveJob_RemovesExistingJob()
	{
		var job = new RecurringCronJob();
		await _store.AddJobAsync(job, _ct);

		var removed = await _store.RemoveJobAsync(job.Id, _ct);

		removed.ShouldBeTrue();
		(await _store.GetJobAsync(job.Id, _ct)).ShouldBeNull();
	}

	[Fact]
	public async Task RemoveJob_ReturnsFalseForNonexistent()
	{
		var removed = await _store.RemoveJobAsync("nonexistent", _ct);

		removed.ShouldBeFalse();
	}

	[Fact]
	public async Task GetJobAsync_ReturnsNullForNonexistent()
	{
		var job = await _store.GetJobAsync("missing", _ct);

		job.ShouldBeNull();
	}

	[Fact]
	public async Task GetActiveJobs_ReturnsOnlyEnabledJobs()
	{
		await _store.AddJobAsync(new RecurringCronJob { IsEnabled = true }, _ct);
		await _store.AddJobAsync(new RecurringCronJob { IsEnabled = false }, _ct);
		await _store.AddJobAsync(new RecurringCronJob { IsEnabled = true }, _ct);

		var active = (await _store.GetActiveJobsAsync(_ct)).ToList();

		active.Count.ShouldBe(2);
		active.ShouldAllBe(j => j.IsEnabled);
	}

	[Fact]
	public async Task GetDueJobs_ReturnsJobsDueBeforeCutoff()
	{
		var now = DateTimeOffset.UtcNow;

		var dueJob = new RecurringCronJob
		{
			IsEnabled = true,
			NextRunUtc = now.AddMinutes(-5),
			StartDate = now.AddDays(-1),
			EndDate = now.AddDays(1),
		};
		var futureJob = new RecurringCronJob
		{
			IsEnabled = true,
			NextRunUtc = now.AddHours(1),
		};

		await _store.AddJobAsync(dueJob, _ct);
		await _store.AddJobAsync(futureJob, _ct);

		var due = (await _store.GetDueJobsAsync(now, _ct)).ToList();

		due.Count.ShouldBe(1);
		due[0].Id.ShouldBe(dueJob.Id);
	}

	[Fact]
	public async Task GetJobsByTag_ReturnsMatchingJobs()
	{
		var job1 = new RecurringCronJob();
		job1.Tags.Add("billing");
		var job2 = new RecurringCronJob();
		job2.Tags.Add("notifications");

		await _store.AddJobAsync(job1, _ct);
		await _store.AddJobAsync(job2, _ct);

		var tagged = (await _store.GetJobsByTagAsync("billing", _ct)).ToList();

		tagged.Count.ShouldBe(1);
		tagged[0].Id.ShouldBe(job1.Id);
	}

	[Fact]
	public async Task UpdateNextRunTime_UpdatesRunTime()
	{
		var job = new RecurringCronJob();
		await _store.AddJobAsync(job, _ct);

		var nextRun = DateTimeOffset.UtcNow.AddHours(1);
		await _store.UpdateNextRunTimeAsync(job.Id, nextRun, _ct);

		var retrieved = await _store.GetJobAsync(job.Id, _ct);
		retrieved!.NextRunUtc.ShouldBe(nextRun);
	}

	[Fact]
	public async Task RecordExecution_TracksSuccessHistory()
	{
		var job = new RecurringCronJob();
		await _store.AddJobAsync(job, _ct);

		await _store.RecordExecutionAsync(job.Id, true, _ct);

		var history = (await _store.GetJobHistoryAsync(job.Id, _ct)).ToList();
		history.Count.ShouldBe(1);
		history[0].Success.ShouldBeTrue();
	}

	[Fact]
	public async Task RecordExecution_TracksFailureHistory()
	{
		var job = new RecurringCronJob();
		await _store.AddJobAsync(job, _ct);

		await _store.RecordExecutionAsync(job.Id, false, _ct, "timeout");

		var history = (await _store.GetJobHistoryAsync(job.Id, _ct)).ToList();
		history.Count.ShouldBe(1);
		history[0].Success.ShouldBeFalse();
		history[0].Error.ShouldBe("timeout");
	}

	[Fact]
	public async Task SetJobEnabled_TogglesJobState()
	{
		var job = new RecurringCronJob { IsEnabled = true };
		await _store.AddJobAsync(job, _ct);

		var result = await _store.SetJobEnabledAsync(job.Id, false, _ct);

		result.ShouldBeTrue();
		var retrieved = await _store.GetJobAsync(job.Id, _ct);
		retrieved!.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public async Task SetJobEnabled_ReturnsFalseForNonexistent()
	{
		var result = await _store.SetJobEnabledAsync("missing", true, _ct);

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task GetJobHistory_ReturnsEmptyForNoHistory()
	{
		var history = (await _store.GetJobHistoryAsync("missing", _ct)).ToList();

		history.ShouldBeEmpty();
	}

	[Fact]
	public async Task Clear_RemovesAllData()
	{
		var job = new RecurringCronJob();
		await _store.AddJobAsync(job, _ct);
		await _store.RecordExecutionAsync(job.Id, true, _ct);

		_store.Clear();

		(await _store.GetJobAsync(job.Id, _ct)).ShouldBeNull();
		(await _store.GetJobHistoryAsync(job.Id, _ct)).ShouldBeEmpty();
	}
}
