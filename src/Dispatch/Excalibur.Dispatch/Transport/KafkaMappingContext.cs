// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Kafka mapping context implementation.
/// </summary>
public sealed class KafkaMappingContext : IKafkaMappingContext
{
	private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public string? Topic { get; set; }

	/// <inheritdoc/>
	public string? Key { get; set; }

	/// <inheritdoc/>
	public int? Partition { get; set; }

	/// <inheritdoc/>
	public int? SchemaId { get; set; }

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
	/// Applies this configuration to a Kafka message context.
	/// </summary>
	/// <param name="context">The context to apply configuration to.</param>
	public void ApplyTo(KafkaMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (Topic is not null)
		{
			context.Topic = Topic;
		}

		if (Key is not null)
		{
			context.Key = Key;
		}

		if (Partition.HasValue)
		{
			context.Partition = Partition.Value;
		}

		foreach (var header in _headers)
		{
			context.SetHeader(header.Key, header.Value);
		}
	}
}
