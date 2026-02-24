// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Extensions;

namespace Excalibur.Dispatch.Tests.Extensions;

/// <summary>
/// Depth tests for <see cref="TaskExtensions"/>.
/// Covers TimeoutAfterAsync (void and typed), WithCancellationAsync,
/// SafeAwaitAsync (void and typed), null guards, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TaskExtensionsShould
{
	// --- TimeoutAfterAsync (void) ---

	[Fact]
	public async Task TimeoutAfterAsync_ThrowsWhenTaskIsNull()
	{
		Task task = null!;

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await task.TimeoutAfterAsync(TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public async Task TimeoutAfterAsync_CompletesWhenTaskFinishesInTime()
	{
		// Arrange
		var task = global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(10);

		// Act & Assert — should not throw
		await task.TimeoutAfterAsync(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task TimeoutAfterAsync_ThrowsTimeoutExceptionWhenTaskExceedsTimeout()
	{
		// Arrange
		var task = global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(TimeSpan.FromSeconds(10));

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(async () =>
			await task.TimeoutAfterAsync(TimeSpan.FromMilliseconds(50)));
	}

	// --- TimeoutAfterAsync<T> ---

	[Fact]
	public async Task TimeoutAfterAsync_Typed_ThrowsWhenTaskIsNull()
	{
		Task<int> task = null!;

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await task.TimeoutAfterAsync(TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public async Task TimeoutAfterAsync_Typed_ReturnsResultWhenFinishesInTime()
	{
		// Arrange
		var task = Task.FromResult(42);

		// Act
		var result = await task.TimeoutAfterAsync(TimeSpan.FromSeconds(5));

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task TimeoutAfterAsync_Typed_ThrowsTimeoutExceptionWhenExceedsTimeout()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		var task = tcs.Task; // Never completes

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(async () =>
			await task.TimeoutAfterAsync(TimeSpan.FromMilliseconds(50)));
	}

	// --- WithCancellationAsync ---

	[Fact]
	public async Task WithCancellationAsync_ThrowsWhenTaskIsNull()
	{
		Task<int> task = null!;

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await task.WithCancellationAsync(CancellationToken.None));
	}

	[Fact]
	public async Task WithCancellationAsync_ReturnsResultWhenNotCancelled()
	{
		// Arrange
		var task = Task.FromResult(42);

		// Act
		var result = await task.WithCancellationAsync(CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task WithCancellationAsync_ReturnsDefaultWhenCancelled()
	{
		// Arrange
		var tcs = new TaskCompletionSource<int>();
		using var cts = new CancellationTokenSource();

		// Act
		var resultTask = tcs.Task.WithCancellationAsync(cts.Token);
		await cts.CancelAsync();
		var result = await resultTask;

		// Assert
		result.ShouldBe(default(int));
	}

	// --- SafeAwaitAsync (void) ---

	[Fact]
	public async Task SafeAwaitAsync_SwallowsExceptions()
	{
		// Arrange
		var task = Task.FromException(new InvalidOperationException("test"));

		// Act & Assert — should not throw
		await task.SafeAwaitAsync();
	}

	[Fact]
	public async Task SafeAwaitAsync_CompletesNormally()
	{
		// Arrange
		var task = Task.CompletedTask;

		// Act & Assert — should not throw
		await task.SafeAwaitAsync();
	}

	// --- SafeAwaitAsync<T> ---

	[Fact]
	public async Task SafeAwaitAsync_Typed_ReturnsDefaultOnException()
	{
		// Arrange
		var task = Task.FromException<int>(new InvalidOperationException("test"));

		// Act
		var result = await task.SafeAwaitAsync();

		// Assert
		result.ShouldBe(default(int));
	}

	[Fact]
	public async Task SafeAwaitAsync_Typed_ReturnsResultOnSuccess()
	{
		// Arrange
		var task = Task.FromResult(42);

		// Act
		var result = await task.SafeAwaitAsync();

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task SafeAwaitAsync_Typed_ReturnsNullOnExceptionForReferenceType()
	{
		// Arrange
		var task = Task.FromException<string>(new InvalidOperationException("test"));

		// Act
		var result = await task.SafeAwaitAsync();

		// Assert
		result.ShouldBeNull();
	}
}

