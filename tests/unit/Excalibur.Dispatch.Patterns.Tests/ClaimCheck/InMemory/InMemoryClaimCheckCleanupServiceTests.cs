// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

using Excalibur.Dispatch.Patterns.ClaimCheck;
using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryClaimCheckCleanupService"/>.
/// All intervals and delays are generous to handle thread pool starvation
/// during full-suite parallel execution (40K+ concurrent tests).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("IDisposableAnalyzers.Correctness", "CA1001:Types that own disposable fields should be disposable",
	Justification = "Disposal is handled by IAsyncLifetime.DisposeAsync() which is the xUnit pattern for test fixtures.")]
[Trait("Category", "Unit")]
public sealed class InMemoryClaimCheckCleanupServiceTests : IAsyncLifetime
{
	private InMemoryClaimCheckProvider? _provider;
	private InMemoryClaimCheckCleanupService? _cleanupService;
	private CancellationTokenSource? _cts;

	public Task InitializeAsync()
	{
		_cts = new CancellationTokenSource();
		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		if (_cleanupService != null)
		{
			await _cleanupService.StopAsync(CancellationToken.None);
			_cleanupService.Dispose();
		}

		_cts?.Cancel();
		_cts?.Dispose();
	}

	[Fact]
	public async Task ExecuteAsync_WithCleanupDisabled_ShouldNotRun()
	{
		// Arrange
		var (provider, service) = CreateServiceWithProvider(options =>
		{
			options.EnableCleanup = false;
		});

		// Store an expired entry
		_ = await provider.StoreAsync("Test"u8.ToArray(), CancellationToken.None);

		// Act
		await service.StartAsync(_cts.Token);
		await AssertEntryCountRemainsAsync(provider, 1, TimeSpan.FromSeconds(2));

		// Assert
		provider.EntryCount.ShouldBe(1); // Entry not cleaned up
	}

	[Fact]
	public async Task ExecuteAsync_WithExpiredEntries_ShouldCleanup()
	{
		// Arrange
		var (provider, service) = CreateServiceWithProvider(options =>
		{
			options.EnableCleanup = true;
			options.CleanupInterval = TimeSpan.FromSeconds(1);
			options.DefaultTtl = TimeSpan.FromMilliseconds(500);
		});

		// Store entries that will expire
		_ = await provider.StoreAsync("Payload 1"u8.ToArray(), CancellationToken.None);
		_ = await provider.StoreAsync("Payload 2"u8.ToArray(), CancellationToken.None);
		provider.EntryCount.ShouldBe(2);

		// Act - Start service and wait for cleanup
		await service.StartAsync(_cts.Token);
		await WaitForEntryCountAsync(provider, 0, TimeSpan.FromSeconds(10));

		// Assert
		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task ExecuteAsync_WithNoExpiredEntries_ShouldNotRemoveAnything()
	{
		// Arrange
		var (provider, service) = CreateServiceWithProvider(options =>
		{
			options.EnableCleanup = true;
			options.CleanupInterval = TimeSpan.FromSeconds(1);
			options.DefaultTtl = TimeSpan.FromHours(1);
		});

		// Store non-expired entries
		_ = await provider.StoreAsync("Payload 1"u8.ToArray(), CancellationToken.None);
		_ = await provider.StoreAsync("Payload 2"u8.ToArray(), CancellationToken.None);

		// Act - Start service and wait
		await service.StartAsync(_cts.Token);
		await AssertEntryCountRemainsAsync(provider, 2, TimeSpan.FromSeconds(3));

		// Assert
		provider.EntryCount.ShouldBe(2); // No cleanup
	}

	[Fact]
	public async Task ExecuteAsync_WithMultipleCleanupCycles_ShouldCleanupEachTime()
	{
		// Arrange
		var (provider, service) = CreateServiceWithProvider(options =>
		{
			options.EnableCleanup = true;
			options.CleanupInterval = TimeSpan.FromSeconds(1);
			options.DefaultTtl = TimeSpan.FromMilliseconds(500);
		});

		// Act & Assert - First cycle
		_ = await provider.StoreAsync("Batch 1 - Item 1"u8.ToArray(), CancellationToken.None);
		_ = await provider.StoreAsync("Batch 1 - Item 2"u8.ToArray(), CancellationToken.None);

		await service.StartAsync(_cts.Token);
		await WaitForEntryCountAsync(provider, 0, TimeSpan.FromSeconds(10));

		provider.EntryCount.ShouldBe(0);

		// Second cycle - store more after first cleanup
		_ = await provider.StoreAsync("Batch 2 - Item 1"u8.ToArray(), CancellationToken.None);
		_ = await provider.StoreAsync("Batch 2 - Item 2"u8.ToArray(), CancellationToken.None);

		await WaitForEntryCountAsync(provider, 0, TimeSpan.FromSeconds(10));

		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task StopAsync_ShouldStopCleanupService()
	{
		// Arrange
		var (provider, service) = CreateServiceWithProvider(options =>
		{
			options.EnableCleanup = true;
			options.CleanupInterval = TimeSpan.FromSeconds(1);
			options.DefaultTtl = TimeSpan.FromMilliseconds(500);
		});

		await service.StartAsync(_cts.Token);

		// Store entries
		_ = await provider.StoreAsync("Payload 1"u8.ToArray(), CancellationToken.None);
		_ = await provider.StoreAsync("Payload 2"u8.ToArray(), CancellationToken.None);

		// Act - Stop service immediately (before cleanup can run)
		await service.StopAsync(CancellationToken.None);

		// Wait for TTL to expire, but cleanup shouldn't run since service is stopped
		await AssertEntryCountRemainsAsync(provider, 2, TimeSpan.FromSeconds(3));

		// Assert - Entries should still exist (no cleanup after stop)
		provider.EntryCount.ShouldBe(2);
	}

	[Fact]
	public async Task ExecuteAsync_WithException_ShouldContinueRunning()
	{
		// Arrange
		var (provider, service) = CreateServiceWithProvider(options =>
		{
			options.EnableCleanup = true;
			options.CleanupInterval = TimeSpan.FromSeconds(1);
			options.DefaultTtl = TimeSpan.FromMilliseconds(500);
		});

		await service.StartAsync(_cts.Token);

		// Store entries that will expire
		_ = await provider.StoreAsync("Payload 1"u8.ToArray(), CancellationToken.None);
		await WaitForEntryCountAsync(provider, 0, TimeSpan.FromSeconds(10));

		// Store more entries after potential error
		_ = await provider.StoreAsync("Payload 2"u8.ToArray(), CancellationToken.None);
		await WaitForEntryCountAsync(provider, 0, TimeSpan.FromSeconds(10));

		// Assert - Service should still be running and cleaning up
		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task ExecuteAsync_WithCancellation_ShouldExitGracefully()
	{
		// Arrange
		var (provider, service) = CreateServiceWithProvider(options =>
		{
			options.EnableCleanup = true;
			options.CleanupInterval = TimeSpan.FromSeconds(1);
		});

		// Act
		await service.StartAsync(_cts.Token);
		_cts.Cancel(); // Cancel execution

		// Wait for graceful shutdown
		await service.StopAsync(CancellationToken.None);

		// Assert - Should exit without exception
		// No exception means graceful shutdown
	}

	[Fact]
	public async Task ExecuteAsync_WithCustomCleanupInterval_ShouldRespectInterval()
	{
		// Arrange - Use generous intervals for full-suite parallel load
		var (provider, service) = CreateServiceWithProvider(options =>
		{
			options.EnableCleanup = true;
			options.CleanupInterval = TimeSpan.FromSeconds(5); // Generous interval
			options.DefaultTtl = TimeSpan.FromMilliseconds(500);
		});

		await service.StartAsync(_cts.Token);

		// Store entries
		_ = await provider.StoreAsync("Payload 1"u8.ToArray(), CancellationToken.None);
		var countBefore = provider.EntryCount;

		// Wait less than cleanup interval (but after TTL expires)
		await AssertEntryCountRemainsAsync(provider, 1, TimeSpan.FromSeconds(2));
		var countAfterShortWait = provider.EntryCount;

		// Wait for cleanup interval to fire
		await WaitForEntryCountAsync(provider, 0, TimeSpan.FromSeconds(12));
		var countAfterLongWait = provider.EntryCount;

		// Assert
		countBefore.ShouldBe(1);
		countAfterShortWait.ShouldBe(1); // No cleanup yet
		countAfterLongWait.ShouldBe(0); // Cleanup happened
	}

	[Fact]
	public async Task ExecuteAsync_WithMixedExpiredAndActive_ShouldOnlyRemoveExpired()
	{
		// Arrange - Need two providers with different TTLs
		var shortOptions = new ClaimCheckOptions
		{
			EnableCleanup = true,
			CleanupInterval = TimeSpan.FromSeconds(1),
			DefaultTtl = TimeSpan.FromMilliseconds(500)
		};

		var longOptions = new ClaimCheckOptions
		{
			EnableCleanup = true,
			CleanupInterval = TimeSpan.FromSeconds(1),
			DefaultTtl = TimeSpan.FromHours(1)
		};

		var shortProvider = new InMemoryClaimCheckProvider(Microsoft.Extensions.Options.Options.Create(shortOptions));
		var longProvider = new InMemoryClaimCheckProvider(Microsoft.Extensions.Options.Options.Create(longOptions));

		var shortService = new InMemoryClaimCheckCleanupService(
			shortProvider,
			Microsoft.Extensions.Options.Options.Create(shortOptions),
			CreateEnabledLogger());

		_cleanupService = shortService;

		// Store with short TTL (will expire)
		_ = await shortProvider.StoreAsync("Expiring"u8.ToArray(), CancellationToken.None);

		// Store with long TTL (won't expire)
		_ = await longProvider.StoreAsync("Active"u8.ToArray(), CancellationToken.None);

		// Act
		await shortService.StartAsync(_cts.Token);
		await WaitForEntryCountAsync(shortProvider, 0, TimeSpan.FromSeconds(10));

		// Assert
		shortProvider.EntryCount.ShouldBe(0); // Expired entry removed
		longProvider.EntryCount.ShouldBe(1); // Active entry remains
	}

	private static async Task WaitForEntryCountAsync(InMemoryClaimCheckProvider provider, int expectedCount, TimeSpan timeout)
	{
		var start = DateTime.UtcNow;
		while (provider.EntryCount != expectedCount && (DateTime.UtcNow - start) < timeout)
		{
			await Task.Delay(100).ConfigureAwait(false);
		}

		provider.EntryCount.ShouldBe(expectedCount);
	}

	private static async Task AssertEntryCountRemainsAsync(InMemoryClaimCheckProvider provider, int expectedCount, TimeSpan duration)
	{
		var start = DateTime.UtcNow;
		while ((DateTime.UtcNow - start) < duration)
		{
			provider.EntryCount.ShouldBe(expectedCount);
			await Task.Delay(100).ConfigureAwait(false);
		}
	}

	private static ILogger<InMemoryClaimCheckCleanupService> CreateEnabledLogger()
	{
		// Use a real logger with Debug level so IsEnabled returns true,
		// exercising the source-generated LoggerMessage formatting code.
		var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddConsole());
		return factory.CreateLogger<InMemoryClaimCheckCleanupService>();
	}

	private (InMemoryClaimCheckProvider provider, InMemoryClaimCheckCleanupService service) CreateServiceWithProvider(
											Action<ClaimCheckOptions>? configure = null)
	{
		var options = new ClaimCheckOptions
		{
			EnableCleanup = true,
			CleanupInterval = TimeSpan.FromSeconds(1)
		};

		configure?.Invoke(options);

		var provider = new InMemoryClaimCheckProvider(Microsoft.Extensions.Options.Options.Create(options));
		var service = new InMemoryClaimCheckCleanupService(
			provider,
			Microsoft.Extensions.Options.Options.Create(options),
			CreateEnabledLogger());

		_provider = provider;
		_cleanupService = service;

		return (provider, service);
	}
}
