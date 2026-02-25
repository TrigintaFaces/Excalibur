// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Audit.Events;

/// <summary>
/// Represents an audited activity, capturing details about a user activity within the system for auditing purposes.
/// </summary>
public interface IActivityAudited : IDomainEvent
{
	/// <summary>
	/// Gets the name of the activity being audited.
	/// </summary>
	/// <value>The name of the activity being audited.</value>
	string ActivityName { get; init; }

	/// <summary>
	/// Gets the name of the application where the activity occurred.
	/// </summary>
	/// <value>The name of the application where the activity occurred.</value>
	string ApplicationName { get; init; }

	/// <summary>
	/// Gets the client address (e.g., IP address) associated with the activity.
	/// </summary>
	/// <value>The client address, or <see langword="null"/> if not available.</value>
	string? ClientAddress { get; init; }

	/// <summary>
	/// Gets the correlation ID used to trace the activity across distributed systems or services.
	/// </summary>
	/// <value>The correlation ID used to trace the activity across distributed systems or services.</value>
	Guid CorrelationId { get; init; }

	/// <summary>
	/// Gets exception details, if any, related to the activity.
	/// </summary>
	/// <value>The exception details, or <see langword="null"/> if no exception occurred.</value>
	string? Exception { get; init; }

	/// <summary>
	/// Gets the login identifier (e.g., email) of the user performing the activity.
	/// </summary>
	/// <value>The login identifier, or <see langword="null"/> if not available.</value>
	string? Login { get; init; }

	/// &lt;summary&gt;
	/// Gets the request payload or details associated with the activity.
	/// &lt;/summary&gt;
	/// <value>The request payload or details associated with the activity.</value>
	string Request
	{
		[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize&lt;TValue&gt;(TValue, JsonSerializerOptions)")]
		[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize&lt;TValue&gt;(TValue, JsonSerializerOptions)")]
		get;
		init;
	}

	/// <summary>
	/// Gets the response payload or details associated with the activity, if applicable.
	/// </summary>
	/// <value>The response payload, or <see langword="null"/> if not applicable.</value>
	string? Response { get; init; }

	/// <summary>
	/// Gets or sets the HTTP status code returned as a result of the activity.
	/// </summary>
	/// <value>The HTTP status code returned as a result of the activity.</value>
	int StatusCode { get; set; }

	/// <summary>
	/// Gets the tenant identifier associated with the activity.
	/// </summary>
	/// <value>The tenant identifier, or <see langword="null"/> if not in a multi-tenant context.</value>
	string? TenantId { get; init; }

	/// <summary>
	/// Gets or sets the timestamp indicating when the activity occurred.
	/// </summary>
	/// <value>The timestamp indicating when the activity occurred.</value>
	DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets the user identifier of the individual performing the activity.
	/// </summary>
	/// <value>The user identifier of the individual performing the activity.</value>
	string UserId { get; init; }

	/// <summary>
	/// Gets the name of the user performing the activity.
	/// </summary>
	/// <value>The name of the user performing the activity.</value>
	string UserName { get; init; }
}
