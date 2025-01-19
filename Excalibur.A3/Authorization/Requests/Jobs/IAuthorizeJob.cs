using Excalibur.Application.Requests.Jobs;

namespace Excalibur.A3.Authorization.Requests.Jobs;

/// <summary>
///     Represents a job that requires authorization.
/// </summary>
/// <remarks>
///     Combines the functionalities of <see cref="IJob" /> and <see cref="IAmAuthorizable" />, ensuring that the job can enforce access
///     control and permissions checks before execution.
/// </remarks>
public interface IAuthorizeJob : IJob, IAmAuthorizable
{
}
