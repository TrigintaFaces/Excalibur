// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.A3.Policy.Cedar;

/// <summary>
/// Configuration options for the Cedar HTTP authorization adapter.
/// </summary>
public sealed class CedarOptions
{
	/// <summary>
	/// Gets or sets the base URL of the Cedar agent or AVP endpoint.
	/// </summary>
	/// <value>The Cedar endpoint URL.</value>
	[Required]
	public string Endpoint { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the policy store identifier. Required for <see cref="CedarMode.AwsVerifiedPermissions"/>.
	/// </summary>
	/// <value>The AVP policy store ID.</value>
	public string? PolicyStoreId { get; set; }

	/// <summary>
	/// Gets or sets the HTTP request timeout in milliseconds.
	/// </summary>
	/// <value>The timeout in milliseconds. Defaults to 5000.</value>
	[Range(100, 60000)]
	public int TimeoutMs { get; set; } = 5000;

	/// <summary>
	/// Gets or sets a value indicating whether authorization should deny on Cedar errors (fail-closed).
	/// </summary>
	/// <value><see langword="true"/> to fail closed (deny on error); <see langword="false"/> to fail open. Defaults to <see langword="true"/>.</value>
	public bool FailClosed { get; set; } = true;

	/// <summary>
	/// Gets or sets the Cedar evaluation mode.
	/// </summary>
	/// <value>The Cedar mode. Defaults to <see cref="CedarMode.Local"/>.</value>
	public CedarMode Mode { get; set; } = CedarMode.Local;
}
