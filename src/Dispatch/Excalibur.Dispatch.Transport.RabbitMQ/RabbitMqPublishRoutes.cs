// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>Owns the message destination snapshot for one named publisher.</summary>
internal sealed class RabbitMqPublishRoutes
{
	private readonly Dictionary<Type, string> _exchanges;
	private readonly Dictionary<Type, string> _queues;
	private readonly Dictionary<Type, string> _inferredKeys = [];

	internal RabbitMqPublishRoutes(RabbitMQTopologyOptions topology)
	{
		ArgumentNullException.ThrowIfNull(topology);
		_exchanges = new(topology.ExchangeMappings);
		_queues = new(topology.QueueMappings);
		var types = topology.Exchanges.GroupBy(e => e.Name, StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.Last().Type, StringComparer.Ordinal);
		foreach (var (messageType, exchange) in _exchanges)
		{
			if (!_queues.TryGetValue(messageType, out var queue) || !types.TryGetValue(exchange, out var exchangeType))
			{
				continue;
			}
			if (exchangeType is RabbitMQExchangeType.Fanout or RabbitMQExchangeType.Headers)
			{
				_inferredKeys[messageType] = string.Empty;
				continue;
			}
			var keys = topology.Bindings.Where(b => b.Exchange == exchange && b.Queue == queue)
				.Select(b => b.RoutingKey).Where(key => exchangeType == RabbitMQExchangeType.Direct ||
					!key.Split('.').Any(segment => segment is "*" or "#"))
				.Distinct(StringComparer.Ordinal).ToArray();
			if (keys.Length == 1)
			{
				_inferredKeys[messageType] = keys[0];
			}
		}
	}

	internal (string Exchange, string RoutingKey) Resolve(Type messageType, string defaultExchange, string defaultKey)
	{
		var hasExchange = _exchanges.TryGetValue(messageType, out var exchange);
		var hasQueue = _queues.TryGetValue(messageType, out var queue);
		if (!hasExchange)
		{
			return hasQueue ? (string.Empty, queue!) : (defaultExchange, defaultKey);
		}
		if (!hasQueue || !string.IsNullOrEmpty(defaultKey))
		{
			return (exchange!, defaultKey);
		}
		return _inferredKeys.TryGetValue(messageType, out var key) ? (exchange!, key) : throw new InvalidOperationException(
			$"RabbitMQ message '{messageType.Name}' maps to exchange '{exchange}' and queue '{queue}' without a unique literal binding. Configure an explicit publication routing key.");
	}
}
