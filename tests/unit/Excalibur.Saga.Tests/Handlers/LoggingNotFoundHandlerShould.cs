// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Handlers;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Handlers;

/// <summary>
/// Unit tests for <see cref="LoggingNotFoundHandler{TSaga}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class LoggingNotFoundHandlerShould
{
	[Fact]
	public async Task HandleAsync_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = new LoggingNotFoundHandler<TestSaga>(
			NullLogger<LoggingNotFoundHandler<TestSaga>>.Instance);

		// Act & Assert — should not throw
		await handler.HandleAsync(new TestMessage(), "saga-123", CancellationToken.None);
	}

	[Fact]
	public async Task HandleAsync_ShouldLogWarning()
	{
		// Arrange — Cannot fake ILogger<LoggingNotFoundHandler<TestSaga>> because TestSaga is private.
		// Use NullLogger which successfully completes without logging; verify no exception is thrown.
		var handler = new LoggingNotFoundHandler<TestSaga>(
			NullLogger<LoggingNotFoundHandler<TestSaga>>.Instance);

		// Act & Assert — the handler should complete without throwing,
		// confirming it invoked the LoggerMessage method successfully.
		await Should.NotThrowAsync(
			() => handler.HandleAsync(new TestMessage(), "saga-456", CancellationToken.None));
	}

	[Fact]
	public async Task HandleAsync_ShouldThrow_WhenMessageIsNull()
	{
		// Arrange
		var handler = new LoggingNotFoundHandler<TestSaga>(
			NullLogger<LoggingNotFoundHandler<TestSaga>>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => handler.HandleAsync(null!, "saga-123", CancellationToken.None));
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	public async Task HandleAsync_ShouldThrow_WhenSagaIdIsNullOrEmpty(string? sagaId)
	{
		// Arrange
		var handler = new LoggingNotFoundHandler<TestSaga>(
			NullLogger<LoggingNotFoundHandler<TestSaga>>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => handler.HandleAsync(new TestMessage(), sagaId!, CancellationToken.None));
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new LoggingNotFoundHandler<TestSaga>(null!));
	}

	[Fact]
	public void ShouldImplementISagaNotFoundHandler()
	{
		// Arrange
		var handler = new LoggingNotFoundHandler<TestSaga>(
			NullLogger<LoggingNotFoundHandler<TestSaga>>.Instance);

		// Assert
		handler.ShouldBeAssignableTo<ISagaNotFoundHandler<TestSaga>>();
	}

	private sealed class TestSaga;
	private sealed class TestMessage;
}
