// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Quartz;

using FakeItEasy;

namespace Excalibur.Jobs.Tests.Quartz;

/// <summary>
/// Unit tests for <see cref="JobConfiguratorExtensions"/>.
/// Tests the extension methods extracted from IJobConfigurator in Sprint 590 (B.3).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class JobConfiguratorExtensionsShould
{
	private readonly IJobConfigurator _configurator = A.Fake<IJobConfigurator>();

	// --- AddRecurringJob null guard ---

	[Fact]
	public void AddRecurringJob_ThrowWhenConfiguratorIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => JobConfiguratorExtensions.AddRecurringJob<TestJob>(null!, TimeSpan.FromMinutes(5)));
	}

	// --- AddRecurringJob interval-to-cron: seconds ---

	[Fact]
	public void AddRecurringJob_WithSecondsInterval_DelegatesToAddJobWithSecondsCron()
	{
		// Arrange
		A.CallTo(() => _configurator.AddJob<TestJob>(A<string>._, A<string?>._))
			.Returns(_configurator);

		// Act
		_configurator.AddRecurringJob<TestJob>(TimeSpan.FromSeconds(30), "sec-job");

		// Assert
		A.CallTo(() => _configurator.AddJob<TestJob>("*/30 * * * * ?", "sec-job"))
			.MustHaveHappenedOnceExactly();
	}

	// --- AddRecurringJob interval-to-cron: minutes ---

	[Fact]
	public void AddRecurringJob_WithMinutesInterval_DelegatesToAddJobWithMinutesCron()
	{
		// Arrange
		A.CallTo(() => _configurator.AddJob<TestJob>(A<string>._, A<string?>._))
			.Returns(_configurator);

		// Act
		_configurator.AddRecurringJob<TestJob>(TimeSpan.FromMinutes(15));

		// Assert
		A.CallTo(() => _configurator.AddJob<TestJob>("0 */15 * * * ?", null))
			.MustHaveHappenedOnceExactly();
	}

	// --- AddRecurringJob interval-to-cron: hours ---

	[Fact]
	public void AddRecurringJob_WithHoursInterval_DelegatesToAddJobWithHoursCron()
	{
		// Arrange
		A.CallTo(() => _configurator.AddJob<TestJob>(A<string>._, A<string?>._))
			.Returns(_configurator);

		// Act
		_configurator.AddRecurringJob<TestJob>(TimeSpan.FromHours(6), "hourly-job");

		// Assert
		A.CallTo(() => _configurator.AddJob<TestJob>("0 0 */6 * * ?", "hourly-job"))
			.MustHaveHappenedOnceExactly();
	}

	// --- AddRecurringJob interval-to-cron: days ---

	[Fact]
	public void AddRecurringJob_WithDaysInterval_DelegatesToAddJobWithDaysCron()
	{
		// Arrange
		A.CallTo(() => _configurator.AddJob<TestJob>(A<string>._, A<string?>._))
			.Returns(_configurator);

		// Act
		_configurator.AddRecurringJob<TestJob>(TimeSpan.FromDays(2));

		// Assert
		A.CallTo(() => _configurator.AddJob<TestJob>("0 0 0 */2 * ?", null))
			.MustHaveHappenedOnceExactly();
	}

	// --- AddRecurringJob interval-to-cron: boundary (exactly 60s = 1 min) ---

	[Fact]
	public void AddRecurringJob_WithExactly60Seconds_UsesMinutesCron()
	{
		// Arrange — 60 seconds is exactly 1 minute, should use minutes branch
		A.CallTo(() => _configurator.AddJob<TestJob>(A<string>._, A<string?>._))
			.Returns(_configurator);

		// Act
		_configurator.AddRecurringJob<TestJob>(TimeSpan.FromSeconds(60));

		// Assert — TotalSeconds == 60, TotalMinutes == 1, should go to minutes branch
		A.CallTo(() => _configurator.AddJob<TestJob>("0 */1 * * * ?", null))
			.MustHaveHappenedOnceExactly();
	}

	// --- AddRecurringJob interval-to-cron: boundary (exactly 60 min = 1 hour) ---

	[Fact]
	public void AddRecurringJob_WithExactly60Minutes_UsesHoursCron()
	{
		// Arrange
		A.CallTo(() => _configurator.AddJob<TestJob>(A<string>._, A<string?>._))
			.Returns(_configurator);

		// Act
		_configurator.AddRecurringJob<TestJob>(TimeSpan.FromMinutes(60));

		// Assert — TotalMinutes == 60, should go to hours branch
		A.CallTo(() => _configurator.AddJob<TestJob>("0 0 */1 * * ?", null))
			.MustHaveHappenedOnceExactly();
	}

	// --- AddJobIf null guards ---

	[Fact]
	public void AddJobIf_ThrowWhenConfiguratorIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => JobConfiguratorExtensions.AddJobIf(null!, true, _ => { }));
	}

	[Fact]
	public void AddJobIf_ThrowWhenConfigureJobIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => _configurator.AddJobIf(true, null!));
	}

	// --- AddJobIf conditional behavior ---

	[Fact]
	public void AddJobIf_WhenConditionTrue_InvokesAction()
	{
		// Arrange
		var invoked = false;

		// Act
		_configurator.AddJobIf(true, _ => invoked = true);

		// Assert
		invoked.ShouldBeTrue();
	}

	[Fact]
	public void AddJobIf_WhenConditionFalse_DoesNotInvokeAction()
	{
		// Arrange
		var invoked = false;

		// Act
		_configurator.AddJobIf(false, _ => invoked = true);

		// Assert
		invoked.ShouldBeFalse();
	}

	[Fact]
	public void AddJobIf_PassesConfiguratorToAction()
	{
		// Arrange
		IJobConfigurator? received = null;

		// Act
		_configurator.AddJobIf(true, c => received = c);

		// Assert
		received.ShouldBeSameAs(_configurator);
	}

	[Fact]
	public void AddJobIf_ReturnsSameConfigurator()
	{
		// Act
		var result = _configurator.AddJobIf(false, _ => { });

		// Assert
		result.ShouldBeSameAs(_configurator);
	}

	// --- AddJobInstances null guards ---

	[Fact]
	public void AddJobInstances_ThrowWhenConfiguratorIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => JobConfiguratorExtensions.AddJobInstances<TestJob>(null!, new JobConfiguration("k", "0 0 * * * ?")));
	}

	[Fact]
	public void AddJobInstances_ThrowWhenConfigurationsIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => _configurator.AddJobInstances<TestJob>(null!));
	}

	// --- AddJobInstances filtering ---

	[Fact]
	public void AddJobInstances_OnlyAddsEnabledConfigurations()
	{
		// Arrange
		A.CallTo(() => _configurator.AddJob<TestJob>(A<string>._, A<string?>._))
			.Returns(_configurator);

		var configs = new[]
		{
			new JobConfiguration("enabled-1", "0 0 * * * ?") { Enabled = true },
			new JobConfiguration("disabled-1", "0 30 * * * ?") { Enabled = false },
			new JobConfiguration("enabled-2", "0 15 * * * ?") { Enabled = true },
		};

		// Act
		_configurator.AddJobInstances<TestJob>(configs);

		// Assert — only 2 enabled configs should produce AddJob calls
		A.CallTo(() => _configurator.AddJob<TestJob>(A<string>._, A<string?>._))
			.MustHaveHappened(2, Times.Exactly);
		A.CallTo(() => _configurator.AddJob<TestJob>("0 0 * * * ?", "enabled-1"))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _configurator.AddJob<TestJob>("0 15 * * * ?", "enabled-2"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void AddJobInstances_WithEmptyArray_DoesNotCallAddJob()
	{
		// Act
		_configurator.AddJobInstances<TestJob>();

		// Assert
		A.CallTo(() => _configurator.AddJob<TestJob>(A<string>._, A<string?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void AddJobInstances_WithAllDisabled_DoesNotCallAddJob()
	{
		// Arrange
		var configs = new[]
		{
			new JobConfiguration("d1", "0 0 * * * ?") { Enabled = false },
			new JobConfiguration("d2", "0 30 * * * ?") { Enabled = false },
		};

		// Act
		_configurator.AddJobInstances<TestJob>(configs);

		// Assert
		A.CallTo(() => _configurator.AddJob<TestJob>(A<string>._, A<string?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void AddJobInstances_ReturnsSameConfigurator()
	{
		// Act
		var result = _configurator.AddJobInstances<TestJob>();

		// Assert
		result.ShouldBeSameAs(_configurator);
	}

	// --- Test helpers ---

	private sealed class TestJob : IBackgroundJob
	{
		public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
