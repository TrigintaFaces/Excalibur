using Excalibur.Domain.Events;

namespace Excalibur.A3.Audit.Events;

/// <summary>
///     Represents an audited activity, capturing details about a user activity within the system for auditing purposes.
/// </summary>
public interface IActivityAudited : IDomainEvent
{
	/// <summary>
	///     Gets the name of the activity being audited.
	/// </summary>
	string ActivityName { get; init; }

	/// <summary>
	///     Gets the name of the application where the activity occurred.
	/// </summary>
	string ApplicationName { get; init; }

	/// <summary>
	///     Gets the client address (e.g., IP address) associated with the activity.
	/// </summary>
	string? ClientAddress { get; init; }

	/// <summary>
	///     Gets the correlation ID used to trace the activity across distributed systems or services.
	/// </summary>
	Guid CorrelationId { get; init; }

	/// <summary>
	///     Gets exception details, if any, related to the activity.
	/// </summary>
	string? Exception { get; init; }

	/// <summary>
	///     Gets the login identifier (e.g., email) of the user performing the activity.
	/// </summary>
	string? Login { get; init; }

	/// <summary>
	///     Gets the request payload or details associated with the activity.
	/// </summary>
	string Request { get; init; }

	/// <summary>
	///     Gets the response payload or details associated with the activity, if applicable.
	/// </summary>
	string? Response { get; init; }

	/// <summary>
	///     Gets the HTTP status code returned as a result of the activity.
	/// </summary>
	int StatusCode { get; set; }

	/// <summary>
	///     Gets the tenant identifier associated with the activity.
	/// </summary>
	string? TenantId { get; init; }

	/// <summary>
	///     Gets the timestamp indicating when the activity occurred.
	/// </summary>
	DateTimeOffset Timestamp { get; set; }

	/// <summary>
	///     Gets the user identifier of the individual performing the activity.
	/// </summary>
	string UserId { get; init; }

	/// <summary>
	///     Gets the name of the user performing the activity.
	/// </summary>
	string UserName { get; init; }
}
