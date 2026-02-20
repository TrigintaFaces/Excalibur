// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Azure Service Bus mapping context implementation.
/// </summary>
public sealed class AzureServiceBusMappingContext : IAzureServiceBusMappingContext
{
	private readonly Dictionary<string, object> _properties = new(StringComparer.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public string? TopicOrQueueName { get; set; }

	/// <inheritdoc/>
	public string? SessionId { get; set; }

	/// <inheritdoc/>
	public string? PartitionKey { get; set; }

	/// <inheritdoc/>
	public string? ReplyToSessionId { get; set; }

	/// <inheritdoc/>
	public TimeSpan? TimeToLive { get; set; }

	/// <inheritdoc/>
	public DateTimeOffset? ScheduledEnqueueTime { get; set; }

	/// <summary>
	/// Gets all configured properties.
	/// </summary>
	public IReadOnlyDictionary<string, object> Properties => _properties;

	/// <inheritdoc/>
	public void SetProperty(string key, object value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_properties[key] = value;
	}

	/// <summary>
	/// Applies this configuration to a transport message context.
	/// </summary>
	/// <param name="context">The context to apply configuration to.</param>
	public void ApplyTo(TransportMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		foreach (var property in _properties)
		{
			context.SetTransportProperty(property.Key, property.Value);
		}
	}
}
