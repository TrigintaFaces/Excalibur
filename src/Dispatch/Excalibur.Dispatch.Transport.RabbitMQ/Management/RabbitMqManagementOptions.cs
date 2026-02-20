// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Configuration options for the RabbitMQ Management HTTP API client.
/// </summary>
/// <remarks>
/// <para>
/// The RabbitMQ Management plugin exposes an HTTP API for monitoring and managing
/// the broker. This options class configures connection credentials and endpoint
/// for the <see cref="IRabbitMqManagementClient"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddRabbitMqManagement(options =>
/// {
///     options.BaseUrl = "http://localhost:15672";
///     options.Username = "guest";
///     options.Password = "guest";
/// });
/// </code>
/// </example>
public sealed class RabbitMqManagementOptions
{
	/// <summary>
	/// Gets or sets the base URL of the RabbitMQ Management API.
	/// </summary>
	/// <remarks>
	/// Typically <c>http://localhost:15672</c> for local development
	/// or the management endpoint URL of the RabbitMQ cluster.
	/// </remarks>
	/// <value>The base URL. Default is <c>http://localhost:15672</c>.</value>
	[Required]
	public string BaseUrl { get; set; } = "http://localhost:15672";

	/// <summary>
	/// Gets or sets the username for management API authentication.
	/// </summary>
	/// <value>The username. Default is <c>guest</c>.</value>
	[Required]
	public string Username { get; set; } = "guest";

	/// <summary>
	/// Gets or sets the password for management API authentication.
	/// </summary>
	/// <value>The password. Default is <c>guest</c>.</value>
	[Required]
	public string Password { get; set; } = "guest";

	/// <summary>
	/// Gets or sets the timeout for management API HTTP requests.
	/// </summary>
	/// <value>The request timeout. Default is 30 seconds.</value>
	public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
