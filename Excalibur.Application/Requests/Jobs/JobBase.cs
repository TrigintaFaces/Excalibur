namespace Excalibur.Application.Requests.Jobs;

/// <summary>
///     Represents the base implementation for a job in the system.
/// </summary>
public abstract class JobBase : IJob
{
	/// <summary>
	///     Initializes a new instance of the <see cref="JobBase" /> class with the specified correlation ID and tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID for the job. </param>
	/// <param name="tenantId"> The tenant ID associated with the job. Defaults to "NotSpecified" if not provided. </param>
	protected JobBase(Guid correlationId, string? tenantId = null)
	{
		CorrelationId = correlationId;
		TenantId = tenantId ?? "NotSpecified";
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="JobBase" /> class with default values.
	/// </summary>
	protected JobBase() : this(Guid.Empty)
	{
	}

	/// <inheritdoc />
	public ActivityType ActivityType => ActivityType.Job;

	/// <inheritdoc />
	public string ActivityName => GetType().Name;

	/// <inheritdoc />
	public abstract string ActivityDisplayName { get; }

	/// <inheritdoc />
	public abstract string ActivityDescription { get; }

	/// <inheritdoc />
	public Guid CorrelationId { get; protected init; }

	/// <inheritdoc />
	public string? TenantId { get; protected init; }
}
