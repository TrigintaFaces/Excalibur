using Excalibur.Application.Requests.Jobs;

namespace Excalibur.A3.Authorization.Requests.Jobs;

/// <summary>
///     Provides a base implementation for jobs that require authorization.
/// </summary>
/// <remarks>
///     Implements the <see cref="IAuthorizeJob" /> interface, enabling access token handling and tenant-based authorization for jobs.
/// </remarks>
public abstract class AuthorizeJobBase : JobBase, IAuthorizeJob
{
	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizeJobBase" /> class with the specified correlation ID and tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID to track the job. </param>
	/// <param name="tenantId"> The tenant ID associated with the job. </param>
	protected AuthorizeJobBase(Guid correlationId, string? tenantId)
		: base(correlationId)
	{
		TenantId = tenantId;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizeJobBase" /> class with default values.
	/// </summary>
	protected AuthorizeJobBase()
	{
	}

	/// <inheritdoc />
	public IAccessToken? AccessToken { get; set; }
}
