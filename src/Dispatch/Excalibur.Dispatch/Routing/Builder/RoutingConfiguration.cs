// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Routing.Builder;

/// <summary>
/// Configuration holder for routing rules built from the fluent builder.
/// </summary>
/// <remarks>
/// This is an internal configuration class that holds the routing rules
/// configured via the <see cref="IRoutingBuilder"/> fluent API.
/// </remarks>
internal sealed class RoutingConfiguration
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RoutingConfiguration"/> class.
	/// </summary>
	/// <param name="builder">The routing builder containing configuration.</param>
	public RoutingConfiguration(RoutingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		TransportRules = builder.Transport.GetRules();
		DefaultTransport = builder.Transport.DefaultTransport ?? "local";
		EndpointRules = builder.Endpoints.GetRules();
		FallbackEndpoint = builder.Fallback.Endpoint;
		FallbackReason = builder.Fallback.Reason;
	}

	/// <summary>
	/// Gets the configured transport routing rules.
	/// </summary>
	public IReadOnlyList<TransportRoutingRule> TransportRules { get; }

	/// <summary>
	/// Gets the default transport name.
	/// </summary>
	public string DefaultTransport { get; }

	/// <summary>
	/// Gets the configured endpoint routing rules.
	/// </summary>
	public IReadOnlyList<EndpointRoutingRule> EndpointRules { get; }

	/// <summary>
	/// Gets the fallback endpoint.
	/// </summary>
	public string? FallbackEndpoint { get; }

	/// <summary>
	/// Gets the fallback reason for diagnostics.
	/// </summary>
	public string? FallbackReason { get; }
}
