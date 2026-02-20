// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Default message mapper that copies context properties between transports.
/// </summary>
/// <remarks>
/// <para>
/// This mapper provides basic property copying between transport contexts.
/// It preserves all common properties (MessageId, CorrelationId, etc.) and
/// allows transport-specific properties to be mapped via configuration.
/// </para>
/// </remarks>
public class DefaultMessageMapper : IMessageMapper
{
	/// <summary>
	/// Gets the wildcard transport identifier for matching any transport.
	/// </summary>
	public const string WildcardTransport = "*";

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultMessageMapper"/> class.
	/// </summary>
	/// <param name="name">The mapper name.</param>
	/// <param name="sourceTransport">The source transport (or "*" for any).</param>
	/// <param name="targetTransport">The target transport (or "*" for any).</param>
	public DefaultMessageMapper(string name, string sourceTransport = WildcardTransport, string targetTransport = WildcardTransport)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceTransport);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetTransport);

		Name = name;
		SourceTransport = sourceTransport;
		TargetTransport = targetTransport;
	}

	/// <inheritdoc/>
	public string Name { get; }

	/// <inheritdoc/>
	public string SourceTransport { get; }

	/// <inheritdoc/>
	public string TargetTransport { get; }

	/// <inheritdoc/>
	public bool CanMap(string sourceTransport, string targetTransport)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceTransport);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetTransport);

		var sourceMatches = SourceTransport == WildcardTransport ||
			string.Equals(SourceTransport, sourceTransport, StringComparison.OrdinalIgnoreCase);

		var targetMatches = TargetTransport == WildcardTransport ||
			string.Equals(TargetTransport, targetTransport, StringComparison.OrdinalIgnoreCase);

		return sourceMatches && targetMatches;
	}

	/// <inheritdoc/>
	public virtual ITransportMessageContext Map(ITransportMessageContext source, string targetTransportName)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetTransportName);

		var target = CreateTargetContext(source.MessageId, targetTransportName);

		// Copy common properties
		target.CorrelationId = source.CorrelationId;
		target.CausationId = source.CausationId;
		target.SourceTransport = source.SourceTransport;
		target.TargetTransport = targetTransportName;
		target.Timestamp = source.Timestamp;
		target.ContentType = source.ContentType;

		// Copy headers
		target.SetHeaders(source.Headers);

		// Copy transport properties (subclasses can override to customize)
		CopyTransportProperties(source, target);

		return target;
	}

	/// <summary>
	/// Creates the appropriate target context for the specified transport.
	/// </summary>
	/// <param name="messageId">The message ID to use.</param>
	/// <param name="targetTransport">The target transport name.</param>
	/// <returns>A new transport message context instance.</returns>
	protected virtual TransportMessageContext CreateTargetContext(string messageId, string targetTransport)
	{
		if (string.Equals(targetTransport, "rabbitmq", StringComparison.OrdinalIgnoreCase))
		{
			return new RabbitMqMessageContext(messageId);
		}

		if (string.Equals(targetTransport, "kafka", StringComparison.OrdinalIgnoreCase))
		{
			return new KafkaMessageContext(messageId);
		}

		return new TransportMessageContext(messageId);
	}

	/// <summary>
	/// Copies transport-specific properties from source to target context.
	/// </summary>
	/// <param name="source">The source context.</param>
	/// <param name="target">The target context.</param>
	/// <remarks>
	/// Override this method to customize how transport properties are copied or transformed.
	/// </remarks>
	protected virtual void CopyTransportProperties(ITransportMessageContext source, TransportMessageContext target)
	{
		// By default, copy all transport properties
		foreach (var property in source.GetAllTransportProperties())
		{
			target.SetTransportProperty(property.Key, property.Value);
		}
	}
}
