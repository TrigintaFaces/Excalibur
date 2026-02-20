// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Transactions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DispatchTransactionOptions = Excalibur.Dispatch.Options.Middleware.TransactionOptions;
using IMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;
using IMessageResult = Excalibur.Dispatch.Abstractions.IMessageResult;
using MessageKinds = Excalibur.Dispatch.Abstractions.MessageKinds;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware responsible for managing transactional boundaries around message processing to ensure ACID properties and consistency across
/// multiple resources.
/// </summary>
/// <remarks>
/// This middleware wraps message processing in a transaction scope to ensure that all operations within the handler execute as a single
/// atomic unit. It supports:
/// <list type="bullet">
/// <item> Database transaction management </item>
/// <item> Distributed transaction coordination </item>
/// <item> Configurable transaction isolation levels </item>
/// <item> Automatic rollback on exceptions </item>
/// <item> Integration with outbox patterns for consistent event publishing </item>
/// </list>
/// This middleware primarily targets Action messages (commands) that modify state, rather than Events which are typically read-only notifications.
/// </remarks>
[AppliesTo(MessageKinds.Action)]
public sealed partial class TransactionMiddleware : IDispatchMiddleware
{
	private readonly DispatchTransactionOptions _options;

	private readonly ITransactionService _transactionService;

	private readonly ILogger<TransactionMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransactionMiddleware"/> class.
	/// Creates a new transaction middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for transaction management. </param>
	/// <param name="transactionService"> Service for managing transaction lifecycle. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public TransactionMiddleware(
		IOptions<DispatchTransactionOptions> options,
		ITransactionService transactionService,
		ILogger<TransactionMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(transactionService);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_transactionService = transactionService;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

	/// <inheritdoc />
	/// <remarks>
	/// Transactions primarily apply to Actions (commands) that modify state, rather than Events which are typically read-only notifications.
	/// </remarks>
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Skip transaction management if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Check if message requires transaction
		if (!RequiresTransaction(message))
		{
			LogMessageTypeDoesNotRequireTransaction(message.GetType().Name);

			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Determine transaction configuration
		var transactionConfig = DetermineTransactionConfiguration(message, context);

		// Set up logging scope
		using var logScope = CreateTransactionLoggingScope(message, transactionConfig);

		// Set up OpenTelemetry activity tags
		SetTransactionActivityTags(message, transactionConfig);

		LogStartingTransactionWithIsolationLevel(message.GetType().Name, transactionConfig.IsolationLevel);

		// Begin transaction
		var transaction = await _transactionService.BeginTransactionAsync(transactionConfig, cancellationToken)
			.ConfigureAwait(false);

		try
		{
			// Set transaction in context for downstream middleware and handlers
			SetTransactionContext(context, transaction);

			using (transaction)
			{
				// Continue pipeline execution within transaction
				var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

				// Check if the result indicates success
				if (IsSuccessResult(result))
				{
					// Commit transaction on success
					await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

					LogTransactionCommittedSuccessfully(message.GetType().Name);
				}
				else
				{
					// Rollback transaction on failure
					await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

					LogTransactionRolledBackUnsuccessfulResult(message.GetType().Name);
				}

				return result;
			}
		}
		catch (Exception ex)
		{
			try
			{
				// Rollback transaction on exception
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

				LogTransactionRolledBackException(message.GetType().Name, ex);
			}
			catch (Exception rollbackEx)
			{
				LogErrorDuringTransactionRollback(message.GetType().Name, rollbackEx);
			}

			throw;
		}
	}

	/// <summary>
	/// Determines if a message result indicates success.
	/// </summary>
	private static bool IsSuccessResult(IMessageResult result)
	{
		// Check common success indicators This would be extended based on your IMessageResult implementation
		if (result is { Succeeded: true })
		{
			return true;
		}

		// Check for specific result types that indicate success
		return result.GetType().Name.Contains("Success", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Sets transaction context in the message context for downstream access.
	/// </summary>
	private static void SetTransactionContext(IMessageContext context, ITransaction transaction)
	{
		context.SetItem("Transaction", transaction);
		context.SetItem("TransactionId", transaction.Id);
	}

	/// <summary>
	/// Sets OpenTelemetry activity tags for transaction tracing.
	/// </summary>
	private static void SetTransactionActivityTags(
		IDispatchMessage message,
		TransactionConfiguration config)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("transaction.message_type", message.GetType().Name);
		_ = activity.SetTag("transaction.isolation_level", config.IsolationLevel.ToString());
		_ = activity.SetTag("transaction.timeout_seconds", config.Timeout.TotalSeconds);
		_ = activity.SetTag("transaction.distributed", config.EnableDistributedTransactions);
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.TransactionMiddlewareExecuting, LogLevel.Debug,
		"Message type {MessageType} does not require transaction")]
	private partial void LogMessageTypeDoesNotRequireTransaction(string messageType);

	[LoggerMessage(MiddlewareEventId.TransactionStarted, LogLevel.Debug,
		"Starting transaction for message {MessageType} with isolation level {IsolationLevel}")]
	private partial void LogStartingTransactionWithIsolationLevel(string messageType, IsolationLevel isolationLevel);

	[LoggerMessage(MiddlewareEventId.TransactionCommitted, LogLevel.Debug,
		"Transaction committed successfully for message {MessageType}")]
	private partial void LogTransactionCommittedSuccessfully(string messageType);

	[LoggerMessage(MiddlewareEventId.TransactionRolledBack, LogLevel.Warning,
		"Transaction rolled back due to unsuccessful result for message {MessageType}")]
	private partial void LogTransactionRolledBackUnsuccessfulResult(string messageType);

	[LoggerMessage(MiddlewareEventId.TransactionRolledBack + 1, LogLevel.Error,
		"Transaction rolled back due to exception during message processing for {MessageType}")]
	private partial void LogTransactionRolledBackException(string messageType, Exception ex);

	[LoggerMessage(MiddlewareEventId.TransactionRolledBack + 2, LogLevel.Error,
		"Error occurred during transaction rollback for message {MessageType}")]
	private partial void LogErrorDuringTransactionRollback(string messageType, Exception ex);

	/// <summary>
	/// Determines if a message requires transaction management.
	/// </summary>
	private bool RequiresTransaction(IDispatchMessage message)
	{
		var messageType = message.GetType();

		// Check for explicit bypass attributes
		if (messageType.GetCustomAttributes(typeof(NoTransactionAttribute), inherit: true).Length != 0)
		{
			return false;
		}

		// Check if message type is in the bypass list
		if (_options.BypassTransactionForTypes?.Contains(messageType.Name) == true)
		{
			return false;
		}

		// Check for explicit transaction requirement
		if (messageType.GetCustomAttributes(typeof(RequireTransactionAttribute), inherit: true).Length != 0)
		{
			return true;
		}

		// Default: require transaction for actions if enabled
		return _options.RequireTransactionByDefault;
	}

	/// <summary>
	/// Determines the transaction configuration for a message.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Context parameter reserved for future context-based transaction configuration logic")]
	private TransactionConfiguration DetermineTransactionConfiguration(
		IDispatchMessage message,
		IMessageContext context)
	{
		// Check for message-specific transaction attributes
		var messageType = message.GetType();
		var transactionAttr = messageType.GetCustomAttributes(typeof(TransactionAttribute), inherit: true)
			.Cast<TransactionAttribute>()
			.FirstOrDefault();

		var isolationLevel = transactionAttr?.IsolationLevel ?? _options.DefaultIsolationLevel;
		var timeout = transactionAttr?.TimeoutSeconds > 0
			? TimeSpan.FromSeconds(transactionAttr.TimeoutSeconds)
			: _options.DefaultTimeout;

		return new TransactionConfiguration(isolationLevel, timeout, _options.EnableDistributedTransactions);
	}

	/// <summary>
	/// Creates a logging scope with transaction context.
	/// </summary>
	private IDisposable? CreateTransactionLoggingScope(
		IDispatchMessage message,
		TransactionConfiguration config)
	{
		var scopeProperties = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["MessageType"] = message.GetType().Name,
			["TransactionEnabled"] = true,
			["IsolationLevel"] = config.IsolationLevel.ToString(),
			["TransactionTimeout"] = config.Timeout.TotalSeconds,
		};

		return _logger.BeginScope(scopeProperties);
	}

	/// <summary>
	/// Internal structure to hold transaction configuration.
	/// </summary>
	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
	private readonly record struct TransactionConfiguration(
		IsolationLevel IsolationLevel,
		TimeSpan Timeout,
		bool EnableDistributedTransactions);
}
