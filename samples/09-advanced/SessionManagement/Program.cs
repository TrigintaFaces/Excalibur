// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

using MessageSystemAttributeName = Amazon.SQS.MessageSystemAttributeName;

namespace SessionManagementExample;

/// <summary>
///     Example demonstrating AWS Session Management with SQS FIFO.
/// </summary>
public static class Program
{
	public static async Task Main(string[] args)
	{
		var host = Host.CreateDefaultBuilder(args)
			.ConfigureServices(static (context, services) =>
			{
				// Configure AWS
				_ = services.AddDefaultAWSOptions(context.Configuration.GetAWSOptions());
				_ = services.AddAWSService<IAmazonSQS>();

				// Register session management services
				_ = services.AddSingleton<Excalibur.Dispatch.Transport.Aws.ISessionStore, InMemorySessionStore>();
				_ = services.AddSingleton<ISessionManager, SessionManager>();

				// Configure session options
				_ = services.Configure<Excalibur.Dispatch.Transport.Aws.SessionOptions>(static options =>
				{
					options.SessionTimeout = TimeSpan.FromMinutes(5);
					options.MaxConcurrentSessions = 100;
					options.EnableAutoRenewal = true;
					options.LockTimeout = TimeSpan.FromMinutes(2);
				});

				// Session management is handled by the AWS provider

				// Register our message processor
				_ = services.AddHostedService<OrderProcessingService>();
			})
			.Build();

		await host.RunAsync().ConfigureAwait(false);
	}
}

/// <summary>
///     Background service that processes orders with session affinity.
/// </summary>
public class OrderProcessingService(
	ISessionManager sessionManager,
	IAmazonSQS sqsClient,
	ILogger<OrderProcessingService> logger) : BackgroundService
{
	private readonly ISessionManager _sessionManager = sessionManager;
	private readonly IAmazonSQS _sqsClient = sqsClient;
	private readonly ILogger<OrderProcessingService> _logger = logger;
	private readonly string _queueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/orders.fifo";

	/// <inheritdoc/>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Receive messages from SQS FIFO
				var request = new ReceiveMessageRequest
				{
					QueueUrl = _queueUrl,
					MaxNumberOfMessages = 10,
					WaitTimeSeconds = 20,
					MessageSystemAttributeNames = { MessageSystemAttributeName.All }
				};

				var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken).ConfigureAwait(false);

				// Process messages with session affinity
				foreach (var message in response.Messages)
				{
					await ProcessMessageWithSession(message, stoppingToken).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing messages");
				await Task.Delay(5000, stoppingToken).ConfigureAwait(false);
			}
		}
	}

	private async Task ProcessMessageWithSession(Message message, CancellationToken cancellationToken)
	{
		// Extract session ID from message group ID
		var sessionId = message.Attributes["MessageGroupId"];

		SessionLockToken? sessionLock = null;
		try
		{
			// Try to acquire session lock
			sessionLock = await _sessionManager.TryAcquireLockAsync(
				sessionId,
				TimeSpan.FromMinutes(5),
				cancellationToken);

			if (sessionLock == null)
			{
				_logger.LogWarning("Could not acquire session {SessionId}, requeuing message", sessionId);
				return; // Message will be retried
			}

			_logger.LogInformation(
				"Processing message {MessageId} in session {SessionId}",
				message.MessageId,
				sessionId);

			// Simulate order processing
			await ProcessOrder(message.Body, cancellationToken).ConfigureAwait(false);

			// Delete message after successful processing
			_ = await _sqsClient.DeleteMessageAsync(
				new DeleteMessageRequest { QueueUrl = _queueUrl, ReceiptHandle = message.ReceiptHandle },
				cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Successfully processed message {MessageId} in session {SessionId}",
				message.MessageId,
				sessionId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex,
				"Error processing message {MessageId} in session {SessionId}",
				message.MessageId,
				sessionId);
			throw; // Let message return to queue
		}
		finally
		{
			// Always release session lock
			if (sessionLock != null)
			{
				_ = await _sessionManager.ReleaseLockAsync(sessionLock, cancellationToken);
			}
		}
	}

	private async Task ProcessOrder(string orderJson, CancellationToken cancellationToken) =>
		// Simulate order processing
		await Task.Delay(Random.Shared.Next(100, 500), cancellationToken).ConfigureAwait(false);

	// In real implementation:
	// 1. Deserialize order
	// 2. Validate order
	// 3. Process payment
	// 4. Update inventory
	// 5. Send confirmation
}

/// <summary>
///     Example showing session migration for load balancing.
/// </summary>
public class LoadBalancer(
	ISessionManager sessionManager,
	ILogger<LoadBalancer> logger)
{
	private readonly ISessionManager _sessionManager = sessionManager;
	private readonly ILogger<LoadBalancer> _logger = logger;

	public async Task BalanceLoad()
	{
		// Example: Analyze current session distribution and manage sessions
		try
		{
			_logger.LogInformation("Starting session analysis");

			// In a real implementation, you would:
			// 1. Analyze active sessions
			// 2. Identify load distribution
			// 3. Coordinate session management across workers

			// Example: Get active session information
			var activeSessionIds = new[] { "session-1", "session-2", "session-3" };

			foreach (var sessionId in activeSessionIds)
			{
				try
				{
					// Check if session exists and is active
					var sessionInfo = await _sessionManager.GetSessionInfoAsync(sessionId, CancellationToken.None);
					if (sessionInfo != null)
					{
						_logger.LogInformation("Active session found: {SessionId}", sessionId);
					}
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Error checking session {SessionId}", sessionId);
				}
			}

			_logger.LogInformation("Session analysis completed");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to analyze sessions");
		}
	}
}

/// <summary>
/// Simple in-memory session store for sample usage.
/// </summary>
internal sealed class InMemorySessionStore : Excalibur.Dispatch.Transport.Aws.ISessionStore
{
	private readonly ConcurrentDictionary<string, SessionData> _sessions = new(StringComparer.Ordinal);

	public Task<SessionData> CreateAsync(string sessionId, TimeSpan timeout, CancellationToken cancellationToken = default)
	{
		var now = DateTimeOffset.UtcNow;
		var session = new SessionData
		{
			Id = sessionId,
			State = AwsSessionState.Active,
			CreatedAt = now,
			LastAccessedAt = now,
			ExpiresAt = now.Add(timeout),
		};

		_sessions[sessionId] = session;
		return Task.FromResult(session);
	}

	public Task<SessionData?> TryGetAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		_ = _sessions.TryGetValue(sessionId, out var session);
		return Task.FromResult(session);
	}

	public Task<SessionData> UpdateAsync(SessionData session, CancellationToken cancellationToken = default)
	{
		_sessions[session.Id] = session;
		return Task.FromResult(session);
	}

	public Task CreateOrUpdateAsync(SessionData session, CancellationToken cancellationToken = default)
	{
		_sessions[session.Id] = session;
		return Task.CompletedTask;
	}

	public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		_ = _sessions.TryRemove(sessionId, out _);
		return Task.CompletedTask;
	}

	public Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default) =>
		Task.FromResult(_sessions.ContainsKey(sessionId));

	public Task<int> GetCountAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(_sessions.Count);
}
