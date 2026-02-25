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
/// Unit tests for <see cref="JobConfigHostedWatcherServiceFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class JobConfigHostedWatcherServiceFactoryShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenServiceProviderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new JobConfigHostedWatcherServiceFactory(null!));
	}

	[Fact]
	public async Task CreateAsync_ReturnsJobConfigHostedWatcherService()
	{
		// Arrange
		var scheduler = A.Fake<IScheduler>();
		var schedulerFactory = A.Fake<ISchedulerFactory>();
		A.CallTo(() => schedulerFactory.GetScheduler(A<CancellationToken>._))
			.Returns(Task.FromResult(scheduler));

		var config = new FactoryTestJobConfig { Disabled = false, JobName = "FactoryTest", JobGroup = "TestGroup" };

		var services = new ServiceCollection();
		services.AddSingleton(schedulerFactory);
		services.AddSingleton<IOptionsMonitor<FactoryTestJobConfig>>(
			new FactoryTestOptionsMonitorWrapper<FactoryTestJobConfig>(config));
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		var factory = new JobConfigHostedWatcherServiceFactory(sp);

		// Act
		var result = await factory.CreateAsync<FactoryTestJob, FactoryTestJobConfig>();

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeOfType<JobConfigHostedWatcherService<FactoryTestJob, FactoryTestJobConfig>>();
	}

	[Fact]
	public async Task CreateAsync_ThrowsInvalidOperationException_WhenSchedulerFactoryMissing()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		var factory = new JobConfigHostedWatcherServiceFactory(sp);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => factory.CreateAsync<FactoryTestJob, FactoryTestJobConfig>());
	}
}

internal sealed class FactoryTestJobConfig : JobConfig
{
}

internal sealed class FactoryTestJob : IConfigurableJob<FactoryTestJobConfig>
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
