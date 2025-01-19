using Excalibur.A3.Authentication;
using Excalibur.Exceptions;

namespace Excalibur.A3.Exceptions;

/// <summary>
///     Represents an exception that is thrown when an authorization check fails.
/// </summary>
/// <remarks>
///     Inherits from <see cref="ApiException" /> and provides specific details about unauthorized access, including user-related context.
/// </remarks>
[Serializable]
public class NotAuthorizedException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="NotAuthorizedException" /> class with the specified user details, status code,
	///     message, and optional inner exception.
	/// </summary>
	/// <param name="user"> The user attempting the unauthorized action. </param>
	/// <param name="statusCode"> The HTTP status code representing the unauthorized error. </param>
	/// <param name="message"> A message describing the reason for the exception. </param>
	/// <param name="innerException"> An optional inner exception providing additional context. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="user" /> is <c> null </c>. </exception>
	public NotAuthorizedException(IAuthenticationToken user,
		int statusCode,
		string message,
		Exception? innerException = null)
		: base(statusCode, message, innerException)
	{
		ArgumentNullException.ThrowIfNull(user);

		Login = user.Login;
		UserId = user.UserId;
		UserName = user.FullName;
	}

	/// <summary>
	///     Gets the login associated with the unauthorized action.
	/// </summary>
	public string? Login { get; protected set; }

	/// <summary>
	///     Gets the user ID associated with the unauthorized action.
	/// </summary>
	public string? UserId { get; protected set; }

	/// <summary>
	///     Gets the full name of the user associated with the unauthorized action.
	/// </summary>
	public string? UserName { get; protected set; }

	/// <summary>
	///     Creates a <see cref="NotAuthorizedException" /> indicating that anonymous access is not allowed.
	/// </summary>
	/// <param name="exception"> An optional exception providing additional context. </param>
	/// <returns> A <see cref="NotAuthorizedException" /> for unauthorized anonymous access. </returns>
	public static NotAuthorizedException BecauseNotAuthenticated(Exception? exception = null) =>
		new(AuthenticationToken.Anonymous, 401, "Anonymous access is not allowed.", exception);

	/// <summary>
	///     Creates a <see cref="NotAuthorizedException" /> indicating that the user is forbidden from performing an activity.
	/// </summary>
	/// <param name="user"> The user attempting the unauthorized action. </param>
	/// <param name="activityName"> The name of the activity being attempted. </param>
	/// <param name="resourceId"> The optional ID of the resource associated with the activity. </param>
	/// <param name="exception"> An optional exception providing additional context. </param>
	/// <returns> A <see cref="NotAuthorizedException" /> for forbidden access to the specified activity and resource. </returns>
	public static NotAuthorizedException BecauseForbidden(IAuthenticationToken user, string activityName, string? resourceId,
		Exception? exception = null)
	{
		ArgumentNullException.ThrowIfNull(user);

		var clarification = string.IsNullOrEmpty(resourceId) ? string.Empty : $" on {resourceId}";
		return new NotAuthorizedException(user, 403, $"{user.FullName} is not authorized for {activityName}{clarification}.", exception);
	}

	/// <summary>
	///     Creates a <see cref="NotAuthorizedException" /> with a custom message explaining the reason for the failure.
	/// </summary>
	/// <param name="user"> The user attempting the unauthorized action. </param>
	/// <param name="message"> A custom message describing the unauthorized action. </param>
	/// <param name="exception"> An optional exception providing additional context. </param>
	/// <returns> A <see cref="NotAuthorizedException" /> with a detailed explanation for the authorization failure. </returns>
	public static NotAuthorizedException Because(IAuthenticationToken user, string message, Exception? exception = null) =>
		new(user, 403, message, exception);
}
