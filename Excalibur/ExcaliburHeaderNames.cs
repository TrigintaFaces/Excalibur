namespace Excalibur;

/// <summary>
///     Provides constants for common Excalibur-specific HTTP header names.
/// </summary>
public static class ExcaliburHeaderNames
{
	/// <summary>
	///     The header name used for identifying the correlation ID of a request.
	/// </summary>
	public static readonly string CorrelationId = "excalibur-correlation-id";

	/// <summary>
	///     The header name used for identifying the entity tag (ETag) associated with a resource.
	/// </summary>
	public static readonly string ETag = "excalibur-etag";

	/// <summary>
	///     The header name used for specifying the tenant ID associated with the request.
	/// </summary>
	public static readonly string TenantId = "excalibur-tenant-id";

	/// <summary>
	///     The header name used to indicate the entity or user that raised the request.
	/// </summary>
	public static readonly string RaisedBy = "excalibur-raised-by";
}
