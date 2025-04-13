using Excalibur.DataAccess;

namespace Excalibur.Tests.Shared;

public class OneShotFetcher<T>(IEnumerable<T> batch) : IRecordFetcher<T>
{
	private bool _returned;

	public Task<IEnumerable<T>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken)
	{
		if (_returned)
		{
			return Task.FromResult(Enumerable.Empty<T>());
		}

		_returned = true;
		return Task.FromResult(batch);
	}
}
