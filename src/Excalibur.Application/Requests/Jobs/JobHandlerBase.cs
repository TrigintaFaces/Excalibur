using MediatR;

namespace Excalibur.Application.Requests.Jobs;

/// <summary>
///     Represents a base class for handling jobs of type <typeparamref name="TRequest" />.
/// </summary>
/// <typeparam name="TRequest"> The type of the job to handle, which must implement <see cref="IJob" />. </typeparam>
public abstract class JobHandlerBase<TRequest> : IRequestHandler<TRequest, JobResult>
	where TRequest : IJob
{
	/// <summary>
	///     Handles the specified job request.
	/// </summary>
	/// <param name="request"> The job request to handle. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the <see cref="JobResult" />. </returns>
	public abstract Task<JobResult> Handle(TRequest request, CancellationToken cancellationToken);
}
