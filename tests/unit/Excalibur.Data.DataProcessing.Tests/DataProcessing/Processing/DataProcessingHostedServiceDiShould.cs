// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Processing;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.DataProcessing.Processing;

/// <summary>
/// Integration tests verifying <see cref="DataProcessingHostedService"/> resolves
/// scoped <see cref="IDataOrchestrationManager"/> correctly through the real DI container.
/// This catches captive dependency issues that unit tests with mocks cannot detect.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class DataProcessingHostedServiceDiShould : UnitTestBase
{
	[Fact]
	public void ResolveHostedService_FromRealDiContainer()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddScoped<IDataOrchestrationManager, FakeOrchestrationManager>();
		services.EnableDataProcessingBackgroundService();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		// Act
		var hostedServices = sp.GetServices<IHostedService>().ToList();

		// Assert
		hostedServices.ShouldContain(s => s is DataProcessingHostedService);
	}

	[Fact]
	public async Task CreateFreshScope_PerPollingCycle()
	{
		// Arrange -- track how many distinct instances are resolved
		var resolvedInstances = new ConcurrentBag<int>();

		var services = new ServiceCollection();
		services.AddScoped<IDataOrchestrationManager>(_ =>
		{
			var mgr = new FakeOrchestrationManager();
			resolvedInstances.Add(mgr.GetHashCode());
			return mgr;
		});
		services.EnableDataProcessingBackgroundService(opts =>
		{
			opts.PollingInterval = TimeSpan.FromMilliseconds(50);
		});
		services.AddLogging();

		var sp = services.BuildServiceProvider();
		var hostedService = sp.GetServices<IHostedService>()
			.OfType<DataProcessingHostedService>()
			.Single();

		// Act -- run for enough cycles to get multiple scope creations
		await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => resolvedInstances.Count >= 3, TimeSpan.FromSeconds(10));
		await hostedService.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert -- each cycle should have resolved a distinct scoped instance
		resolvedInstances.Count.ShouldBeGreaterThanOrEqualTo(3);
		var uniqueInstances = resolvedInstances.ToHashSet();
		uniqueInstances.Count.ShouldBe(resolvedInstances.Count,
			"Each polling cycle should create a fresh scope with a new IDataOrchestrationManager instance");
	}

	[Fact]
	public async Task NotThrowInvalidOperationException_ForScopedDependency()
	{
		// Arrange -- register IDataOrchestrationManager as scoped (like production)
		var services = new ServiceCollection();
		services.AddScoped<IDataOrchestrationManager, FakeOrchestrationManager>();
		services.EnableDataProcessingBackgroundService(opts =>
		{
			opts.PollingInterval = TimeSpan.FromMilliseconds(50);
		});
		services.AddLogging();

		var sp = services.BuildServiceProvider();
		var hostedService = sp.GetServices<IHostedService>()
			.OfType<DataProcessingHostedService>()
			.Single();

		// Act & Assert -- should NOT throw InvalidOperationException about
		// "Cannot resolve scoped service from root provider"
		await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => hostedService.IsHealthy, TimeSpan.FromSeconds(5));
		await hostedService.StopAsync(CancellationToken.None).ConfigureAwait(false);

		hostedService.ConsecutiveErrors.ShouldBe(0);
	}

	/// <summary>
	/// Minimal fake that satisfies <see cref="IDataOrchestrationManager"/> for DI tests.
	/// </summary>
	private sealed class FakeOrchestrationManager : IDataOrchestrationManager
	{
		public Task<Guid> AddDataTaskForRecordTypeAsync(string recordType, CancellationToken cancellationToken)
			=> Task.FromResult(Guid.NewGuid());

		public ValueTask ProcessDataTasksAsync(CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}
}
