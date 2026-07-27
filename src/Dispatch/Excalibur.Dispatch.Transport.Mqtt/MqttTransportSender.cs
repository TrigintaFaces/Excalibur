// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MQTTnet;
using MQTTnet.Protocol;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// Publishes messages to an MQTT topic at the configured quality-of-service level. Connects lazily on the
/// first send and reuses the connection.
/// </summary>
internal sealed partial class MqttTransportSender : ITransportSender
{
	private readonly IMqttConnectionProvider _connectionProvider;
	private readonly MqttOptions _options;
	private readonly ILogger<MqttTransportSender> _logger;
	private readonly SemaphoreSlim _connectGate = new(1, 1);
	private IMqttClient? _client;
	private volatile bool _disposed;

	public MqttTransportSender(
		IMqttConnectionProvider connectionProvider,
		MqttOptions options,
		ILogger<MqttTransportSender> logger)
	{
		_connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Destination => _options.Topic;

	/// <inheritdoc />
	public async Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var client = await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

		var applicationMessage = new MqttApplicationMessageBuilder()
			.WithTopic(_options.Topic)
			.WithPayload(message.Body.ToArray())
			.WithQualityOfServiceLevel(MapQos(_options.QualityOfService))
			.WithCorrelationData(message.CorrelationId is null ? null : System.Text.Encoding.UTF8.GetBytes(message.CorrelationId))
			.WithResponseTopic(_options.ResponseTopic)
			.Build();

		var result = await client.PublishAsync(applicationMessage, cancellationToken).ConfigureAwait(false);
		if (result.IsSuccess)
		{
			return SendResult.Success(message.Id);
		}

		LogPublishFailed(message.Id, _options.Topic, result.ReasonCode.ToString());
		return SendResult.Failure(new SendError
		{
			Code = result.ReasonCode.ToString(),
			Message = $"MQTT publish rejected with reason code {result.ReasonCode}.",
			IsRetryable = true,
		});
	}

	/// <inheritdoc />
	public async Task<BatchSendResult> SendBatchAsync(
		IReadOnlyList<TransportMessage> messages,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);

		var results = new List<SendResult>(messages.Count);
		foreach (var message in messages)
		{
			cancellationToken.ThrowIfCancellationRequested();
			results.Add(await SendAsync(message, cancellationToken).ConfigureAwait(false));
		}

		var successCount = results.Count(static r => r.IsSuccess);
		return new BatchSendResult
		{
			TotalMessages = messages.Count,
			SuccessCount = successCount,
			FailureCount = messages.Count - successCount,
			Results = results,
		};
	}

	/// <inheritdoc />
	public Task FlushAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return _client is not null && serviceType.IsInstanceOfType(_client) ? _client : null;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		_client?.Dispose();
		_connectGate.Dispose();
		return ValueTask.CompletedTask;
	}

	private async Task<IMqttClient> EnsureConnectedAsync(CancellationToken cancellationToken)
	{
		if (_client is { IsConnected: true })
		{
			return _client;
		}

		await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_client is { IsConnected: true })
			{
				return _client;
			}

			_client ??= _connectionProvider.CreateClient();
			if (!_client.IsConnected)
			{
				await _client.ConnectAsync(_connectionProvider.BuildClientOptions("pub"), cancellationToken).ConfigureAwait(false);
			}

			return _client;
		}
		finally
		{
			_ = _connectGate.Release();
		}
	}

	internal static MqttQualityOfServiceLevel MapQos(MqttQualityOfService qos) => qos switch
	{
		MqttQualityOfService.AtMostOnce => MqttQualityOfServiceLevel.AtMostOnce,
		MqttQualityOfService.AtLeastOnce => MqttQualityOfServiceLevel.AtLeastOnce,
		MqttQualityOfService.ExactlyOnce => MqttQualityOfServiceLevel.ExactlyOnce,
		_ => MqttQualityOfServiceLevel.AtLeastOnce,
	};

	[LoggerMessage(EventId = 6120, Level = LogLevel.Error,
		Message = "MQTT publish failed for message {MessageId} to {Topic} (reason {ReasonCode}).")]
	private partial void LogPublishFailed(string messageId, string topic, string reasonCode);
}
