// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.TestTypes;

/// <summary>
/// Test interface for cache invalidation in integration tests.
/// Provides async invalidation methods for test scenarios.
/// </summary>
public interface ITestCacheInvalidator
{
	/// <summary>
	/// Invalidates a single cache key asynchronously.
	/// </summary>
	/// <param name="key">The cache key to invalidate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the async operation.</returns>
	Task InvalidateAsync(string key, CancellationToken cancellationToken = default);

	/// <summary>
	/// Invalidates multiple cache keys asynchronously.
	/// </summary>
	/// <param name="keys">The cache keys to invalidate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the async operation.</returns>
	Task InvalidateManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

	/// <summary>
	/// Invalidates all cache entries matching a pattern.
	/// </summary>
	/// <param name="pattern">The pattern to match.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default);

	/// <summary>
	/// Invalidates all cache entries with a specific tag.
	/// </summary>
	/// <param name="tag">The tag to match.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op implementation of ITestCacheInvalidator for tests.
/// </summary>
public class NoOpCacheInvalidator : ITestCacheInvalidator
{
	/// <inheritdoc/>
	public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	/// <inheritdoc/>
	public Task InvalidateManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	/// <inheritdoc/>
	public Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;

	/// <inheritdoc/>
	public Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
		=> Task.CompletedTask;
}
