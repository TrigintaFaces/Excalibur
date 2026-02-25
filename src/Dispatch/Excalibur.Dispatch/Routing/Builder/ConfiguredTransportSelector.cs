// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Routing.Builder;

/// <summary>
/// Transport selector implementation that uses the fluent builder configuration.
/// </summary>
/// <remarks>
/// This implementation evaluates transport routing rules configured via the
/// <see cref="IRoutingBuilder"/> fluent API and uses caching for performance.
/// </remarks>
internal sealed class ConfiguredTransportSelector : ITransportSelector
{
	private readonly RoutingConfiguration _configuration;
	private readonly ConcurrentDictionary<Type, string> _typeToTransportCache = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfiguredTransportSelector"/> class.
	/// </summary>
	/// <param name="configuration">The routing configuration.</param>
	public ConfiguredTransportSelector(RoutingConfiguration configuration)
	{
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
	}

	/// <inheritdoc/>
	public ValueTask<string> SelectTransportAsync(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = message.GetType();

		// Try cache first for unconditional rules
		if (_typeToTransportCache.TryGetValue(messageType, out var cached))
		{
			return new ValueTask<string>(cached);
		}

		// Evaluate rules in order
		foreach (var rule in _configuration.TransportRules)
		{
			if (!rule.MessageType.IsAssignableFrom(messageType))
			{
				continue;
			}

			// If there's a predicate, evaluate it
			if (rule.Predicate is not null)
			{
				if (rule.Predicate(message, context))
				{
					return new ValueTask<string>(rule.Transport);
				}
			}
			else
			{
				// Unconditional rule - cache it for this type
				_typeToTransportCache.TryAdd(messageType, rule.Transport);
				return new ValueTask<string>(rule.Transport);
			}
		}

		// Return default transport
		return new ValueTask<string>(_configuration.DefaultTransport);
	}

	/// <inheritdoc/>
	public IEnumerable<string> GetAvailableTransports(Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);

		var transports = new HashSet<string> { _configuration.DefaultTransport };

		foreach (var rule in _configuration.TransportRules)
		{
			if (rule.MessageType.IsAssignableFrom(messageType))
			{
				transports.Add(rule.Transport);
			}
		}

		return transports;
	}
}
