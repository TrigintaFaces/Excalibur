// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Core;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Unit tests for <see cref="JobOptionsHostedWatcherServiceFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class JobOptionsHostedWatcherServiceFactoryShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenServiceProviderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new JobOptionsHostedWatcherServiceFactory(null!));
	}

	[Fact]
	public async Task CreateAsync_ReturnsJobOptionsHostedWatcherService()
	{
		// Arrange
		var scheduler = A.Fake<IScheduler>();
		var schedulerFactory = A.Fake<ISchedulerFactory>();
		A.CallTo(() => schedulerFactory.GetScheduler(A<CancellationToken>._))
			.Returns(Task.FromResult(scheduler));

		var config = new FactoryTestJobOptions { Disabled = false, JobName = "FactoryTest", JobGroup = "TestGroup" };

		var services = new ServiceCollection();
		services.AddSingleton(schedulerFactory);
		services.AddSingleton<IOptionsMonitor<FactoryTestJobOptions>>(
			new FactoryTestOptionsMonitorWrapper<FactoryTestJobOptions>(config));
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		var factory = new JobOptionsHostedWatcherServiceFactory(sp);

		// Act
		var result = await factory.CreateAsync<FactoryTestJob, FactoryTestJobOptions>();

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeOfType<JobOptionsHostedWatcherService<FactoryTestJob, FactoryTestJobOptions>>();
	}

	[Fact]
	public async Task CreateAsync_ThrowsInvalidOperationException_WhenSchedulerFactoryMissing()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		var factory = new JobOptionsHostedWatcherServiceFactory(sp);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => factory.CreateAsync<FactoryTestJob, FactoryTestJobOptions>());
	}
}

internal sealed class FactoryTestJobOptions : JobOptions
{
}

internal sealed class FactoryTestJob : IConfigurableJob<FactoryTestJobOptions>
{
	public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Simple IOptionsMonitor wrapper for testing.
/// </summary>
internal sealed class FactoryTestOptionsMonitorWrapper<T>(T value) : IOptionsMonitor<T>
{
	public T CurrentValue => value;

	public T Get(string? name) => value;

	public IDisposable? OnChange(Action<T, string?> listener) => null;
}
