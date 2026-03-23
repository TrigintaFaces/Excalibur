// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// ISP compliance tests for ICronJobStore interface.
/// Verifies interface shape, method semantics, and ISP gate compliance.
/// ICronJobStore currently has 11 methods -- the <=5 method gate requires splitting into:
/// - ICronJobStore (CRUD: Add, Update, Remove, Get) -- 4 methods
/// - ICronJobQuery (Active, Due, ByTag, History) -- 4 methods
/// - ICronJobExecution (UpdateNextRunTime, RecordExecution, SetJobEnabled) -- 3 methods
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CronJobStoreIspShould
{
	private static readonly BindingFlags DeclaredPublicInstance =
		BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

	#region Interface Shape

	[Fact]
	public void HaveElevenMethodsCurrently()
	{
		// ICronJobStore currently has 11 methods (violates <=5 gate)
		// This test documents the current state and will be updated after ISP split
		var methods = typeof(ICronJobStore).GetMethods(DeclaredPublicInstance);

		methods.Length.ShouldBe(11,
			"ICronJobStore should have exactly 11 methods (pre-ISP split)");
	}

	[Fact]
	public void HaveAddJobAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("AddJobAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have AddJobAsync");
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void HaveUpdateJobAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("UpdateJobAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have UpdateJobAsync");
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void HaveRemoveJobAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("RemoveJobAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have RemoveJobAsync");
		method.ReturnType.ShouldBe(typeof(Task<bool>));
	}

	[Fact]
	public void HaveGetJobAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("GetJobAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have GetJobAsync");
		method.ReturnType.ShouldBe(typeof(Task<RecurringCronJob?>));
	}

	[Fact]
	public void HaveGetActiveJobsAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("GetActiveJobsAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have GetActiveJobsAsync");
		method.ReturnType.ShouldBe(typeof(Task<IEnumerable<RecurringCronJob>>));
	}

	[Fact]
	public void HaveGetDueJobsAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("GetDueJobsAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have GetDueJobsAsync");
		method.ReturnType.ShouldBe(typeof(Task<IEnumerable<RecurringCronJob>>));
	}

	[Fact]
	public void HaveGetJobsByTagAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("GetJobsByTagAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have GetJobsByTagAsync");
		method.ReturnType.ShouldBe(typeof(Task<IEnumerable<RecurringCronJob>>));
	}

	[Fact]
	public void HaveUpdateNextRunTimeAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("UpdateNextRunTimeAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have UpdateNextRunTimeAsync");
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void HaveRecordExecutionAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("RecordExecutionAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have RecordExecutionAsync");
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void HaveSetJobEnabledAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("SetJobEnabledAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have SetJobEnabledAsync");
		method.ReturnType.ShouldBe(typeof(Task<bool>));
	}

	[Fact]
	public void HaveGetJobHistoryAsyncMethod()
	{
		var method = typeof(ICronJobStore).GetMethod("GetJobHistoryAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("ICronJobStore must have GetJobHistoryAsync");
		method.ReturnType.ShouldBe(typeof(Task<IEnumerable<JobExecutionHistory>>));
	}

	#endregion

	#region ISP Split Categorization

	[Fact]
	public void HaveFourCrudMethods()
	{
		// CRUD operations: Add, Update, Remove, Get
		var crudMethodNames = new[] { "AddJobAsync", "UpdateJobAsync", "RemoveJobAsync", "GetJobAsync" };

		var methods = typeof(ICronJobStore).GetMethods(DeclaredPublicInstance);
		var crudFound = methods.Where(m => crudMethodNames.Contains(m.Name)).ToList();

		crudFound.Count.ShouldBe(4,
			"ICronJobStore should have exactly 4 CRUD methods (Add, Update, Remove, Get)");
	}

	[Fact]
	public void HaveFourQueryMethods()
	{
		// Query operations: GetActiveJobs, GetDueJobs, GetJobsByTag, GetJobHistory
		var queryMethodNames = new[] { "GetActiveJobsAsync", "GetDueJobsAsync", "GetJobsByTagAsync", "GetJobHistoryAsync" };

		var methods = typeof(ICronJobStore).GetMethods(DeclaredPublicInstance);
		var queryFound = methods.Where(m => queryMethodNames.Contains(m.Name)).ToList();

		queryFound.Count.ShouldBe(4,
			"ICronJobStore should have exactly 4 query methods (GetActiveJobs, GetDueJobs, GetJobsByTag, GetJobHistory)");
	}

	[Fact]
	public void HaveThreeExecutionMethods()
	{
		// Execution operations: UpdateNextRunTime, RecordExecution, SetJobEnabled
		var executionMethodNames = new[] { "UpdateNextRunTimeAsync", "RecordExecutionAsync", "SetJobEnabledAsync" };

		var methods = typeof(ICronJobStore).GetMethods(DeclaredPublicInstance);
		var executionFound = methods.Where(m => executionMethodNames.Contains(m.Name)).ToList();

		executionFound.Count.ShouldBe(3,
			"ICronJobStore should have exactly 3 execution methods (UpdateNextRunTime, RecordExecution, SetJobEnabled)");
	}

	#endregion

	#region Call Semantics via Fake

	[Fact]
	public async Task AcceptAddJobWithRecurringCronJob()
	{
		// Arrange
		var store = A.Fake<ICronJobStore>();
		var job = new RecurringCronJob
		{
			Id = "test-job-1",
			CronExpression = "0 * * * *",
			MessageTypeName = "TestHandler"
		};

		// Act & Assert -- should not throw
		await store.AddJobAsync(job, CancellationToken.None);
		A.CallTo(() => store.AddJobAsync(job, A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AcceptGetJobById()
	{
		// Arrange
		var store = A.Fake<ICronJobStore>();
		A.CallTo(() => store.GetJobAsync("job-1", A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<RecurringCronJob?>(null));

		// Act
		var result = await store.GetJobAsync("job-1", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task AcceptRemoveJobById()
	{
		// Arrange
		var store = A.Fake<ICronJobStore>();
		A.CallTo(() => store.RemoveJobAsync("job-1", A<CancellationToken>.Ignored))
			.Returns(true);

		// Act
		var result = await store.RemoveJobAsync("job-1", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task AcceptGetDueJobsWithCutoff()
	{
		// Arrange
		var store = A.Fake<ICronJobStore>();
		var cutoff = DateTimeOffset.UtcNow;
		A.CallTo(() => store.GetDueJobsAsync(cutoff, A<CancellationToken>.Ignored))
			.Returns(Enumerable.Empty<RecurringCronJob>());

		// Act
		var result = await store.GetDueJobsAsync(cutoff, CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task AcceptRecordExecutionWithOptionalError()
	{
		// Arrange
		var store = A.Fake<ICronJobStore>();

		// Act -- success case
		await store.RecordExecutionAsync("job-1", true, CancellationToken.None);

		// Act -- failure case with error message
		await store.RecordExecutionAsync("job-1", false, CancellationToken.None, errorMessage: "timeout");

		// Assert
		A.CallTo(() => store.RecordExecutionAsync(
			"job-1", A<bool>.Ignored, A<CancellationToken>.Ignored, A<string?>.Ignored))
			.MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task AcceptSetJobEnabled()
	{
		// Arrange
		var store = A.Fake<ICronJobStore>();
		A.CallTo(() => store.SetJobEnabledAsync("job-1", false, A<CancellationToken>.Ignored))
			.Returns(true);

		// Act
		var result = await store.SetJobEnabledAsync("job-1", false, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task AcceptGetJobHistoryWithOptionalLimit()
	{
		// Arrange
		var store = A.Fake<ICronJobStore>();
		A.CallTo(() => store.GetJobHistoryAsync("job-1", A<CancellationToken>.Ignored, A<int>.Ignored))
			.Returns(Enumerable.Empty<JobExecutionHistory>());

		// Act -- with default limit
		var result = await store.GetJobHistoryAsync("job-1", CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion

	#region InMemoryCronJobStore Implementation Compliance

	[Fact]
	public void InMemoryImplementation_ShouldImplementInterface()
	{
		typeof(ICronJobStore).IsAssignableFrom(typeof(InMemoryCronJobStore)).ShouldBeTrue(
			"InMemoryCronJobStore must implement ICronJobStore");
	}

	[Fact]
	public void InMemoryImplementation_ShouldBeSealed()
	{
		typeof(InMemoryCronJobStore).IsSealed.ShouldBeTrue(
			"InMemoryCronJobStore should be sealed");
	}

	[Fact]
	public async Task InMemoryImplementation_RoundTripAddGetRemove()
	{
		// Arrange
		var store = new InMemoryCronJobStore();
		var job = new RecurringCronJob
		{
			Id = "roundtrip-1",
			CronExpression = "*/5 * * * *",
			MessageTypeName = "RoundTripHandler"
		};

		// Act -- Add
		await store.AddJobAsync(job, CancellationToken.None);

		// Assert -- Get
		var retrieved = await store.GetJobAsync("roundtrip-1", CancellationToken.None);
		retrieved.ShouldNotBeNull();
		retrieved.Id.ShouldBe("roundtrip-1");

		// Act -- Remove
		var removed = await store.RemoveJobAsync("roundtrip-1", CancellationToken.None);
		removed.ShouldBeTrue();

		// Assert -- Verify removed
		var afterRemove = await store.GetJobAsync("roundtrip-1", CancellationToken.None);
		afterRemove.ShouldBeNull();
	}

	[Fact]
	public async Task InMemoryImplementation_GetActiveJobsReturnsEnabledOnly()
	{
		// Arrange
		var store = new InMemoryCronJobStore();
		var enabledJob = new RecurringCronJob
		{
			Id = "enabled-1",
			CronExpression = "0 * * * *",
			MessageTypeName = "Handler",
			IsEnabled = true
		};
		var disabledJob = new RecurringCronJob
		{
			Id = "disabled-1",
			CronExpression = "0 * * * *",
			MessageTypeName = "Handler",
			IsEnabled = false
		};

		await store.AddJobAsync(enabledJob, CancellationToken.None);
		await store.AddJobAsync(disabledJob, CancellationToken.None);

		// Act
		var active = (await store.GetActiveJobsAsync(CancellationToken.None)).ToList();

		// Assert
		active.ShouldContain(j => j.Id == "enabled-1");
		active.ShouldNotContain(j => j.Id == "disabled-1");
	}

	#endregion
}
