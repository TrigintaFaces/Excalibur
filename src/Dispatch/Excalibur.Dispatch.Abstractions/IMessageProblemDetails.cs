// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Interface for providing structured problem details for message processing errors.
/// </summary>
/// <remarks>
/// Based on RFC 7807 Problem Details for HTTP APIs, but adapted for message processing scenarios. Provides a standard way to carry
/// machine-readable details of errors in message processing.
/// </remarks>
public interface IMessageProblemDetails
{
	/// <summary>
	/// Gets or sets a URI reference that identifies the problem type.
	/// </summary>
	/// <remarks>
	/// This specification encourages that, when dereferenced, it provide human-readable documentation for the problem type. When this
	/// member is not present, its value is assumed to be "about:blank".
	/// </remarks>
	/// <value> The problem type identifier. </value>
	string Type { get; set; }

	/// <summary>
	/// Gets or sets a short, human-readable summary of the problem type.
	/// </summary>
	/// <remarks> It SHOULD NOT change from occurrence to occurrence of the problem, except for purposes of localization. </remarks>
	/// <value> The summary of the problem type. </value>
	string Title { get; set; }

	/// <summary>
	/// Gets or sets the application-specific error code.
	/// </summary>
	/// <remarks> A problem type may define additional members and their semantics. </remarks>
	/// <value> The application-specific error code. </value>
	int ErrorCode { get; set; }

	/// <summary>
	/// Gets or sets a human-readable explanation specific to this occurrence of the problem.
	/// </summary>
	/// <value> The human-readable explanation. </value>
	string Detail { get; set; }

	/// <summary>
	/// Gets or sets a URI reference that identifies the specific occurrence of the problem.
	/// </summary>
	/// <remarks> It may or may not yield further information if dereferenced. </remarks>
	/// <value> The occurrence URI. </value>
	string Instance { get; set; }

	/// <summary>
	/// Gets the additional problem-specific extension fields.
	/// </summary>
	/// <remarks> Problem type definitions are free to define additional members and their semantics. </remarks>
	/// <value> The problem-specific extensions. </value>
	IDictionary<string, object?> Extensions { get; }
}
