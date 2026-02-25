// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// AWS SQS mapping context implementation.
/// </summary>
public sealed class AwsSqsMappingContext : IAwsSqsMappingContext
{
	private readonly Dictionary<string, (string Value, string DataType)> _attributes = new(StringComparer.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public string? QueueUrl { get; set; }

	/// <inheritdoc/>
	public string? MessageGroupId { get; set; }

	/// <inheritdoc/>
	public string? MessageDeduplicationId { get; set; }

	/// <inheritdoc/>
	public int? DelaySeconds { get; set; }

	/// <summary>
	/// Gets all configured attributes.
	/// </summary>
	public IReadOnlyDictionary<string, (string Value, string DataType)> Attributes => _attributes;

	/// <inheritdoc/>
	public void SetAttribute(string name, string value, string dataType = "String")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		_attributes[name] = (value, dataType);
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
			context.SetTransportProperty($"aws.sqs.{attr.Key}", attr.Value.Value);
		}
	}
}
