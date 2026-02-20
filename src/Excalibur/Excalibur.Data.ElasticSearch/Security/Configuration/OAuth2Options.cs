// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures OAuth2 and OpenID Connect authentication.
/// </summary>
public sealed class OAuth2Options
{
	/// <summary>
	/// Gets a value indicating whether oAuth2 authentication is enabled.
	/// </summary>
	/// <value> True to enable OAuth2/OpenID Connect authentication, false otherwise. </value>
	public bool Enabled { get; init; }

	/// <summary>
	/// Gets the OAuth2 authority/issuer URL.
	/// </summary>
	/// <value> The base URL of the OAuth2 authorization server. </value>
	[Url]
	public string? Authority { get; init; }

	/// <summary>
	/// Gets the OAuth2 client identifier.
	/// </summary>
	/// <value> The client ID registered with the OAuth2 provider. </value>
	public string? ClientId { get; init; }

	/// <summary>
	/// Gets the OAuth2 scope required for Elasticsearch access.
	/// </summary>
	/// <value> The space-separated list of OAuth2 scopes. </value>
	public string? Scope { get; init; } = "elasticsearch:read elasticsearch:write";

	/// <summary>
	/// Gets the audience for token validation.
	/// </summary>
	/// <value> The expected audience value in OAuth2 tokens. </value>
	public string? Audience { get; init; }

	/// <summary>
	/// Gets the token refresh buffer time.
	/// </summary>
	/// <value> The time before token expiration to trigger refresh. Defaults to 5 minutes. </value>
	public TimeSpan RefreshBuffer { get; init; } = TimeSpan.FromMinutes(5);
}
