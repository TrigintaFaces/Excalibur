namespace Excalibur.Application.Requests;

/// <summary>
///     Defines the types of activities in the system.
/// </summary>
public enum ActivityType
{
	/// <summary>
	///     The activity type is unknown.
	/// </summary>
	Unknown,

	/// <summary>
	///     Represents a command activity.
	/// </summary>
	Command,

	/// <summary>
	///     Represents a query activity.
	/// </summary>
	Query,

	/// <summary>
	///     Represents a notification activity.
	/// </summary>
	Notification,

	/// <summary>
	///     Represents a job activity.
	/// </summary>
	Job
}
