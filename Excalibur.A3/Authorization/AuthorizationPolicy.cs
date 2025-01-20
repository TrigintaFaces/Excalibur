using System.Diagnostics;

using Excalibur.Core;

using IOpaPolicy = DOPA.IOpaPolicy;

namespace Excalibur.A3.Authorization;

/// <summary>
///     Represents an authorization policy that evaluates user permissions and grants based on activities and resources.
/// </summary>
/// <remarks> This policy is implemented using an Open Policy Agent (OPA) and evaluates inputs to determine access rights. </remarks>
/// <param name="policy"> The Open Policy Agent (OPA) policy used for authorization evaluation. </param>
/// <param name="tenantId"> The tenant identifier for the current context. </param>
/// <param name="userId"> The user identifier for the current context. </param>
internal sealed class AuthorizationPolicy(IOpaPolicy policy, ITenantId tenantId, string userId) : IAuthorizationPolicy, IDisposable
{
	private bool _disposedValue;

	/// <inheritdoc />
	public string TenantId { get; } = tenantId.Value;

	/// <inheritdoc />
	public string UserId { get; } = userId;

	/// <inheritdoc />
	public bool IsAuthorized(string activityName, string? resourceId = null)
	{
		var result = Evaluate(activityName, resourceId, null);

		return result?.IsAuthorized ?? false;
	}

	/// <inheritdoc />
	public bool HasGrant(string activityName)
	{
		var result = Evaluate(activityName, null, null);

		return result?.HasActivityGrant ?? false;
	}

	/// <inheritdoc />
	public bool HasGrant<TActivity>()
	{
		var activity = TypeNameHelper.GetTypeDisplayName(typeof(TActivity), false, false);
		return HasGrant(activity);
	}

	/// <inheritdoc />
	public bool HasGrant(string resourceType, string resourceId)
	{
		var result = Evaluate(null, resourceId, resourceType);

		return result?.HasResourceGrant ?? false;
	}

	/// <inheritdoc />
	public bool HasGrant<TResourceType>(string resourceId)
	{
		var resourceType = TypeNameHelper.GetTypeDisplayName(typeof(TResourceType), false, false);
		return HasGrant(resourceType, resourceId);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Disposes of the resources used by the policy.
	/// </summary>
	/// <param name="disposing"> Indicates whether the method is being called from the Dispose method. </param>
	private void Dispose(bool disposing)
	{
		if (_disposedValue)
		{
			return;
		}

		if (disposing)
		{
			policy?.Dispose();
		}

		_disposedValue = true;
	}

	/// <summary>
	///     Evaluates a policy for the specified activity, resource, and resource type.
	/// </summary>
	/// <param name="activity"> The name of the activity to evaluate. </param>
	/// <param name="resource"> The identifier of the resource (optional). </param>
	/// <param name="resourceType"> The type of the resource (optional). </param>
	/// <returns> A <see cref="PolicyResult" /> representing the evaluation result. </returns>
	private PolicyResult? Evaluate(string? activity, string? resource, string? resourceType)
	{
		var input = new
		{
			TenantId,
			activity,
			resource,
			resourceType,
			now = DateTime.UtcNow.Ticks
		};

		return policy.Evaluate<PolicyResult>(input);
	}

	/// <summary>
	///     Represents the result of a policy evaluation.
	/// </summary>
	internal sealed record PolicyResult
	{
		/// <summary>
		///     Gets or sets a value indicating whether the activity is authorized.
		/// </summary>
		public bool IsAuthorized { get; init; }

		/// <summary>
		///     Gets or sets a value indicating whether the activity grant exists.
		/// </summary>
		public bool HasActivityGrant { get; init; }

		/// <summary>
		///     Gets or sets a value indicating whether the resource grant exists.
		/// </summary>
		public bool HasResourceGrant { get; init; }
	}
}
