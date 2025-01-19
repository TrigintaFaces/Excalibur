namespace Excalibur.A3.Authorization.Grants.Domain.Model;

/// <summary>
///     Provides predefined constants for grant types.
/// </summary>
public static class GrantType
{
	/// <summary>
	///     Represents a grant type associated with a specific activity.
	/// </summary>
	public static readonly string Activity = nameof(Activity);

	/// <summary>
	///     Represents a grant type associated with a group of activities.
	/// </summary>
	public static readonly string ActivityGroup = nameof(ActivityGroup);
}
