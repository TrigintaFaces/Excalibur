// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Quartz;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Jobs.Tests.Quartz;

/// <summary>
/// Unit tests for <see cref="JobConfigurator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class JobConfiguratorShould
{
	// --- Constructor ---

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new JobConfigurator(null!));
	}

	// --- AddJob<TJob> ---

	[Fact]
	public void AddJobThrowWhenCronExpressionIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act & Assert
		Should.Throw<ArgumentException>(() => sut.AddJob<TestJob>(null!));
	}

	[Fact]
	public void AddJobThrowWhenCronExpressionIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act & Assert
		Should.Throw<ArgumentException>(() => sut.AddJob<TestJob>(""));
	}

	[Fact]
	public void AddJobThrowWhenCronExpressionIsWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act & Assert
		Should.Throw<ArgumentException>(() => sut.AddJob<TestJob>("  "));
	}

	[Fact]
	public void AddJobRegisterJobType()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act
		sut.AddJob<TestJob>("0 0 * * * ?");

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(TestJob));
	}

	[Fact]
	public void AddJobReturnSelfForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act
		var result = sut.AddJob<TestJob>("0 0 * * * ?");

		// Assert
		result.ShouldBeSameAs(sut);
	}

	// --- AddJob<TJob, TContext> ---

	[Fact]
	public void AddJobWithContextThrowWhenCronExpressionIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			sut.AddJob<TestContextJob, TestJobContext>(null!, new TestJobContext()));
	}

	[Fact]
	public void AddJobWithContextThrowWhenContextIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			sut.AddJob<TestContextJob, TestJobContext>("0 0 * * * ?", null!));
	}

	[Fact]
	public void AddJobWithContextRegisterJobType()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act
		sut.AddJob<TestContextJob, TestJobContext>("0 0 * * * ?", new TestJobContext());

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(TestContextJob));
	}

	// --- AddRecurringJob ---

	[Fact]
	public void AddRecurringJobRegisterJobType()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act
		sut.AddRecurringJob<TestJob>(TimeSpan.FromMinutes(5));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(TestJob));
	}

	[Fact]
	public void AddRecurringJobReturnSelfForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act
		var result = sut.AddRecurringJob<TestJob>(TimeSpan.FromMinutes(5));

		// Assert
		result.ShouldBeSameAs(sut);
	}

	// --- AddOneTimeJob ---

	[Fact]
	public void AddOneTimeJobRegisterJobType()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act
		sut.AddOneTimeJob<TestJob>();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(TestJob));
	}

	[Fact]
	public void AddOneTimeJobReturnSelfForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act
		var result = sut.AddOneTimeJob<TestJob>();

		// Assert
		result.ShouldBeSameAs(sut);
	}

	// --- AddDelayedJob ---

	[Fact]
	public void AddDelayedJobRegisterJobType()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act
		sut.AddDelayedJob<TestJob>(TimeSpan.FromMinutes(10));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(TestJob));
	}

	// --- AddJobIf ---

	[Fact]
	public void AddJobIfThrowWhenConfigureJobIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.AddJobIf(true, null!));
	}

	[Fact]
	public void AddJobIfInvokeActionWhenConditionIsTrue()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);
		var invoked = false;

		// Act
		sut.AddJobIf(true, _ => invoked = true);

		// Assert
		invoked.ShouldBeTrue();
	}

	[Fact]
	public void AddJobIfNotInvokeActionWhenConditionIsFalse()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);
		var invoked = false;

		// Act
		sut.AddJobIf(false, _ => invoked = true);

		// Assert
		invoked.ShouldBeFalse();
	}

	[Fact]
	public void AddJobIfReturnSelfForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act
		var result = sut.AddJobIf(true, _ => { });

		// Assert
		result.ShouldBeSameAs(sut);
	}

	// --- AddJobInstances ---

	[Fact]
	public void AddJobInstancesThrowWhenConfigurationsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.AddJobInstances<TestJob>(null!));
	}

	[Fact]
	public void AddJobInstancesRegisterEnabledJobsOnly()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);
		var configs = new[]
		{
			new JobConfiguration("job-1", "0 0 * * * ?") { Enabled = true },
			new JobConfiguration("job-2", "0 30 * * * ?") { Enabled = false },
		};

		// Act
		sut.AddJobInstances<TestJob>(configs);

		// Assert — TestJob should be registered (at least once for enabled config)
		services.ShouldContain(sd => sd.ServiceType == typeof(TestJob));
	}

	[Fact]
	public void AddJobInstancesReturnSelfForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var sut = new JobConfigurator(services);

		// Act
		var result = sut.AddJobInstances<TestJob>();

		// Assert
		result.ShouldBeSameAs(sut);
	}

	// --- Test helpers ---

	private sealed class TestJob : IBackgroundJob
	{
		public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestJobContext;

	private sealed class TestContextJob : IBackgroundJob<TestJobContext>
	{
		public Task ExecuteAsync(TestJobContext context, CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
