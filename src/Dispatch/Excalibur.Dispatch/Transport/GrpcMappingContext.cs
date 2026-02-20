// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// gRPC mapping context implementation.
/// </summary>
public sealed class GrpcMappingContext : IGrpcMappingContext
{
	private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public string? MethodName { get; set; }

	/// <inheritdoc/>
	public TimeSpan? Deadline { get; set; }

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
	/// Applies this configuration to a transport message context.
	/// </summary>
	/// <param name="context">The context to apply configuration to.</param>
	public void ApplyTo(TransportMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		foreach (var header in _headers)
		{
			context.SetTransportProperty($"grpc.{header.Key}", header.Value);
		}
	}
}
