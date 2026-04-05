// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Audit.Events;

/// <summary>
/// Represents the result portion of an audited activity, capturing the outcome of the action.
/// </summary>
public interface IAuditResult
{
	/// <summary>
	/// Gets the request payload or details associated with the activity.
	/// </summary>
	/// <value>The request payload or details associated with the activity.</value>
	string Request
	{
		[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
		[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
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
	/// Gets exception details, if any, related to the activity.
	/// </summary>
	/// <value>The exception details, or <see langword="null"/> if no exception occurred.</value>
	string? Exception { get; init; }
}
