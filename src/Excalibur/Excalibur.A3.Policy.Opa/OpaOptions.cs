// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.A3.Policy.Opa;

/// <summary>
/// Configuration options for the OPA HTTP authorization adapter.
/// </summary>
public sealed class OpaOptions
{
	/// <summary>
	/// Gets or sets the base URL of the OPA server (e.g., <c>http://localhost:8181</c>).
	/// </summary>
	/// <value>The OPA server endpoint URL.</value>
	[Required]
	public string Endpoint { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the OPA policy document path to query (e.g., <c>v1/data/authz/allow</c>).
	/// </summary>
	/// <value>The policy path relative to the OPA server root.</value>
	[Required]
	public string PolicyPath { get; set; } = "v1/data/authz/allow";

	/// <summary>
	/// Gets or sets the HTTP request timeout in milliseconds.
	/// </summary>
	/// <value>The timeout in milliseconds. Defaults to 5000.</value>
	[Range(100, 60000)]
	public int TimeoutMs { get; set; } = 5000;

	/// <summary>
	/// Gets or sets a value indicating whether authorization should deny on OPA errors (fail-closed).
	/// When <see langword="true"/>, HTTP errors, timeouts, and malformed responses result in denial.
	/// When <see langword="false"/>, errors result in permit (fail-open).
	/// </summary>
	/// <value><see langword="true"/> to fail closed (deny on error); <see langword="false"/> to fail open. Defaults to <see langword="true"/>.</value>
	public bool FailClosed { get; set; } = true;
}
