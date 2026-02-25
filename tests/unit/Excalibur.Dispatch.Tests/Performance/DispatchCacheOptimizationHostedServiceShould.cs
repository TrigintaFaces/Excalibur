// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Performance;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
/// Tests for <see cref="DispatchCacheOptimizationHostedService"/> automatic cache freezing.
/// Validates startup behavior, hot reload detection, and configuration opt-out.
/// </summary>
/// <remarks>
/// Sprint 455 - S455.5: Unit tests for auto-freeze functionality.
/// Tests the hosted service that automatically freezes caches on ApplicationStarted.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class DispatchCacheOptimizationHostedServiceShould : IDisposable
{
	private readonly IDispatchCacheManager _cacheManager;
	private readonly IHostApplicationLifetime _applicationLifetime;
	private readonly ILogger<DispatchCacheOptimizationHostedService> _logger;
	private readonly CancellationTokenSource _applicationStartedSource;
	private readonly DispatchOptions _options;
	private readonly IOptions<DispatchOptions> _optionsAccessor;

	private string? _originalDotnetWatch;
	private string? _originalModifiableAssemblies;

	public DispatchCacheOptimizationHostedServiceShould()
	{
		_cacheManager = A.Fake<IDispatchCacheManager>();
		_applicationLifetime = A.Fake<IHostApplicationLifetime>();
		_logger = A.Fake<ILogger<DispatchCacheOptimizationHostedService>>();
		_applicationStartedSource = new CancellationTokenSource();
		_options = new DispatchOptions();
		_optionsAccessor = MsOptions.Create(_options);

		// Set up ApplicationStarted to use our CancellationToken
		_ = A.CallTo(() => _applicationLifetime.ApplicationStarted)
			.Returns(_applicationStartedSource.Token);

		// Save and clear environment variables
		_originalDotnetWatch = Environment.GetEnvironmentVariable("DOTNET_WATCH");
		_originalModifiableAssemblies = Environment.GetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES");
		Environment.SetEnvironmentVariable("DOTNET_WATCH", null);
		Environment.SetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES", null);
	}

	public void Dispose()
	{
		// Restore environment variables
		Environment.SetEnvironmentVariable("DOTNET_WATCH", _originalDotnetWatch);
		Environment.SetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES", _originalModifiableAssemblies);
		_applicationStartedSource.Dispose();
	}

	#region Constructor Tests (2 tests)

	[Fact]
	public void Constructor_ThrowsOnNullCacheManager()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DispatchCacheOptimizationHostedService(
				null!,
				_applicationLifetime,
				_optionsAccessor,
				_logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullApplicationLifetime()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DispatchCacheOptimizationHostedService(
				_cacheManager,
				null!,
				_optionsAccessor,
				_logger));
	}

	#endregion

	#region StartAsync Tests (2 tests)

	[Fact]
	public async Task StartAsync_RegistersForApplicationStartedEvent()
	{
		// Arrange
		var service = new DispatchCacheOptimizationHostedService(
			_cacheManager, _applicationLifetime, _optionsAccessor, _logger);

		// Act
		await service.StartAsync(CancellationToken.None);

		// Assert - FreezeAll not called until ApplicationStarted fires
		A.CallTo(() => _cacheManager.FreezeAll()).MustNotHaveHappened();
	}

	[Fact]
	public async Task StartAsync_FreezesCachesOnApplicationStarted()
	{
		// Arrange
		var service = new DispatchCacheOptimizationHostedService(
			_cacheManager, _applicationLifetime, _optionsAccessor, _logger);

		// Act
		await service.StartAsync(CancellationToken.None);
		_applicationStartedSource.Cancel(); // Trigger ApplicationStarted

		// Assert
		_ = A.CallTo(() => _cacheManager.FreezeAll()).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region AutoFreezeOnStart Configuration Tests (2 tests)

	[Fact]
	public async Task ApplicationStarted_SkipsFreezeWhenAutoFreezeDisabled()
	{
		// Arrange
		_options.CrossCutting.Performance.AutoFreezeOnStart = false;
		var service = new DispatchCacheOptimizationHostedService(
			_cacheManager, _applicationLifetime, _optionsAccessor, _logger);

		// Act
		await service.StartAsync(CancellationToken.None);
		_applicationStartedSource.Cancel(); // Trigger ApplicationStarted

		// Assert - FreezeAll not called because AutoFreezeOnStart is false
		A.CallTo(() => _cacheManager.FreezeAll()).MustNotHaveHappened();
	}

	[Fact]
	public async Task ApplicationStarted_FreezesByDefault()
	{
		// Arrange - AutoFreezeOnStart is true by default
		_options.CrossCutting.Performance.AutoFreezeOnStart.ShouldBeTrue();
		var service = new DispatchCacheOptimizationHostedService(
			_cacheManager, _applicationLifetime, _optionsAccessor, _logger);

		// Act
		await service.StartAsync(CancellationToken.None);
		_applicationStartedSource.Cancel(); // Trigger ApplicationStarted

		// Assert
		_ = A.CallTo(() => _cacheManager.FreezeAll()).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Hot Reload Detection Tests (3 tests)

	[Fact]
	public async Task ApplicationStarted_SkipsFreezeWhenDotnetWatchIsSet()
	{
		// Arrange
		Environment.SetEnvironmentVariable("DOTNET_WATCH", "1");
		var service = new DispatchCacheOptimizationHostedService(
			_cacheManager, _applicationLifetime, _optionsAccessor, _logger);

		// Act
		await service.StartAsync(CancellationToken.None);
		_applicationStartedSource.Cancel(); // Trigger ApplicationStarted

		// Assert - FreezeAll not called because hot reload detected
		A.CallTo(() => _cacheManager.FreezeAll()).MustNotHaveHappened();
	}

	[Fact]
	public async Task ApplicationStarted_SkipsFreezeWhenDotnetWatchIsTrue()
	{
		// Arrange
		Environment.SetEnvironmentVariable("DOTNET_WATCH", "true");
		var service = new DispatchCacheOptimizationHostedService(
			_cacheManager, _applicationLifetime, _optionsAccessor, _logger);

		// Act
		await service.StartAsync(CancellationToken.None);
		_applicationStartedSource.Cancel(); // Trigger ApplicationStarted

		// Assert - FreezeAll not called because hot reload detected
		A.CallTo(() => _cacheManager.FreezeAll()).MustNotHaveHappened();
	}

	[Fact]
	public async Task ApplicationStarted_SkipsFreezeWhenModifiableAssembliesIsDebug()
	{
		// Arrange
		Environment.SetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES", "debug");
		var service = new DispatchCacheOptimizationHostedService(
			_cacheManager, _applicationLifetime, _optionsAccessor, _logger);

		// Act
		await service.StartAsync(CancellationToken.None);
		_applicationStartedSource.Cancel(); // Trigger ApplicationStarted

		// Assert - FreezeAll not called because hot reload detected
		A.CallTo(() => _cacheManager.FreezeAll()).MustNotHaveHappened();
	}

	#endregion

	#region StopAsync Tests (1 test)

	[Fact]
	public async Task StopAsync_DisposesRegistration()
	{
		// Arrange
		var service = new DispatchCacheOptimizationHostedService(
			_cacheManager, _applicationLifetime, _optionsAccessor, _logger);
		await service.StartAsync(CancellationToken.None);

		// Act - should not throw
		await service.StopAsync(CancellationToken.None);

		// Assert - no exception means success
	}

	#endregion

	#region Error Handling Tests (1 test)

	[Fact]
	public async Task ApplicationStarted_DoesNotThrowWhenFreezeAllFails()
	{
		// Arrange
		_ = A.CallTo(() => _cacheManager.FreezeAll())
			.Throws(new InvalidOperationException("Test exception"));
		var service = new DispatchCacheOptimizationHostedService(
			_cacheManager, _applicationLifetime, _optionsAccessor, _logger);

		// Act - should not throw even if FreezeAll fails
		await service.StartAsync(CancellationToken.None);
		_applicationStartedSource.Cancel(); // Trigger ApplicationStarted

		// Assert - no exception means error was handled gracefully
		_ = A.CallTo(() => _cacheManager.FreezeAll()).MustHaveHappenedOnceExactly();
	}

	#endregion
}
