using Polly;

namespace Excalibur.DataAccess.SqlServer;

public interface IDataAccessPolicyFactory
{
	IAsyncPolicy GetComprehensivePolicy();

	IAsyncPolicy GetRetryPolicy();

	IAsyncPolicy CreateCircuitBreakerPolicy();
}
