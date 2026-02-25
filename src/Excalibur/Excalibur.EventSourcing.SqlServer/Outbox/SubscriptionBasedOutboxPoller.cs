// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Outbox;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.SqlServer.Outbox;

/// <summary>
/// SQL Server outbox poller that supports both fixed-interval polling and
/// SQL Server Query Notifications for event-driven message retrieval.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="SubscriptionPollingOptions.UseQueryNotifications"/> is enabled,
/// the poller subscribes to SQL Server Query Notifications via SqlDependency to receive
/// immediate notification when new outbox messages are inserted. This reduces latency
/// compared to fixed-interval polling while maintaining reliability through fallback polling.
/// </para>
/// <para>
/// Follows the Microsoft BackgroundService pattern with Start/Stop lifecycle.
/// </para>
/// </remarks>
public sealed partial class SubscriptionBasedOutboxPoller : IAsyncDisposable
{
	private readonly SubscriptionPollingOptions _options;
	private readonly IEventSourcedOutboxStore _outboxStore;
	private readonly ILogger<SubscriptionBasedOutboxPoller> _logger;
	private readonly Func<IReadOnlyList<OutboxMessage>, CancellationToken, Task>? _onMessagesReceived;
	private readonly SemaphoreSlim _notificationSignal = new(0, int.MaxValue);

	private volatile bool _disposed;
	private volatile bool _running;
	private bool _sqlDependencyStarted;

	/// <summary>
	/// Initializes a new instance of the <see cref="SubscriptionBasedOutboxPoller"/> class.
	/// </summary>
	/// <param name="options"> The polling configuration options. </param>
	/// <param name="outboxStore"> The outbox store to poll messages from. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="onMessagesReceived"> Optional callback when messages are retrieved. </param>
	public SubscriptionBasedOutboxPoller(
		IOptions<SubscriptionPollingOptions> options,
		IEventSourcedOutboxStore outboxStore,
		ILogger<SubscriptionBasedOutboxPoller> logger,
		Func<IReadOnlyList<OutboxMessage>, CancellationToken, Task>? onMessagesReceived = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(outboxStore);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_outboxStore = outboxStore;
		_logger = logger;
		_onMessagesReceived = onMessagesReceived;
	}

	/// <summary>
	/// Starts the subscription-based outbox poller.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous start operation. </returns>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_running)
		{
			return Task.CompletedTask;
		}

		LogPollerStarting(_options.TableName, _options.UseQueryNotifications);

		if (_options.UseQueryNotifications && !string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			try
			{
				SqlDependency.Start(_options.ConnectionString);
				_sqlDependencyStarted = true;
				LogQueryNotificationsEnabled();
			}
			catch (Exception ex)
			{
				LogQueryNotificationsFailed(ex);
				// Fall back to polling
			}
		}

		_running = true;
		LogPollerStarted(_options.TableName);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Executes a single polling cycle, retrieving pending messages from the outbox.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> The messages retrieved during this polling cycle. </returns>
	public async Task<IReadOnlyList<OutboxMessage>> PollAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_running)
		{
			return Array.Empty<OutboxMessage>();
		}

		// Wait for notification signal or poll interval timeout
		if (_sqlDependencyStarted)
		{
			try
			{
				_ = await _notificationSignal.WaitAsync(_options.PollInterval, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return Array.Empty<OutboxMessage>();
			}
		}
		else
		{
			try
			{
				await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return Array.Empty<OutboxMessage>();
			}
		}

		var messages = await _outboxStore.GetPendingAsync(_options.BatchSize, cancellationToken).ConfigureAwait(false);

		if (messages.Count > 0)
		{
			LogMessagesRetrieved(messages.Count);

			if (_onMessagesReceived != null)
			{
				await _onMessagesReceived(messages, cancellationToken).ConfigureAwait(false);
			}
		}

		// Re-subscribe for query notifications after processing
		if (_sqlDependencyStarted)
		{
			SubscribeToNotifications();
		}

		return messages;
	}

	/// <summary>
	/// Stops the subscription-based outbox poller.
	/// </summary>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous stop operation. </returns>
	public Task StopAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!_running)
		{
			return Task.CompletedTask;
		}

		LogPollerStopping(_options.TableName);

		if (_sqlDependencyStarted && !string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			try
			{
				SqlDependency.Stop(_options.ConnectionString);
				_sqlDependencyStarted = false;
			}
			catch (Exception ex)
			{
				LogQueryNotificationsStopFailed(ex);
			}
		}

		_running = false;
		LogPollerStopped(_options.TableName);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_running)
		{
			await StopAsync(CancellationToken.None).ConfigureAwait(false);
		}

		_notificationSignal.Dispose();
	}

	private void SubscribeToNotifications()
	{
		if (string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			return;
		}

		try
		{
			using var connection = new SqlConnection(_options.ConnectionString);
			connection.Open();

			var queryText = $"SELECT [Id] FROM [{_options.SchemaName}].[{_options.TableName}] WHERE [PublishedAt] IS NULL";

#pragma warning disable CA2100 // SchemaName and TableName are validated via options; not user input
			using var command = new SqlCommand(queryText, connection);
#pragma warning restore CA2100
			command.Notification = null;

			var dependency = new SqlDependency(command, null, (int)_options.NotificationTimeout.TotalSeconds);
			dependency.OnChange += OnSqlDependencyChange;

			using var reader = command.ExecuteReader();
			// We just need to execute the query to register the notification
		}
		catch (Exception ex)
		{
			LogQueryNotificationSubscriptionFailed(ex);
		}
	}

	private void OnSqlDependencyChange(object sender, SqlNotificationEventArgs e)
	{
		if (e.Type == SqlNotificationType.Change)
		{
			LogNotificationReceived(e.Info.ToString());

			try
			{
				_ = _notificationSignal.Release();
			}
			catch (SemaphoreFullException)
			{
				// Signal already pending
			}
		}
	}

	[LoggerMessage(113250, LogLevel.Information,
		"Starting subscription-based outbox poller for table '{TableName}' (queryNotifications={UseQueryNotifications})")]
	private partial void LogPollerStarting(string tableName, bool useQueryNotifications);

	[LoggerMessage(113251, LogLevel.Information,
		"Subscription-based outbox poller started for table '{TableName}'")]
	private partial void LogPollerStarted(string tableName);

	[LoggerMessage(113252, LogLevel.Information,
		"Stopping subscription-based outbox poller for table '{TableName}'")]
	private partial void LogPollerStopping(string tableName);

	[LoggerMessage(113253, LogLevel.Information,
		"Subscription-based outbox poller stopped for table '{TableName}'")]
	private partial void LogPollerStopped(string tableName);

	[LoggerMessage(113254, LogLevel.Information,
		"SQL Server Query Notifications enabled for outbox polling")]
	private partial void LogQueryNotificationsEnabled();

	[LoggerMessage(113255, LogLevel.Warning,
		"Failed to enable SQL Server Query Notifications, falling back to interval polling")]
	private partial void LogQueryNotificationsFailed(Exception ex);

	[LoggerMessage(113256, LogLevel.Warning,
		"Failed to stop SQL Server Query Notifications")]
	private partial void LogQueryNotificationsStopFailed(Exception ex);

	[LoggerMessage(113257, LogLevel.Warning,
		"Failed to subscribe to SQL Server Query Notifications")]
	private partial void LogQueryNotificationSubscriptionFailed(Exception ex);

	[LoggerMessage(113258, LogLevel.Debug,
		"Retrieved {MessageCount} pending outbox messages")]
	private partial void LogMessagesRetrieved(int messageCount);

	[LoggerMessage(113259, LogLevel.Debug,
		"SQL Server Query Notification received: {NotificationInfo}")]
	private partial void LogNotificationReceived(string notificationInfo);
}
