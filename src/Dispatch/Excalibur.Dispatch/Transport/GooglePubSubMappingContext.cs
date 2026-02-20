// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Google Pub/Sub mapping context implementation.
/// </summary>
public sealed class GooglePubSubMappingContext : IGooglePubSubMappingContext
{
	private readonly Dictionary<string, string> _attributes = new(StringComparer.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public string? TopicName { get; set; }

	/// <inheritdoc/>
	public string? OrderingKey { get; set; }

	/// <summary>
	/// Gets all configured attributes.
	/// </summary>
	public IReadOnlyDictionary<string, string> Attributes => _attributes;

	/// <inheritdoc/>
	public void SetAttribute(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_attributes[key] = value;
	}

	/// <summary>
	/// Applies this configuration to a transport message context.
	/// </summary>
	/// <param name="context">The context to apply configuration to.</param>
	public void ApplyTo(TransportMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		foreach (var attr in _attributes)
		{
			context.SetTransportProperty($"gcp.pubsub.{attr.Key}", attr.Value);
		}
	}
}
