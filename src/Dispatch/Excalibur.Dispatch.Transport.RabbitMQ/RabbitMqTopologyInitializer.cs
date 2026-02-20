// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

public sealed partial class RabbitMqTopologyInitializer
{
	private const string QuorumQueueType = "quorum";
	private const string QueueTypeKey = "x-queue-type";
	private const string DeadLetterExchangeKey = "x-dead-letter-exchange";
	private const string DeadLetterRoutingKey = "x-dead-letter-routing-key";
	private const string MessageTtlKey = "x-message-ttl";

	private readonly RabbitMqOptions _options;
	private readonly RabbitMqCloudEventOptions? _cloudEventOptions;
	private readonly ILogger<RabbitMqTopologyInitializer>? _logger;
	private int _initialized;

	public RabbitMqTopologyInitializer(
		RabbitMqOptions options,
		RabbitMqCloudEventOptions? cloudEventOptions,
		ILogger<RabbitMqTopologyInitializer>? logger = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_cloudEventOptions = cloudEventOptions;
		_logger = logger;
	}

	public async Task EnsureInitializedAsync(IChannel channel, CancellationToken cancellationToken)
	{
		if (Interlocked.Exchange(ref _initialized, 1) == 1)
		{
			return;
		}

		ArgumentNullException.ThrowIfNull(channel);

		var exchangeName = ResolveExchangeName();
		var exchangeType = ResolveExchangeType();

		if (!string.IsNullOrWhiteSpace(exchangeName))
		{
			await channel
				.ExchangeDeclareAsync(
					exchange: exchangeName,
					type: exchangeType,
					durable: _cloudEventOptions?.DurableExchanges ?? true,
					autoDelete: false,
					arguments: null,
					cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			if (_logger is not null)
			{
				LogExchangeDeclared(_logger, exchangeName, exchangeType);
			}
		}

		var queueName = ResolveQueueName();
		if (string.IsNullOrWhiteSpace(queueName))
		{
			return;
		}

		var queueArguments = BuildQueueArguments();

		_ = await channel
			.QueueDeclareAsync(
				queue: queueName,
				durable: _options.QueueDurable,
				exclusive: _options.QueueExclusive,
				autoDelete: _options.QueueAutoDelete,
				arguments: queueArguments,
				cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		if (_logger is not null)
		{
			LogQueueDeclared(_logger, queueName, _options.QueueDurable, _options.QueueAutoDelete);
		}

		if (!string.IsNullOrWhiteSpace(exchangeName))
		{
			await channel
				.QueueBindAsync(
					queue: queueName,
					exchange: exchangeName,
					routingKey: _options.RoutingKey ?? string.Empty,
					arguments: null,
					cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			if (_logger is not null)
			{
				LogBindingCreated(_logger, queueName, exchangeName, _options.RoutingKey ?? string.Empty);
			}
		}

		await EnsureDeadLetterExchangeAsync(channel, cancellationToken).ConfigureAwait(false);
	}

	[LoggerMessage(RabbitMqEventId.ExchangeDeclared, LogLevel.Information,
		"RabbitMQ exchange declared: {ExchangeName} ({ExchangeType})")]
	private static partial void LogExchangeDeclared(
		ILogger logger,
		string exchangeName,
		string exchangeType);

	[LoggerMessage(RabbitMqEventId.QueueDeclared, LogLevel.Information,
		"RabbitMQ queue declared: {QueueName} (Durable={Durable}, AutoDelete={AutoDelete})")]
	private static partial void LogQueueDeclared(
		ILogger logger,
		string queueName,
		bool durable,
		bool autoDelete);

	[LoggerMessage(RabbitMqEventId.BindingCreated, LogLevel.Information,
		"RabbitMQ binding created: Queue={QueueName}, Exchange={ExchangeName}, RoutingKey={RoutingKey}")]
	private static partial void LogBindingCreated(
		ILogger logger,
		string queueName,
		string exchangeName,
		string routingKey);

	private string ResolveExchangeName() =>
		!string.IsNullOrWhiteSpace(_options.Exchange)
			? _options.Exchange
			: _cloudEventOptions?.DefaultExchange ?? string.Empty;

	private string ResolveQueueName() =>
		!string.IsNullOrWhiteSpace(_options.QueueName)
			? _options.QueueName
			: _cloudEventOptions?.DefaultQueue ?? string.Empty;

	private string ResolveExchangeType()
	{
		var exchangeType = _cloudEventOptions?.ExchangeType ?? RabbitMqExchangeType.Topic;
		return exchangeType switch
		{
			RabbitMqExchangeType.Direct => ExchangeType.Direct,
			RabbitMqExchangeType.Fanout => ExchangeType.Fanout,
			RabbitMqExchangeType.Headers => ExchangeType.Headers,
			_ => ExchangeType.Topic,
		};
	}

	private Dictionary<string, object?> BuildQueueArguments()
	{
		var arguments = new Dictionary<string, object?>(_options.QueueArguments, StringComparer.Ordinal);

		if (_cloudEventOptions?.UseQuorumQueues == true &&
			!arguments.ContainsKey(QueueTypeKey))
		{
			arguments[QueueTypeKey] = QuorumQueueType;
		}

		var deadLetterExchange = ResolveDeadLetterExchange();
		if (!string.IsNullOrWhiteSpace(deadLetterExchange) &&
			!arguments.ContainsKey(DeadLetterExchangeKey))
		{
			arguments[DeadLetterExchangeKey] = deadLetterExchange;
		}

		if (!string.IsNullOrWhiteSpace(_options.DeadLetterRoutingKey) &&
			!arguments.ContainsKey(DeadLetterRoutingKey))
		{
			arguments[DeadLetterRoutingKey] = _options.DeadLetterRoutingKey;
		}

		if (_cloudEventOptions?.MessageTtl is { } ttl &&
			ttl > TimeSpan.Zero &&
			!arguments.ContainsKey(MessageTtlKey))
		{
			arguments[MessageTtlKey] = (long)ttl.TotalMilliseconds;
		}

		return arguments;
	}

	private string? ResolveDeadLetterExchange()
	{
		if (_options.EnableDeadLetterExchange && !string.IsNullOrWhiteSpace(_options.DeadLetterExchange))
		{
			return _options.DeadLetterExchange;
		}

		if (_cloudEventOptions?.EnableDeadLetterExchange == true &&
			!string.IsNullOrWhiteSpace(_cloudEventOptions.DeadLetterExchange))
		{
			return _cloudEventOptions.DeadLetterExchange;
		}

		return null;
	}

	private async Task EnsureDeadLetterExchangeAsync(IChannel channel, CancellationToken cancellationToken)
	{
		var deadLetterExchange = ResolveDeadLetterExchange();
		if (string.IsNullOrWhiteSpace(deadLetterExchange))
		{
			return;
		}

		await channel
			.ExchangeDeclareAsync(
				exchange: deadLetterExchange,
				type: ExchangeType.Direct,
				durable: _cloudEventOptions?.DurableExchanges ?? true,
				autoDelete: false,
				arguments: null,
				cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		if (_logger is not null)
		{
			LogExchangeDeclared(_logger, deadLetterExchange, ExchangeType.Direct);
		}
	}
}
