// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Standard Problem Details Type URIs for Dispatch error responses.
/// </summary>
/// <remarks>
/// <para>
/// Type URIs follow the URN format: <c>urn:dispatch:error:{type}</c>
/// </para>
/// <para>
/// Per RFC 9457 (Problem Details for HTTP APIs), Type URIs do not need to resolve
/// to an actual resource. The URN format is preferred over URLs because:
/// <list type="bullet">
/// <item><description>URNs are explicitly non-resolvable identifiers</description></item>
/// <item><description>Avoids confusion when URLs don't resolve to documentation</description></item>
/// <item><description>Self-documenting format with clear namespace hierarchy</description></item>
/// </list>
/// </para>
/// <para>
/// All type suffixes use lowercase kebab-case (e.g., <c>not-found</c>, <c>rate-limited</c>).
/// </para>
/// </remarks>
/// <seealso href="https://www.rfc-editor.org/rfc/rfc9457.html">RFC 9457 - Problem Details for HTTP APIs</seealso>
public static class ProblemDetailsTypes
{
	/// <summary>
	/// The URN prefix for all Dispatch error types.
	/// </summary>
	public const string Prefix = "urn:dispatch:error:";

	/// <summary>
	/// Validation error - request data failed validation rules.
	/// </summary>
	/// <value><c>urn:dispatch:error:validation</c></value>
	public const string Validation = Prefix + "validation";

	/// <summary>
	/// Resource not found - the requested resource does not exist.
	/// </summary>
	/// <value><c>urn:dispatch:error:not-found</c></value>
	public const string NotFound = Prefix + "not-found";

	/// <summary>
	/// Conflict error - request conflicts with current state (e.g., concurrency violation).
	/// </summary>
	/// <value><c>urn:dispatch:error:conflict</c></value>
	public const string Conflict = Prefix + "conflict";

	/// <summary>
	/// Forbidden - the caller is authenticated but not authorized for this operation.
	/// </summary>
	/// <value><c>urn:dispatch:error:forbidden</c></value>
	public const string Forbidden = Prefix + "forbidden";

	/// <summary>
	/// Unauthorized - authentication is required but was not provided or is invalid.
	/// </summary>
	/// <value><c>urn:dispatch:error:unauthorized</c></value>
	public const string Unauthorized = Prefix + "unauthorized";

	/// <summary>
	/// Timeout error - the operation exceeded its time limit.
	/// </summary>
	/// <value><c>urn:dispatch:error:timeout</c></value>
	public const string Timeout = Prefix + "timeout";

	/// <summary>
	/// Rate limited - the caller has exceeded rate limits.
	/// </summary>
	/// <value><c>urn:dispatch:error:rate-limited</c></value>
	public const string RateLimited = Prefix + "rate-limited";

	/// <summary>
	/// Internal error - an unexpected server-side error occurred.
	/// </summary>
	/// <value><c>urn:dispatch:error:internal</c></value>
	public const string Internal = Prefix + "internal";

	/// <summary>
	/// Routing error - message could not be routed to a handler.
	/// </summary>
	/// <value><c>urn:dispatch:error:routing</c></value>
	public const string Routing = Prefix + "routing";

	/// <summary>
	/// Transport error - message transport or delivery failed.
	/// </summary>
	/// <value><c>urn:dispatch:error:transport</c></value>
	public const string Transport = Prefix + "transport";

	/// <summary>
	/// Serialization error - message serialization or deserialization failed.
	/// </summary>
	/// <value><c>urn:dispatch:error:serialization</c></value>
	public const string Serialization = Prefix + "serialization";

	/// <summary>
	/// Concurrency error - optimistic concurrency check failed.
	/// </summary>
	/// <value><c>urn:dispatch:error:concurrency</c></value>
	public const string Concurrency = Prefix + "concurrency";

	/// <summary>
	/// Handler not found - no handler is registered for the message type.
	/// </summary>
	/// <value><c>urn:dispatch:error:handler-not-found</c></value>
	public const string HandlerNotFound = Prefix + "handler-not-found";

	/// <summary>
	/// Handler error - the message handler threw an exception.
	/// </summary>
	/// <value><c>urn:dispatch:error:handler-error</c></value>
	public const string HandlerError = Prefix + "handler-error";

	/// <summary>
	/// Mapping failed - exception mapping to problem details failed.
	/// </summary>
	/// <value><c>urn:dispatch:error:mapping-failed</c></value>
	public const string MappingFailed = Prefix + "mapping-failed";

	/// <summary>
	/// Background execution error - background task execution failed.
	/// </summary>
	/// <value><c>urn:dispatch:error:background-execution</c></value>
	public const string BackgroundExecution = Prefix + "background-execution";
}
