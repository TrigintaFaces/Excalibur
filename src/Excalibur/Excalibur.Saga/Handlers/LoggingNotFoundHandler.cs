// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Handlers;

/// <summary>
/// Default implementation of <see cref="ISagaNotFoundHandler{TSaga}"/> that logs a warning
/// when a message arrives for a non-existent saga instance.
/// </summary>
/// <typeparam name="TSaga">The type of saga that was not found.</typeparam>
/// <remarks>
/// <para>
/// This handler logs the event at <see cref="LogLevel.Warning"/> level and takes no
/// further action. Register a custom <see cref="ISagaNotFoundHandler{TSaga}"/> to
/// override this behavior with saga creation or other recovery logic.
/// </para>
/// </remarks>
public sealed partial class LoggingNotFoundHandler<TSaga> : ISagaNotFoundHandler<TSaga>
	where TSaga : class
{
	private readonly ILogger<LoggingNotFoundHandler<TSaga>> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="LoggingNotFoundHandler{TSaga}"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public LoggingNotFoundHandler(ILogger<LoggingNotFoundHandler<TSaga>> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task HandleAsync(object message, string sagaId, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrEmpty(sagaId);

		LogSagaNotFound(typeof(TSaga).Name, sagaId, message.GetType().Name);

		return Task.CompletedTask;
	}

	[LoggerMessage(SagaEventId.SagaStateNotFound, LogLevel.Warning,
		"Saga {SagaType} with ID {SagaId} not found for message type {MessageType}")]
	private partial void LogSagaNotFound(string sagaType, string sagaId, string messageType);
}
