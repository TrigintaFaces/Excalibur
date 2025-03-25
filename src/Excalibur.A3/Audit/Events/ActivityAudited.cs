namespace Excalibur.A3.Audit.Events;

/// <summary>
///     Represents an audited activity, capturing details about an activity performed in the system, including metadata for auditing purposes.
/// </summary>
public class ActivityAudited : IActivityAudited
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ActivityAudited" /> class by copying properties from an existing
	///     <see cref="IActivityAudited" /> instance.
	/// </summary>
	/// <param name="audit"> The <see cref="IActivityAudited" /> instance to copy data from. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="audit" /> is <c> null </c>. </exception>
	public ActivityAudited(IActivityAudited audit)
	{
		ArgumentNullException.ThrowIfNull(audit);

		ActivityName = audit.ActivityName;
		ApplicationName = audit.ApplicationName;
		ClientAddress = audit.ClientAddress;
		CorrelationId = audit.CorrelationId;
		Exception = audit.Exception;
		Login = audit.Login;
		Request = audit.Request;
		Response = audit.Response;
		StatusCode = audit.StatusCode;
		TenantId = audit.TenantId;
		Timestamp = audit.Timestamp;
		UserId = audit.UserId;
		UserName = audit.UserName;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="ActivityAudited" /> class.
	/// </summary>
	public ActivityAudited()
	{
	}

	/// <inheritdoc />
	public string ActivityName { get; init; }

	/// <inheritdoc />
	public string ApplicationName { get; init; }

	/// <inheritdoc />
	public string? ClientAddress { get; init; }

	/// <inheritdoc />
	public Guid CorrelationId { get; init; }

	/// <inheritdoc />
	public string? Exception { get; init; }

	/// <inheritdoc />
	public string? Login { get; init; }

	/// <inheritdoc />
	public string Request { get; init; }

	/// <inheritdoc />
	public string? Response { get; init; }

	/// <inheritdoc />
	public int StatusCode { get; set; }

	/// <inheritdoc />
	public string? TenantId { get; init; }

	/// <inheritdoc />
	public DateTimeOffset Timestamp { get; set; }

	/// <inheritdoc />
	public string UserId { get; init; }

	/// <inheritdoc />
	public string UserName { get; init; }
}
