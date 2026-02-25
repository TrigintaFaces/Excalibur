// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Helper methods for creating <see cref="MessageContext" /> instances from various sources.
/// </summary>
public static class DispatchContextInitializer
{
	/// <summary>
	/// Creates a minimal <see cref="MessageContext" /> using a temporary service provider.
	/// </summary>
	/// <returns> A new <see cref="MessageContext" /> instance. </returns>
	public static MessageContext CreateDefaultContext()
	{
		var provider = new ServiceCollection().BuildServiceProvider();
		return CreateDefaultContext(provider);
	}

	/// <summary>
	/// Creates a <see cref="MessageContext" /> using the specified <see cref="IServiceProvider" />.
	/// </summary>
	/// <param name="serviceProvider"> Service provider for resolving scoped services. </param>
	/// <returns> A new <see cref="MessageContext" /> instance. </returns>
	public static MessageContext CreateDefaultContext(IServiceProvider serviceProvider)
	{
		var context = MessageContext.CreateForDeserialization(serviceProvider);
		context.CorrelationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
		context.TraceParent = Activity.Current?.Id;

		if (Activity.Current?.Baggage != null)
		{
			foreach (var (key, value) in Activity.Current.Baggage)
			{
				context.Items[$"baggage.{key}"] = value ?? string.Empty;
			}
		}

		return context;
	}

	/// <summary>
	/// Creates a <see cref="MessageContext" /> from HTTP headers.
	/// </summary>
	/// <param name="headers"> Header dictionary containing context values. </param>
	/// <returns> A new <see cref="MessageContext" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="headers" /> is null. </exception>
	public static MessageContext CreateFromHeaders(IDictionary<string, string?> headers)
	{
		ArgumentNullException.ThrowIfNull(headers);

		var context = CreateDefaultContext();

		if (headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId))
		{
			context.CorrelationId = correlationId;
		}

		if (headers.TryGetValue("X-Causation-ID", out var causationId) && !string.IsNullOrWhiteSpace(causationId))
		{
			context.CausationId = causationId;
		}

		_ = headers.ContainsKey("X-User-ID");

		_ = headers.ContainsKey("X-Tenant-ID");

		foreach (var pair in headers)
		{
			context.Items[pair.Key] = pair.Value ?? string.Empty;
		}

		return context;
	}

	/// <summary>
	/// Creates a <see cref="MessageContext" /> from message metadata.
	/// </summary>
	/// <param name="metadata"> Metadata dictionary containing context values. </param>
	/// <returns> A new <see cref="MessageContext" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="metadata" /> is null. </exception>
	public static MessageContext CreateFromMetadata(IDictionary<string, string?> metadata)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		var context = CreateDefaultContext();

		if (metadata.TryGetValue("CorrelationId", out var correlation) && !string.IsNullOrWhiteSpace(correlation))
		{
			context.CorrelationId = correlation;
		}

		if (metadata.TryGetValue("CausationId", out var causation) && !string.IsNullOrWhiteSpace(causation))
		{
			context.CausationId = causation;
		}

		_ = metadata.ContainsKey("UserId");

		_ = metadata.ContainsKey("TenantId");

		foreach (var pair in metadata)
		{
			context.Items[pair.Key] = pair.Value ?? string.Empty;
		}

		return context;
	}
}
