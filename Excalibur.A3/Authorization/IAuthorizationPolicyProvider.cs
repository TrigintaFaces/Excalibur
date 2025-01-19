namespace Excalibur.A3.Authorization;

/// <summary>
///     Provides functionality to retrieve an authorization policy for the current user.
/// </summary>
public interface IAuthorizationPolicyProvider : IPolicyProvider<IAuthorizationPolicy>
{
}
