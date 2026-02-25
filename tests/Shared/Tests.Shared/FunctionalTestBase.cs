// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;

using Tests.Shared.Categories;
using Tests.Shared.Infrastructure;

namespace Tests.Shared;

/// <summary>
/// Base class for functional/E2E tests. Provides common utilities for end-to-end user scenario tests.
/// </summary>
/// <remarks>
/// <para>
/// Functional tests should:
/// - Test complete user scenarios from end-to-end
/// - Use real infrastructure (via TestContainers)
/// - Validate integration across multiple components
/// - Be the slowest tests but provide highest confidence
/// - Test realistic production-like scenarios
/// </para>
/// <para>
/// This base class provides:
/// - Longer default timeout than integration tests (60 seconds via <see cref="TestTimeouts.Functional"/>)
/// - All capabilities from <see cref="IntegrationTestBase"/>
/// - Helper for creating test hosts
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Functional)]
public abstract class FunctionalTestBase : IntegrationTestBase
{
	/// <summary>
	/// Gets the timeout duration for functional tests.
	/// Default is <see cref="TestTimeouts.Functional"/> (60 seconds * multiplier).
	/// </summary>
	protected override TimeSpan TestTimeout => TestTimeouts.Functional;

	/// <summary>
	/// Creates a test host with optional service configuration.
	/// </summary>
	/// <param name="configureServices">Optional action to configure services.</param>
	/// <returns>The configured host.</returns>
	protected IHost CreateHost(Action<IServiceCollection>? configureServices = null)
	{
		var builder = Host.CreateDefaultBuilder()
			.ConfigureServices((context, services) =>
			{
				configureServices?.Invoke(services);
			});

		return builder.Build();
	}

	/// <summary>
	/// Creates a test host with configuration and optional service configuration.
	/// </summary>
	/// <param name="configureHost">Action to configure the host builder.</param>
	/// <param name="configureServices">Optional action to configure services.</param>
	/// <returns>The configured host.</returns>
	protected IHost CreateHost(
		Action<IHostBuilder> configureHost,
		Action<IServiceCollection>? configureServices = null)
	{
		var builder = Host.CreateDefaultBuilder();
		configureHost(builder);

		_ = builder.ConfigureServices((context, services) =>
		{
			configureServices?.Invoke(services);
		});

		return builder.Build();
	}

	/// <summary>
	/// Waits for a condition to become true, throwing on timeout.
	/// This is a convenience wrapper that throws instead of returning false.
	/// </summary>
	/// <param name="condition">The condition to check.</param>
	/// <param name="timeoutMessage">Optional message for timeout exception.</param>
	/// <param name="timeout">Optional timeout (defaults to <see cref="TestTimeout"/>).</param>
	/// <exception cref="TimeoutException">If the condition is not met within timeout.</exception>
	protected async Task WaitForConditionOrThrowAsync(
		Func<bool> condition,
		string? timeoutMessage = null,
		TimeSpan? timeout = null)
	{
		var result = await WaitForConditionAsync(condition, timeout).ConfigureAwait(false);
		if (!result)
		{
			throw new TimeoutException(
				timeoutMessage ?? $"Condition not met within {timeout ?? TestTimeout}");
		}
	}

	/// <summary>
	/// Waits for an async condition to become true, throwing on timeout.
	/// This is a convenience wrapper that throws instead of returning false.
	/// </summary>
	/// <param name="condition">The async condition to check.</param>
	/// <param name="timeoutMessage">Optional message for timeout exception.</param>
	/// <param name="timeout">Optional timeout (defaults to <see cref="TestTimeout"/>).</param>
	/// <exception cref="TimeoutException">If the condition is not met within timeout.</exception>
	protected async Task WaitForConditionOrThrowAsync(
		Func<Task<bool>> condition,
		string? timeoutMessage = null,
		TimeSpan? timeout = null)
	{
		var result = await WaitForConditionAsync(condition, timeout).ConfigureAwait(false);
		if (!result)
		{
			throw new TimeoutException(
				timeoutMessage ?? $"Condition not met within {timeout ?? TestTimeout}");
		}
	}
}
