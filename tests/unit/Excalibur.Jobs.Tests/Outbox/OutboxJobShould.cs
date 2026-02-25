// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Jobs.Core;
using Excalibur.Jobs.Outbox;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Quartz;

namespace Excalibur.Jobs.Tests.Outbox;

/// <summary>
/// Unit tests for <see cref="OutboxJob"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class OutboxJobShould
{
	private readonly IOutboxDispatcher _fakeOutbox;
	private readonly JobHeartbeatTracker _heartbeatTracker;

	public OutboxJobShould()
	{
		_fakeOutbox = A.Fake<IOutboxDispatcher>();
		_heartbeatTracker = new JobHeartbeatTracker();
	}

	// --- Constructor null guards ---

	[Fact]
	public void ThrowWhenOutboxIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new OutboxJob(null!, _heartbeatTracker, NullLogger<OutboxJob>.Instance));
	}

	[Fact]
	public void ThrowWhenHeartbeatTrackerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new OutboxJob(_fakeOutbox, null!, NullLogger<OutboxJob>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new OutboxJob(_fakeOutbox, _heartbeatTracker, null!));
	}

	// --- Execute ---

	[Fact]
	public async Task ExecuteThrowWhenContextIsNull()
	{
		// Arrange
		var sut = new OutboxJob(_fakeOutbox, _heartbeatTracker, NullLogger<OutboxJob>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.Execute(null!));
	}

	[Fact]
	public async Task ExecuteCallRunOutboxDispatch()
	{
		// Arrange
		var sut = new OutboxJob(_fakeOutbox, _heartbeatTracker, NullLogger<OutboxJob>.Instance);
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(3);

		var context = CreateJobExecutionContext("OutboxJob", "DEFAULT");

		// Act
		await sut.Execute(context);

		// Assert
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteRecordHeartbeatOnSuccess()
	{
		// Arrange
		var sut = new OutboxJob(_fakeOutbox, _heartbeatTracker, NullLogger<OutboxJob>.Instance);
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(1);

		var context = CreateJobExecutionContext("OutboxJob", "DEFAULT");

		// Act
		await sut.Execute(context);

		// Assert — heartbeat should be recorded
		_heartbeatTracker.GetLastHeartbeat("OutboxJob").ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecuteSwallowExceptionAndLog()
	{
		// Arrange
		var sut = new OutboxJob(_fakeOutbox, _heartbeatTracker, NullLogger<OutboxJob>.Instance);
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Outbox failure"));

		var context = CreateJobExecutionContext("OutboxJob", "DEFAULT");

		// Act & Assert — Quartz jobs swallow exceptions
		await Should.NotThrowAsync(() => sut.Execute(context));
	}

	// --- JobConfigSectionName ---

	[Fact]
	public void HaveCorrectJobConfigSectionName()
	{
		// Assert
		OutboxJob.JobConfigSectionName.ShouldBe("Jobs:OutboxJob");
	}

	// --- Helpers ---

	private static IJobExecutionContext CreateJobExecutionContext(string jobName, string group)
	{
		var fakeContext = A.Fake<IJobExecutionContext>();

		var jobDetail = JobBuilder.Create<OutboxJob>()
			.WithIdentity(jobName, group)
			.Build();

		A.CallTo(() => fakeContext.JobDetail).Returns(jobDetail);
		A.CallTo(() => fakeContext.CancellationToken).Returns(CancellationToken.None);

		return fakeContext;
	}
}
