// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Processing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DataProcessing.Processing;

/// <summary>
/// Unit tests for <see cref="DataProcessingHostedService"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataProcessingHostedServiceShould : UnitTestBase
{
	private readonly IDataOrchestrationManager _mockManager = A.Fake<IDataOrchestrationManager>();

	private DataProcessingHostedService CreateService(
		DataProcessingHostedServiceOptions? options = null)
	{
		var opts = Options.Create(options ?? new DataProcessingHostedServiceOptions());
		return new DataProcessingHostedService(
			CreateScopeFactory(),
			opts,
			NullLogger<DataProcessingHostedService>.Instance);
	}

	private IServiceScopeFactory CreateScopeFactory()
	{
		var scopeFactory = A.Fake<IServiceScopeFactory>();
		var scope = A.Fake<IServiceScope>();
		var serviceProvider = A.Fake<IServiceProvider>();

		A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);
		A.CallTo(() => scope.ServiceProvider).Returns(serviceProvider);
		A.CallTo(() => serviceProvider.GetService(typeof(IDataOrchestrationManager)))
			.Returns(_mockManager);

		return scopeFactory;
	}

	[Fact]
	public void ThrowArgumentNullException_WhenScopeFactoryIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DataProcessingHostedService(
				null!,
				Options.Create(new DataProcessingHostedServiceOptions()),
				NullLogger<DataProcessingHostedService>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DataProcessingHostedService(
				CreateScopeFactory(),
				null!,
				NullLogger<DataProcessingHostedService>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DataProcessingHostedService(
				CreateScopeFactory(),
				Options.Create(new DataProcessingHostedServiceOptions()),
				null!));
	}

	[Fact]
	public async Task NotCallOrchestrationManager_WhenDisabled()
	{
		// Arrange
		var service = CreateService(new DataProcessingHostedServiceOptions { Enabled = false });
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

		// Act
		await service.StartAsync(cts.Token).ConfigureAwait(false);
		await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
		await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _mockManager.ProcessDataTasksAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task CallProcessDataTasks_WhenEnabled()
	{
		// Arrange
		var callCount = 0;
		A.CallTo(() => _mockManager.ProcessDataTasksAsync(A<CancellationToken>._))
			.Invokes(() => Interlocked.Increment(ref callCount))
			.Returns(ValueTask.CompletedTask);

		var service = CreateService(new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.FromMilliseconds(50),
		});

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref callCount) >= 2, TimeSpan.FromSeconds(10));
		await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		Volatile.Read(ref callCount).ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task TrackHealthState_WhenProcessingSucceeds()
	{
		// Arrange
		A.CallTo(() => _mockManager.ProcessDataTasksAsync(A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		var service = CreateService(new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.FromMilliseconds(50),
		});

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => service.IsHealthy, TimeSpan.FromSeconds(5));
		await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		service.ConsecutiveErrors.ShouldBe(0);
	}

	[Fact]
	public async Task BecomeUnhealthy_AfterConsecutiveErrors()
	{
		// Arrange
		A.CallTo(() => _mockManager.ProcessDataTasksAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("Test error"));

		var service = CreateService(new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.FromMilliseconds(50),
			UnhealthyThreshold = 2,
		});

		// Act -- wait for errors to accumulate past the threshold, not for IsHealthy
		// (IsHealthy also becomes false on normal shutdown, causing a race).
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => service.ConsecutiveErrors >= 2, TimeSpan.FromSeconds(10));
		await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		service.IsHealthy.ShouldBeFalse();
		service.ConsecutiveErrors.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task ResetConsecutiveErrors_OnSuccess()
	{
		// Arrange -- first call fails, second succeeds
		var callCount = 0;
		A.CallTo(() => _mockManager.ProcessDataTasksAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var count = Interlocked.Increment(ref callCount);
				if (count == 1)
				{
					throw new InvalidOperationException("First call fails");
				}

				return ValueTask.CompletedTask;
			});

		var service = CreateService(new DataProcessingHostedServiceOptions
		{
			PollingInterval = TimeSpan.FromMilliseconds(50),
			UnhealthyThreshold = 5,
		});

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref callCount) >= 3, TimeSpan.FromSeconds(10));
		await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert -- errors reset after successful call
		service.ConsecutiveErrors.ShouldBe(0);
	}
}
