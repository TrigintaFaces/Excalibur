// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Base implementation of <see cref="ITransportMessageContext"/> providing transport-agnostic message context.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a default implementation for the transport message context interface.
/// Transport-specific implementations can extend this class to add transport-specific properties
/// and behavior while inheriting the common functionality.
/// </para>
/// </remarks>
public class TransportMessageContext : ITransportMessageContext
{
	private readonly ConcurrentDictionary<string, object?> _transportProperties = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportMessageContext"/> class.
	/// </summary>
	/// <param name="messageId">The unique message identifier.</param>
	public TransportMessageContext(string messageId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		MessageId = messageId;
		Timestamp = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportMessageContext"/> class with a generated message ID.
	/// </summary>
	public TransportMessageContext()
		: this(Guid.NewGuid().ToString("N"))
	{
	}

	/// <inheritdoc/>
	public string MessageId { get; }

	/// <inheritdoc/>
	public string? CorrelationId { get; set; }

	/// <inheritdoc/>
	public string? CausationId { get; set; }

	/// <inheritdoc/>
	public string? SourceTransport { get; set; }

	/// <inheritdoc/>
	public string? TargetTransport { get; set; }

	/// <inheritdoc/>
	public IReadOnlyDictionary<string, string> Headers => _headers;

	/// <inheritdoc/>
	public DateTimeOffset Timestamp { get; set; }

	/// <inheritdoc/>
	public string? ContentType { get; set; }

	/// <inheritdoc/>
	public T? GetTransportProperty<T>(string propertyName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

		if (_transportProperties.TryGetValue(propertyName, out var value) && value is T typedValue)
		{
			return typedValue;
		}

		return default;
	}

	/// <inheritdoc/>
	public void SetTransportProperty<T>(string propertyName, T value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
		_transportProperties[propertyName] = value;
	}

	/// <inheritdoc/>
	public bool HasTransportProperty(string propertyName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
		return _transportProperties.ContainsKey(propertyName);
	}

	/// <inheritdoc/>
	public IReadOnlyDictionary<string, object?> GetAllTransportProperties()
		=> _transportProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Sets a header value.
	/// </summary>
	/// <param name="name">The header name.</param>
	/// <param name="value">The header value.</param>
	public void SetHeader(string name, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		_headers[name] = value;
	}

	/// <summary>
	/// Sets multiple headers at once.
	/// </summary>
	/// <param name="headers">The headers to set.</param>
	public void SetHeaders(IEnumerable<KeyValuePair<string, string>> headers)
	{
		ArgumentNullException.ThrowIfNull(headers);

		foreach (var header in headers)
		{
			_headers[header.Key] = header.Value;
		}
	}

	/// <summary>
	/// Removes a header by name.
	/// </summary>
	/// <param name="name">The header name to remove.</param>
	/// <returns><see langword="true"/> if the header was removed; otherwise, <see langword="false"/>.</returns>
	public bool RemoveHeader(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		return _headers.Remove(name);
	}
}
