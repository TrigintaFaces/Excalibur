// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryCronJobStore"/> validating ICronJobStore contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryCronJobStore uses an instance-level ConcurrentDictionary with no static state,
/// so no special isolation is required beyond using fresh store instances.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>AddJobAsync THROWS InvalidOperationException on duplicate (not upsert)</description></item>
/// <item><description>UpdateJobAsync is UPSERT - creates if not exists</description></item>
/// <item><description>RemoveJobAsync cascades to remove execution history</description></item>
/// <item><description>GetDueJobsAsync applies complex filter (enabled + NextRunUtc + ShouldRunAt)</description></item>
/// <item><description>GetJobsByTagAsync is case-insensitive</description></item>
/// <item><description>RecordExecutionAsync updates both job stats AND execution history</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public class InMemoryCronJobStoreConformanceTests : CronJobStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override ICronJobStore CreateStore() => new InMemoryCronJobStore();

	#region Add Tests

	[Fact]
	public Task AddJobAsync_ShouldPersistJob_Test() =>
		AddJobAsync_ShouldPersistJob();

	[Fact]
	public Task AddJobAsync_WithNullJob_ShouldThrow_Test() =>
		AddJobAsync_WithNullJob_ShouldThrow();

	[Fact]
	public Task AddJobAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() =>
		AddJobAsync_DuplicateId_ShouldThrowInvalidOperationException();

	#endregion Add Tests

	#region Update Tests

	[Fact]
	public Task UpdateJobAsync_ShouldModifyJob_Test() =>
		UpdateJobAsync_ShouldModifyJob();

	[Fact]
	public Task UpdateJobAsync_ShouldSetLastModifiedUtc_Test() =>
		UpdateJobAsync_ShouldSetLastModifiedUtc();

	[Fact]
	public Task UpdateJobAsync_NonExistent_ShouldUpsert_Test() =>
		UpdateJobAsync_NonExistent_ShouldUpsert();

	#endregion Update Tests

	#region Remove Tests

	[Fact]
	public Task RemoveJobAsync_ShouldReturnTrueAndRemove_Test() =>
		RemoveJobAsync_ShouldReturnTrueAndRemove();

	[Fact]
	public Task RemoveJobAsync_NonExistent_ShouldReturnFalse_Test() =>
		RemoveJobAsync_NonExistent_ShouldReturnFalse();

	[Fact]
	public Task RemoveJobAsync_ShouldAlsoRemoveHistory_Test() =>
		RemoveJobAsync_ShouldAlsoRemoveHistory();

	#endregion Remove Tests

	#region Retrieval Tests

	[Fact]
	public Task GetJobAsync_ExistingJob_ShouldReturnJob_Test() =>
		GetJobAsync_ExistingJob_ShouldReturnJob();

	[Fact]
	public Task GetJobAsync_NonExistent_ShouldReturnNull_Test() =>
		GetJobAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task GetActiveJobsAsync_ShouldReturnOnlyEnabled_Test() =>
		GetActiveJobsAsync_ShouldReturnOnlyEnabled();

	#endregion Retrieval Tests

	#region Due Jobs Tests

	[Fact]
	public Task GetDueJobsAsync_ShouldReturnDueJobs_Test() =>
		GetDueJobsAsync_ShouldReturnDueJobs();

	[Fact]
	public Task GetDueJobsAsync_ShouldExcludeDisabled_Test() =>
		GetDueJobsAsync_ShouldExcludeDisabled();

	[Fact]
	public Task GetDueJobsAsync_ShouldRespectStartEndDates_Test() =>
		GetDueJobsAsync_ShouldRespectStartEndDates();

	#endregion Due Jobs Tests

	#region Tag Tests

	[Fact]
	public Task GetJobsByTagAsync_ShouldReturnMatchingJobs_CaseInsensitive_Test() =>
		GetJobsByTagAsync_ShouldReturnMatchingJobs_CaseInsensitive();

	[Fact]
	public Task GetJobsByTagAsync_NoMatches_ShouldReturnEmpty_Test() =>
		GetJobsByTagAsync_NoMatches_ShouldReturnEmpty();

	#endregion Tag Tests

	#region Enable/Disable Tests

	[Fact]
	public Task SetJobEnabledAsync_ShouldUpdateAndReturnTrue_Test() =>
		SetJobEnabledAsync_ShouldUpdateAndReturnTrue();

	[Fact]
	public Task SetJobEnabledAsync_NonExistent_ShouldReturnFalse_Test() =>
		SetJobEnabledAsync_NonExistent_ShouldReturnFalse();

	#endregion Enable/Disable Tests

	#region Execution Tests

	[Fact]
	public Task RecordExecutionAsync_Success_ShouldUpdateStatsAndAddHistory_Test() =>
		RecordExecutionAsync_Success_ShouldUpdateStatsAndAddHistory();

	[Fact]
	public Task RecordExecutionAsync_Failure_ShouldIncrementFailureCount_Test() =>
		RecordExecutionAsync_Failure_ShouldIncrementFailureCount();

	[Fact]
	public Task GetJobHistoryAsync_ShouldReturnRecentWithLimit_Test() =>
		GetJobHistoryAsync_ShouldReturnRecentWithLimit();

	#endregion Execution Tests
}
