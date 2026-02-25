// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Domain;

/// <summary>
/// Configuration options for the application context, replacing static <see cref="ApplicationContext"/>
/// initialization. Bind from <c>ApplicationContext</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// Reference: <c>Microsoft.Extensions.Options.IOptions&lt;T&gt;</c> — POCO configuration pattern.
/// This replaces the static <see cref="ApplicationContext.Init"/> approach with standard
/// options binding.
/// </para>
/// <para>
/// Register via <c>services.AddApplicationContext(configuration)</c> in the
/// <c>Microsoft.Extensions.DependencyInjection</c> namespace.
/// </para>
/// </remarks>
public sealed class ApplicationContextOptions
{
	/// <summary>
	/// Gets or sets the name of the application.
	/// </summary>
	public string ApplicationName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the system name of the application (used for telemetry resource identity).
	/// </summary>
	public string ApplicationSystemName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the display name of the application.
	/// </summary>
	public string ApplicationDisplayName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the audience value for the authentication service.
	/// </summary>
	public string AuthenticationServiceAudience { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the endpoint for the authentication service.
	/// </summary>
	public string AuthenticationServiceEndpoint { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the path to the public key file for the authentication service.
	/// </summary>
	public string AuthenticationServicePublicKeyPath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the endpoint for the authorization service.
	/// </summary>
	public string AuthorizationServiceEndpoint { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the service account.
	/// </summary>
	public string ServiceAccountName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the path to the private key file for the service account.
	/// </summary>
	public string ServiceAccountPrivateKeyPath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the password for the service account's private key.
	/// Stored securely via <see cref="ApplicationContext"/> sensitive key detection.
	/// </summary>
	public string ServiceAccountPrivateKeyPasswordSecure { get; set; } = string.Empty;
}
