// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Core;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Jobs.Tests.Quartz;

/// <summary>
/// Unit tests for AsyncFactoryHostedService.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class AsyncFactoryHostedServiceShould
{
	[Fact]
	public async Task StartAsync_CreatesAndStartsInnerService()
	{
		// Arrange
		var innerService = A.Fake<IJobConfigHostedWatcherService>();
		var factory = A.Fake<IJobConfigHostedWatcherServiceFactory>();

#pragma warning disable CA2012
		A.CallTo(() => factory.CreateAsync<AsyncFactoryTestConfigJob, AsyncFactoryTestJobConfig>())
			.Returns(Task.FromResult(innerService));
#pragma warning restore CA2012

		var services = new ServiceCollection();
		services.AddSingleton(factory);
		var sp = services.BuildServiceProvider();

		// We need to use reflection since the class is internal
		var type = typeof(JobConfigHostedWatcherServiceFactory).Assembly
			.GetType("Excalibur.Jobs.Quartz.AsyncFactoryHostedService`2")!
			.MakeGenericType(typeof(AsyncFactoryTestConfigJob), typeof(AsyncFactoryTestJobConfig));

		var service = (Microsoft.Extensions.Hosting.IHostedService)Activator.CreateInstance(type, sp)!;

		// Act
		await service.StartAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => innerService.StartAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StopAsync_DoesNothing_WhenInnerServiceNotCreated()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IJobConfigHostedWatcherServiceFactory>());
		var sp = services.BuildServiceProvider();

		var type = typeof(JobConfigHostedWatcherServiceFactory).Assembly
			.GetType("Excalibur.Jobs.Quartz.AsyncFactoryHostedService`2")!
			.MakeGenericType(typeof(AsyncFactoryTestConfigJob), typeof(AsyncFactoryTestJobConfig));

		var service = (Microsoft.Extensions.Hosting.IHostedService)Activator.CreateInstance(type, sp)!;

		// Act — StopAsync without StartAsync should not throw
		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StopAsync_StopsInnerService_WhenStarted()
	{
		// Arrange
		var innerService = A.Fake<IJobConfigHostedWatcherService>();
		var factory = A.Fake<IJobConfigHostedWatcherServiceFactory>();

#pragma warning disable CA2012
		A.CallTo(() => factory.CreateAsync<AsyncFactoryTestConfigJob, AsyncFactoryTestJobConfig>())
			.Returns(Task.FromResult(innerService));
#pragma warning restore CA2012

		var services = new ServiceCollection();
		services.AddSingleton(factory);
		var sp = services.BuildServiceProvider();

		var type = typeof(JobConfigHostedWatcherServiceFactory).Assembly
			.GetType("Excalibur.Jobs.Quartz.AsyncFactoryHostedService`2")!
			.MakeGenericType(typeof(AsyncFactoryTestConfigJob), typeof(AsyncFactoryTestJobConfig));

		var service = (Microsoft.Extensions.Hosting.IHostedService)Activator.CreateInstance(type, sp)!;

		await service.StartAsync(CancellationToken.None);

		// Act
		await service.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => innerService.StopAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}
}

internal sealed class AsyncFactoryTestJobConfig : JobConfig
{
}

internal sealed class AsyncFactoryTestConfigJob : IConfigurableJob<AsyncFactoryTestJobConfig>
{
	public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
