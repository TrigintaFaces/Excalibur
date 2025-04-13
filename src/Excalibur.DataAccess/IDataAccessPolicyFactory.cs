using Polly;

namespace Excalibur.DataAccess.SqlServer;

public interface IDataAccessPolicyFactory
{
	public IAsyncPolicy GetComprehensivePolicy();

	public IAsyncPolicy GetRetryPolicy();

	public IAsyncPolicy CreateCircuitBreakerPolicy();
}
