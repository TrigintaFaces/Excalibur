using DOPA;

using Excalibur.A3.Authorization;
using Excalibur.Core;

namespace Excalibur.Tests.Wrappers;

/// <summary>
///     A wrapper for the internal AuthorizationPolicy class to enable testing.
/// </summary>
public sealed class AuthorizationPolicyWrapper : IAuthorizationPolicy, IDisposable
{
	private readonly IAuthorizationPolicy _policy;
	private bool _disposed;

	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizationPolicyWrapper" /> class.
	/// </summary>
	/// <param name="policy"> The OPA policy to use. </param>
	/// <param name="tenantId"> The tenant ID. </param>
	/// <param name="userId"> The user ID. </param>
	public AuthorizationPolicyWrapper(IOpaPolicy policy, ITenantId tenantId, string userId)
	{
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentNullException.ThrowIfNull(tenantId);
		ArgumentNullException.ThrowIfNull(userId);

		// Use reflection to create an instance of the internal class
		var policyType = Type.GetType("Excalibur.A3.Authorization.AuthorizationPolicy, Excalibur.A3");
		_policy = (IAuthorizationPolicy)Activator.CreateInstance(policyType, policy, tenantId, userId);
	}

	/// <inheritdoc />
	public string TenantId => _policy.TenantId;

	/// <inheritdoc />
	public string UserId => _policy.UserId;

	/// <inheritdoc />
	public bool IsAuthorized(string activityName, string? resourceId = null) =>
		_policy.IsAuthorized(activityName, resourceId);

	/// <inheritdoc />
	public bool HasGrant(string activityName) =>
		_policy.HasGrant(activityName);

	/// <inheritdoc />
	public bool HasGrant<TActivity>() =>
		_policy.HasGrant<TActivity>();

	/// <inheritdoc />
	public bool HasGrant(string resourceType, string resourceId) =>
		_policy.HasGrant(resourceType, resourceId);

	/// <inheritdoc />
	public bool HasGrant<TResourceType>(string resourceId) =>
		_policy.HasGrant<TResourceType>(resourceId);

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing && _policy is IDisposable disposable)
		{
			disposable.Dispose();
		}

		_disposed = true;
	}
}
