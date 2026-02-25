// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Scheduling;

/// <summary>
///     Tests for the <see cref="InMemoryCronJobStore" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryCronJobStoreShould
{
	private readonly InMemoryCronJobStore _sut = new();

	private static RecurringCronJob CreateJob(string? id = null) => new()
	{
		Id = id ?? Guid.NewGuid().ToString(),
		Name = "TestJob",
		CronExpression = "0 * * * *",
		MessageTypeName = "TestMessage",
		MessagePayload = "{}",
		IsEnabled = true,
		NextRunUtc = DateTimeOffset.UtcNow.AddMinutes(5),
	};

	[Fact]
	public async Task AddJob()
	{
		var job = CreateJob();
		await _sut.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await _sut.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);
		retrieved.ShouldNotBeNull();
		retrieved.Name.ShouldBe("TestJob");
	}

	[Fact]
	public async Task ThrowWhenAddingDuplicateJobId()
	{
		var job = CreateJob("dup-id");
		await _sut.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.AddJobAsync(CreateJob("dup-id"), CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForNullJobOnAdd()
	{
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.AddJobAsync(null!, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task UpdateJob()
	{
		var job = CreateJob();
		await _sut.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		job.Name = "UpdatedJob";
		await _sut.UpdateJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await _sut.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);
		retrieved!.Name.ShouldBe("UpdatedJob");
		retrieved.LastModifiedUtc.ShouldNotBeNull();
	}

	[Fact]
	public async Task RemoveJob()
	{
		var job = CreateJob();
		await _sut.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var removed = await _sut.RemoveJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);

		removed.ShouldBeTrue();
		var retrieved = await _sut.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);
		retrieved.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnFalseWhenRemovingNonexistentJob()
	{
		var removed = await _sut.RemoveJobAsync("nonexistent", CancellationToken.None).ConfigureAwait(false);

		removed.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnNullForNonexistentJob()
	{
		var result = await _sut.GetJobAsync("nonexistent", CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetActiveJobs()
	{
		var enabledJob = CreateJob();
		enabledJob.IsEnabled = true;

		var disabledJob = CreateJob();
		disabledJob.IsEnabled = false;

		await _sut.AddJobAsync(enabledJob, CancellationToken.None).ConfigureAwait(false);
		await _sut.AddJobAsync(disabledJob, CancellationToken.None).ConfigureAwait(false);

		var active = (await _sut.GetActiveJobsAsync(CancellationToken.None).ConfigureAwait(false)).ToList();

		active.Count.ShouldBe(1);
		active[0].Id.ShouldBe(enabledJob.Id);
	}

	[Fact]
	public async Task GetDueJobs()
	{
		var dueJob = CreateJob();
		dueJob.NextRunUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
		dueJob.IsEnabled = true;

		var futureJob = CreateJob();
		futureJob.NextRunUtc = DateTimeOffset.UtcNow.AddHours(1);
		futureJob.IsEnabled = true;

		await _sut.AddJobAsync(dueJob, CancellationToken.None).ConfigureAwait(false);
		await _sut.AddJobAsync(futureJob, CancellationToken.None).ConfigureAwait(false);

		var due = (await _sut.GetDueJobsAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false)).ToList();

		due.Count.ShouldBe(1);
		due[0].Id.ShouldBe(dueJob.Id);
	}

	[Fact]
	public async Task GetJobsByTag()
	{
		var taggedJob = CreateJob();
		taggedJob.Tags = ["important"];

		var untaggedJob = CreateJob();

		await _sut.AddJobAsync(taggedJob, CancellationToken.None).ConfigureAwait(false);
		await _sut.AddJobAsync(untaggedJob, CancellationToken.None).ConfigureAwait(false);

		var tagged = (await _sut.GetJobsByTagAsync("important", CancellationToken.None).ConfigureAwait(false)).ToList();

		tagged.Count.ShouldBe(1);
		tagged[0].Id.ShouldBe(taggedJob.Id);
	}

	[Fact]
	public async Task UpdateNextRunTime()
	{
		var job = CreateJob();
		await _sut.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var newTime = DateTimeOffset.UtcNow.AddHours(2);
		await _sut.UpdateNextRunTimeAsync(job.Id, newTime, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await _sut.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);
		retrieved!.NextRunUtc.ShouldBe(newTime);
	}

	[Fact]
	public async Task RecordSuccessfulExecution()
	{
		var job = CreateJob();
		await _sut.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		await _sut.RecordExecutionAsync(job.Id, success: true, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await _sut.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);
		retrieved!.RunCount.ShouldBe(1);
		retrieved.FailureCount.ShouldBe(0);
		retrieved.LastError.ShouldBeNull();
	}

	[Fact]
	public async Task RecordFailedExecution()
	{
		var job = CreateJob();
		await _sut.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		await _sut.RecordExecutionAsync(job.Id, success: false, CancellationToken.None, errorMessage: "timeout").ConfigureAwait(false);

		var retrieved = await _sut.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);
		retrieved!.RunCount.ShouldBe(1);
		retrieved.FailureCount.ShouldBe(1);
		retrieved.LastError.ShouldBe("timeout");
	}

	[Fact]
	public async Task SetJobEnabled()
	{
		var job = CreateJob();
		job.IsEnabled = true;
		await _sut.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		var result = await _sut.SetJobEnabledAsync(job.Id, false, CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeTrue();
		var retrieved = await _sut.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);
		retrieved!.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnFalseWhenSettingEnabledForNonexistentJob()
	{
		var result = await _sut.SetJobEnabledAsync("nonexistent", true, CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task GetJobHistory()
	{
		var job = CreateJob();
		await _sut.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);

		await _sut.RecordExecutionAsync(job.Id, true, CancellationToken.None).ConfigureAwait(false);
		await _sut.RecordExecutionAsync(job.Id, false, CancellationToken.None, errorMessage: "error").ConfigureAwait(false);

		var history = (await _sut.GetJobHistoryAsync(job.Id, CancellationToken.None).ConfigureAwait(false)).ToList();

		history.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ReturnEmptyHistoryForNonexistentJob()
	{
		var history = (await _sut.GetJobHistoryAsync("nonexistent", CancellationToken.None).ConfigureAwait(false)).ToList();

		history.ShouldBeEmpty();
	}

	[Fact]
	public async Task ClearAllJobsAndHistory()
	{
		var job = CreateJob();
		await _sut.AddJobAsync(job, CancellationToken.None).ConfigureAwait(false);
		await _sut.RecordExecutionAsync(job.Id, true, CancellationToken.None).ConfigureAwait(false);

		_sut.Clear();

		var retrieved = await _sut.GetJobAsync(job.Id, CancellationToken.None).ConfigureAwait(false);
		retrieved.ShouldBeNull();
	}

	[Fact]
	public void ImplementICronJobStore()
	{
		_sut.ShouldBeAssignableTo<ICronJobStore>();
	}
}
