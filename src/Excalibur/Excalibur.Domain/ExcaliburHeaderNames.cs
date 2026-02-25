// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain;

/// <summary>
/// Provides constants for common Excalibur-specific HTTP header names.
/// </summary>
public static class ExcaliburHeaderNames
{
	/// <summary>
	/// The header name used for identifying the correlation ID of a request.
	/// </summary>
	public const string CorrelationId = "X-Correlation-Id";

	/// <summary>
	/// The header name used for identifying the entity tag (ETag) associated with a resource.
	/// </summary>
	public const string ETag = "X-Etag";

	/// <summary>
	/// The header name used for specifying the tenant ID associated with the request.
	/// </summary>
	public const string TenantId = "X-Tenant-Id";

	/// <summary>
	/// The header name used to indicate the entity or user that raised the request.
	/// </summary>
	public const string RaisedBy = "X-Raised-By";
}
