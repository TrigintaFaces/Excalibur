// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides constants for common Excalibur-specific HTTP header names.
/// </summary>
public static class WellKnownHeaderNames
{
	/// <summary>
	/// The header name used for identifying the correlation ID of a request.
	/// </summary>
	public static readonly string CorrelationId = "X-Correlation-Id";

	/// <summary>
	/// The header name used for identifying the causation ID of a request.
	/// </summary>
	public static readonly string CausationId = "X-Causation-Id";

	/// <summary>
	/// The header name used for identifying the entity tag (ETag) associated with a resource.
	/// </summary>
	public static readonly string ETag = "X-Etag";

	/// <summary>
	/// The header name used for specifying the tenant ID associated with the request.
	/// </summary>
	public static readonly string TenantId = "X-Tenant-Id";

	/// <summary>
	/// The header name used to indicate the entity or user that raised the request.
	/// </summary>
	public static readonly string RaisedBy = "X-Raised-By";
}
