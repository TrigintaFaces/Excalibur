namespace Excalibur.A3.Authorization;

/// <summary>
///     Represents a provider that retrieves a policy of type <typeparamref name="TPolicy" />.
/// </summary>
/// <typeparam name="TPolicy"> The type of the policy being provided. Must implement <see cref="IPolicy" />. </typeparam>
public interface IPolicyProvider<TPolicy>
	where TPolicy : IPolicy
{
	/// <summary>
	///     Asynchronously retrieves an instance of the policy for the current user or context.
	/// </summary>
	/// <returns>
	///     A task that represents the asynchronous operation. The task result contains the policy of type <typeparamref name="TPolicy" />
	///     configured for the user or context.
	/// </returns>
	/// <remarks>
	///     The method implementation should provide logic to determine the policy configuration based on the current user, context, or
	///     application state. The policy returned must be specific to the intended use case of the application.
	/// </remarks>
	public Task<TPolicy> GetPolicyAsync();
}
