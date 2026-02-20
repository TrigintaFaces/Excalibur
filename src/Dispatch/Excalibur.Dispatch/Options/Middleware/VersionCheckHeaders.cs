// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Header name configuration for <see cref="ContractVersionCheckOptions" />.
/// </summary>
public sealed class VersionCheckHeaders
{
	/// <summary>
	/// Gets or sets the header name for message version.
	/// </summary>
	/// <value> Default is "X-Message-Version". </value>
	public string? VersionHeaderName { get; set; } = "X-Message-Version";

	/// <summary>
	/// Gets or sets the header name for schema ID.
	/// </summary>
	/// <value> Default is "X-Schema-MessageId". </value>
	public string? SchemaIdHeaderName { get; set; } = "X-Schema-MessageId";

	/// <summary>
	/// Gets or sets the header name for producer version.
	/// </summary>
	/// <value> Default is "X-Producer-Version". </value>
	public string? ProducerVersionHeaderName { get; set; } = "X-Producer-Version";

	/// <summary>
	/// Gets or sets the header name for producer service.
	/// </summary>
	/// <value> Default is "X-Producer-Service". </value>
	public string? ProducerServiceHeaderName { get; set; } = "X-Producer-Service";
}
