using MediatR;

using Newtonsoft.Json;

namespace Excalibur.Application.Requests.Jobs;

/// <summary>
///     Represents a job that is correlatable, multi-tenant, and produces a <see cref="JobResult" /> response.
/// </summary>
public interface IJob : IAmCorrelatable, IAmMultiTenant, IRequest<JobResult>
{
	/// <summary>
	///     Gets the type of the activity, which is typically <see cref="ActivityType.Job" /> for jobs.
	/// </summary>
	public ActivityType ActivityType { get; }

	/// <summary>
	///     Gets the unique name of the job.
	/// </summary>
	/// <remarks> The name is typically a combination of the namespace and type name, providing a unique identifier for the job. </remarks>
	[JsonIgnore]
	public string ActivityName { get; }

	/// <summary>
	///     Gets a human-readable display name of the job.
	/// </summary>
	/// <remarks> The display name is suitable for use in logs, dashboards, or other user-facing contexts. </remarks>
	[JsonIgnore]
	public string ActivityDisplayName { get; }

	/// <summary>
	///     Gets a description of the job's purpose or functionality.
	/// </summary>
	/// <remarks> The description provides additional context about the job and can be used for documentation or debugging. </remarks>
	[JsonIgnore]
	public string ActivityDescription { get; }
}
