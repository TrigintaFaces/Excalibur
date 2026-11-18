// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using CloudNative.CloudEvents;

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
	private readonly ICloudEventEncoder<MqttApplicationMessage>? _cloudEventEncoder;
	private readonly SemaphoreSlim _connectGate = new(1, 1);
	private IMqttClient? _client;
	private volatile bool _disposed;

	public MqttTransportSender(
		IMqttConnectionProvider connectionProvider,
		MqttOptions options,
		ILogger<MqttTransportSender> logger,
		ICloudEventEncoder<MqttApplicationMessage>? cloudEventEncoder = null)
	{
		_connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_cloudEventEncoder = cloudEventEncoder;
	}

	/// <inheritdoc />
	public string Destination => _options.Topic;

	/// <inheritdoc />
	public async Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var client = await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
		var applicationMessage = await CreateApplicationMessageAsync(message, cancellationToken).ConfigureAwait(false);

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

	private async ValueTask<MqttApplicationMessage> CreateApplicationMessageAsync(
		TransportMessage message,
		CancellationToken cancellationToken)
	{
		if (_cloudEventEncoder is not null && message.HasProperties
			&& message.Properties.TryGetValue("cloudevent", out var marker) && marker is CloudEvent cloudEvent)
		{
			var encoded = await _cloudEventEncoder.ToTransportMessageAsync(
				cloudEvent, _cloudEventEncoder.Options.DefaultMode, cancellationToken).ConfigureAwait(false);
			encoded.Topic = _options.Topic;
			encoded.QualityOfServiceLevel = MapQos(_options.QualityOfService);
			encoded.CorrelationData = message.CorrelationId is null
				? null
				: System.Text.Encoding.UTF8.GetBytes(message.CorrelationId);
			encoded.ResponseTopic = _options.ResponseTopic;
			AppendApplicationProperties(encoded, message, cloudEventEncoded: true);
			return encoded;
		}

		var builder = new MqttApplicationMessageBuilder()
			.WithTopic(_options.Topic)
			.WithPayload(message.Body.ToArray())
			.WithQualityOfServiceLevel(MapQos(_options.QualityOfService))
			.WithCorrelationData(message.CorrelationId is null ? null : System.Text.Encoding.UTF8.GetBytes(message.CorrelationId))
			.WithResponseTopic(_options.ResponseTopic);

		if (!string.IsNullOrEmpty(message.ContentType))
		{
			builder = builder.WithContentType(message.ContentType);
		}

		var applicationMessage = builder.Build();
		AppendApplicationProperties(applicationMessage, message, cloudEventEncoded: false);
		return applicationMessage;
	}

	private static void AppendApplicationProperties(
		MqttApplicationMessage applicationMessage,
		TransportMessage message,
		bool cloudEventEncoded)
	{
		if (!message.HasProperties)
		{
			return;
		}

		// Protect only the encoder's original properties. Properties appended below are application
		// properties, whose case-distinct names must remain distinct on the MQTT wire.
		var encodedPropertyCount = cloudEventEncoded ? applicationMessage.UserProperties?.Count ?? 0 : 0;
		foreach (var (name, value) in message.Properties)
		{
			if (value is null || (cloudEventEncoded &&
				(name.Equals("cloudevent", StringComparison.OrdinalIgnoreCase)
					|| IsCloudEventAttributeName(name)
					|| HasEncodedProperty(applicationMessage, encodedPropertyCount, name))))
			{
				continue;
			}

			// CloudEvent attributes come only from the encoder. Ordinary application properties remain
			// available, but cannot replace its identity, introduce absent standard attributes, or leak
			// the in-process CloudEvent marker onto the wire.
			applicationMessage.UserProperties ??= [];
			applicationMessage.UserProperties.Add(new MQTTnet.Packets.MqttUserProperty(name, value as string ?? value.ToString()));
		}
	}

	private static bool HasEncodedProperty(MqttApplicationMessage message, int encodedPropertyCount, string name)
	{
		for (var index = 0; index < encodedPropertyCount; index++)
		{
			if (string.Equals(message.UserProperties[index].Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsCloudEventAttributeName(string name) =>
		name.Equals("specversion", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("id", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("source", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("type", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("datacontenttype", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("dataschema", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("subject", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("time", StringComparison.OrdinalIgnoreCase);

	/// <inheritdoc />
	/// <remarks>
	/// Cancellation stops the batch; it does not discard what the batch already knows. Every input still
	/// gets a result, because a caller who is told only "cancelled" cannot tell which messages were sent
	/// and has no safe move left but to resend all of them — which on an at-least-once transport
	/// duplicates every message that already arrived. Messages not reached, and the one in flight when
	/// cancellation landed, are reported as retryable failures: their delivery is unknown, and a duplicate
	/// is recoverable where a silent loss is not.
	/// </remarks>
	public async Task<BatchSendResult> SendBatchAsync(
		IReadOnlyList<TransportMessage> messages,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);

		var results = new List<SendResult>(messages.Count);
		foreach (var message in messages)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			try
			{
				results.Add(await SendAsync(message, cancellationToken).ConfigureAwait(false));
			}
			catch (OperationCanceledException)
			{
				// The publish observed the same token. Stop here and report the rest below.
				break;
			}
		}

		// Cancellation is reported per input rather than thrown, so the caller can retry precisely the
		// messages whose delivery is unknown. See BatchSendResult.Results for the contract.
		while (results.Count < messages.Count)
		{
			results.Add(SendResult.Failure(new SendError
			{
				Code = "Canceled",
				Message = "The batch was cancelled; this message is not confirmed sent.",
				IsRetryable = true,
			}));
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
