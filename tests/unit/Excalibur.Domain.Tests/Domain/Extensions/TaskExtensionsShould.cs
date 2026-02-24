// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Extensions;

namespace Excalibur.Tests.Domain.Extensions;

/// <summary>
/// Unit tests for <see cref="TaskExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class TaskExtensionsShould
{
	#region TimeoutAfterAsync (Task) Tests

	[Fact]
	public async Task TimeoutAfterAsync_Task_CompletesNormally_WhenWithinTimeout()
	{
		// Arrange
		var task = global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(10);
		var timeout = TimeSpan.FromSeconds(5);

		// Act & Assert - should not throw
		await task.TimeoutAfterAsync(timeout).ConfigureAwait(false);
	}

	[Fact]
	public async Task TimeoutAfterAsync_Task_ThrowsTimeoutException_WhenExceedsTimeout()
	{
		// Arrange
		var task = global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(TimeSpan.FromSeconds(10));
		var timeout = TimeSpan.FromMilliseconds(50);

		// Act & Assert
		var exception = await Should.ThrowAsync<TimeoutException>(() =>
			task.TimeoutAfterAsync(timeout)).ConfigureAwait(false);

		exception.Message.ShouldContain("0.05"); // 50ms = 0.05 seconds
	}

	[Fact]
	public async Task TimeoutAfterAsync_Task_ThrowsArgumentNullException_WhenTaskIsNull()
	{
		// Arrange
		Task? task = null;
		var timeout = TimeSpan.FromSeconds(1);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			task.TimeoutAfterAsync(timeout)).ConfigureAwait(false);
	}

	[Fact]
	public async Task TimeoutAfterAsync_Task_PropagatesTaskException()
	{
		// Arrange
		var expectedException = new InvalidOperationException("Test exception");
		var task = Task.FromException(expectedException);
		var timeout = TimeSpan.FromSeconds(5);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
			task.TimeoutAfterAsync(timeout)).ConfigureAwait(false);

		exception.ShouldBe(expectedException);
	}

	#endregion TimeoutAfterAsync (Task) Tests

	#region TimeoutAfterAsync (Task<T>) Tests

	[Fact]
	public async Task TimeoutAfterAsync_TaskT_ReturnsResult_WhenWithinTimeout()
	{
		// Arrange
		var expectedResult = 42;
		var task = Task.FromResult(expectedResult);
		var timeout = TimeSpan.FromSeconds(5);

		// Act
		var result = await task.TimeoutAfterAsync(timeout).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task TimeoutAfterAsync_TaskT_ThrowsTimeoutException_WhenExceedsTimeout()
	{
		// Arrange
		var task = DelayWithResult(TimeSpan.FromSeconds(10), 42);
		var timeout = TimeSpan.FromMilliseconds(50);

		// Act & Assert
		_ = await Should.ThrowAsync<TimeoutException>(() =>
			task.TimeoutAfterAsync(timeout)).ConfigureAwait(false);
	}

	[Fact]
	public async Task TimeoutAfterAsync_TaskT_ThrowsArgumentNullException_WhenTaskIsNull()
	{
		// Arrange
		Task<int>? task = null;
		var timeout = TimeSpan.FromSeconds(1);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			task.TimeoutAfterAsync(timeout)).ConfigureAwait(false);
	}

	[Fact]
	public async Task TimeoutAfterAsync_TaskT_PropagatesTaskException()
	{
		// Arrange
		var expectedException = new InvalidOperationException("Test exception");
		var task = Task.FromException<int>(expectedException);
		var timeout = TimeSpan.FromSeconds(5);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
			task.TimeoutAfterAsync(timeout)).ConfigureAwait(false);

		exception.ShouldBe(expectedException);
	}

	[Fact]
	public async Task TimeoutAfterAsync_TaskT_ReturnsStringResult()
	{
		// Arrange
		const string expectedResult = "Hello, World!";
		var task = Task.FromResult(expectedResult);
		var timeout = TimeSpan.FromSeconds(5);

		// Act
		var result = await task.TimeoutAfterAsync(timeout).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task TimeoutAfterAsync_TaskT_ReturnsComplexObjectResult()
	{
		// Arrange
		var expectedResult = new TestObject { Id = 1, Name = "Test" };
		var task = Task.FromResult(expectedResult);
		var timeout = TimeSpan.FromSeconds(5);

		// Act
		var result = await task.TimeoutAfterAsync(timeout).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion TimeoutAfterAsync (Task<T>) Tests

	#region Edge Cases

	[Fact]
	public async Task TimeoutAfterAsync_Task_HandlesZeroTimeout()
	{
		// Arrange
		var task = global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(100);
		var timeout = TimeSpan.Zero;

		// Act & Assert - zero timeout should cause immediate timeout
		_ = await Should.ThrowAsync<TimeoutException>(() =>
			task.TimeoutAfterAsync(timeout)).ConfigureAwait(false);
	}

	[Fact]
	public async Task TimeoutAfterAsync_TaskT_HandlesZeroTimeout()
	{
		// Arrange
		var task = DelayWithResult(TimeSpan.FromMilliseconds(100), 42);
		var timeout = TimeSpan.Zero;

		// Act & Assert - zero timeout should cause immediate timeout
		_ = await Should.ThrowAsync<TimeoutException>(() =>
			task.TimeoutAfterAsync(timeout)).ConfigureAwait(false);
	}

	[Fact]
	public async Task TimeoutAfterAsync_Task_WorksWithAlreadyCompletedTask()
	{
		// Arrange
		var task = Task.CompletedTask;
		var timeout = TimeSpan.FromSeconds(1);

		// Act & Assert - should complete immediately
		await task.TimeoutAfterAsync(timeout).ConfigureAwait(false);
	}

	[Fact]
	public async Task TimeoutAfterAsync_TaskT_WorksWithAlreadyCompletedTask()
	{
		// Arrange
		var task = Task.FromResult(42);
		var timeout = TimeSpan.FromSeconds(1);

		// Act
		var result = await task.TimeoutAfterAsync(timeout).ConfigureAwait(false);

		// Assert
		result.ShouldBe(42);
	}

	#endregion Edge Cases

	private static async Task<T> DelayWithResult<T>(TimeSpan delay, T result)
	{
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(delay).ConfigureAwait(false);
		return result;
	}

	private sealed class TestObject
	{
		public int Id { get; init; }
		public string Name { get; init; } = string.Empty;
	}
}
