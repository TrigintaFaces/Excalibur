using DOPA;

using Excalibur.A3.Authorization;
using Excalibur.Core;

namespace Excalibur.Tests.Unit.A3.Authorization;

/// <summary>
///     Test wrapper for the internal AuthorizationPolicy class
/// </summary>
public class TestableAuthorizationPolicy : IAuthorizationPolicy
{
	private readonly IAuthorizationPolicy _innerPolicy;

	public TestableAuthorizationPolicy(IOpaPolicy opaPolicy, ITenantId tenantId, string userId)
	{
		// Use reflection to create an instance of the internal AuthorizationPolicy class
		var policyType = typeof(IAuthorizationPolicy).Assembly.GetType("Excalibur.A3.Authorization.AuthorizationPolicy");
		_innerPolicy = (IAuthorizationPolicy)Activator.CreateInstance(policyType!, opaPolicy, tenantId, userId)!;

		TenantId = _innerPolicy.TenantId;
		UserId = _innerPolicy.UserId;
	}

	public string TenantId { get; }

	public string UserId { get; }

	public bool IsAuthorized(string activityName, string? resourceId = null) =>
		_innerPolicy.IsAuthorized(activityName, resourceId);

	public bool HasGrant(string activityName) =>
		_innerPolicy.HasGrant(activityName);

	public bool HasGrant<TActivity>() =>
		_innerPolicy.HasGrant<TActivity>();

	public bool HasGrant(string resourceType, string resourceId) =>
		_innerPolicy.HasGrant(resourceType, resourceId);

	public bool HasGrant<TResourceType>(string resourceId) =>
		_innerPolicy.HasGrant<TResourceType>(resourceId);
}
