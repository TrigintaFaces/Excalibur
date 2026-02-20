// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// RabbitMQ mapping context implementation.
/// </summary>
public sealed class RabbitMqMappingContext : IRabbitMqMappingContext
{
	private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public string? Exchange { get; set; }

	/// <inheritdoc/>
	public string? RoutingKey { get; set; }

	/// <inheritdoc/>
	public byte? Priority { get; set; }

	/// <inheritdoc/>
	public string? ReplyTo { get; set; }

	/// <inheritdoc/>
	public string? Expiration { get; set; }

	/// <inheritdoc/>
	public byte? DeliveryMode { get; set; }

	/// <summary>
	/// Gets all configured headers.
	/// </summary>
	public IReadOnlyDictionary<string, string> Headers => _headers;

	/// <inheritdoc/>
	public void SetHeader(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_headers[key] = value;
	}

	/// <summary>
	/// Applies this configuration to a RabbitMQ message context.
	/// </summary>
	/// <param name="context">The context to apply configuration to.</param>
	public void ApplyTo(RabbitMqMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (Exchange is not null)
		{
			context.Exchange = Exchange;
		}

		if (RoutingKey is not null)
		{
			context.RoutingKey = RoutingKey;
		}

		if (Priority.HasValue)
		{
			context.Priority = Priority.Value;
		}

		if (ReplyTo is not null)
		{
			context.ReplyTo = ReplyTo;
		}

		if (Expiration is not null)
		{
			context.Expiration = Expiration;
		}

		if (DeliveryMode.HasValue)
		{
			context.DeliveryMode = DeliveryMode.Value;
		}

		foreach (var header in _headers)
		{
			context.SetHeader(header.Key, header.Value);
		}
	}
}
