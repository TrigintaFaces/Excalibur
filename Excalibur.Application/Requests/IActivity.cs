using Newtonsoft.Json;

namespace Excalibur.Application.Requests;

/// <summary>
///     Represents an activity that is correlatable, multi-tenant, and transactional.
/// </summary>
public interface IActivity : IAmCorrelatable, IAmMultiTenant, IAmTransactional
{
	/// <summary>
	///     Gets the type of the activity.
	/// </summary>
	public ActivityType ActivityType { get; }

	/// <summary>
	///     Gets the name of the activity.
	/// </summary>
	[JsonIgnore]
	public string ActivityName { get; }

	/// <summary>
	///     Gets the display name of the activity.
	/// </summary>
	[JsonIgnore]
	public string ActivityDisplayName { get; }

	/// <summary>
	///     Gets the description of the activity.
	/// </summary>
	[JsonIgnore]
	public string ActivityDescription { get; }
}
