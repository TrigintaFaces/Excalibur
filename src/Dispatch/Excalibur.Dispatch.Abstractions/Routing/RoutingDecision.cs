// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Routing;

/// <summary>
/// Result of routing evaluation containing both transport and endpoint decisions.
/// </summary>
/// <remarks>
/// <para>
/// The routing decision encapsulates the complete outcome of message routing,
/// including which transport (message bus) to use and which endpoints (services)
/// should receive the message.
/// </para>
/// <para>
/// Use the factory methods <see cref="Success"/> and <see cref="Failure"/> to
/// create instances with appropriate initialization.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Creating a successful routing decision
/// var decision = RoutingDecision.Success(
///     transport: "rabbitmq",
///     endpoints: new[] { "billing-service", "inventory-service" },
///     matchedRules: new[] { "order-transport-rule", "order-endpoint-rule" });
///
/// // Creating a failure routing decision
/// var failed = RoutingDecision.Failure("No matching routing rules found");
///
/// // Checking the result
/// if (decision.IsSuccess)
/// {
///     Console.WriteLine($"Route to {decision.Transport} -> {string.Join(", ", decision.Endpoints)}");
/// }
/// </code>
/// </example>
public sealed record RoutingDecision
{
	/// <summary>
	/// Gets the selected transport (e.g., "local", "rabbitmq", "kafka").
	/// </summary>
	/// <value>
	/// The transport name. Empty string indicates routing failure.
	/// </value>
	public required string Transport { get; init; }

	/// <summary>
	/// Gets the target endpoints (e.g., "billing-service", "inventory-service").
	/// </summary>
	/// <value>
	/// A read-only list of endpoint names. May be empty for local-only routing.
	/// </value>
	public required IReadOnlyList<string> Endpoints { get; init; }

	/// <summary>
	/// Gets a value indicating whether routing succeeded.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if a transport was selected;
	/// otherwise, <see langword="false"/>.
	/// </value>
	public bool IsSuccess => !string.IsNullOrEmpty(Transport);

	/// <summary>
	/// Gets the failure reason if routing failed.
	/// </summary>
	/// <value>
	/// A description of why routing failed, or <see langword="null"/> if routing succeeded.
	/// </value>
	public string? FailureReason { get; init; }

	/// <summary>
	/// Gets the names of routing rules that matched (for diagnostics).
	/// </summary>
	/// <value>
	/// A read-only list of rule names that contributed to this routing decision.
	/// </value>
	public IReadOnlyList<string> MatchedRules { get; init; } = [];

	/// <summary>
	/// Creates a successful routing decision.
	/// </summary>
	/// <param name="transport">The selected transport name.</param>
	/// <param name="endpoints">The target endpoint names.</param>
	/// <param name="matchedRules">Optional list of matched rule names for diagnostics.</param>
	/// <returns>A new <see cref="RoutingDecision"/> indicating success.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="transport"/> is null or empty.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="endpoints"/> is null.
	/// </exception>
	public static RoutingDecision Success(
		string transport,
		IReadOnlyList<string> endpoints,
		IReadOnlyList<string>? matchedRules = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(transport);
		ArgumentNullException.ThrowIfNull(endpoints);

		return new RoutingDecision
		{
			Transport = transport,
			Endpoints = endpoints,
			MatchedRules = matchedRules ?? [],
		};
	}

	/// <summary>
	/// Creates a failed routing decision.
	/// </summary>
	/// <param name="reason">A description of why routing failed.</param>
	/// <returns>A new <see cref="RoutingDecision"/> indicating failure.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="reason"/> is null or empty.
	/// </exception>
	public static RoutingDecision Failure(string reason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		return new RoutingDecision
		{
			Transport = string.Empty,
			Endpoints = [],
			FailureReason = reason,
		};
	}
}
