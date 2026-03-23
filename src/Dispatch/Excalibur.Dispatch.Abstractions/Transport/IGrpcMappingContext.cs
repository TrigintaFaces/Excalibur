// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// gRPC-specific mapping context for configuring message properties.
/// </summary>
public interface IGrpcMappingContext
{
	/// <summary>
	/// Gets or sets the service method name.
	/// </summary>
	string? MethodName { get; set; }

	/// <summary>
	/// Gets or sets the deadline for the call.
	/// </summary>
	TimeSpan? Deadline { get; set; }

	/// <summary>
	/// Sets a custom header on the call.
	/// </summary>
	/// <param name="key">The header key.</param>
	/// <param name="value">The header value.</param>
	void SetHeader(string key, string value);
}
