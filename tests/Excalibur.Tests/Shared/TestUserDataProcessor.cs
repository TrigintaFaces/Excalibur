using System.Collections.ObjectModel;

using Excalibur.DataAccess.DataProcessing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Shared;

[DataTaskRecordType("User")]
public class TestUserDataProcessor(
	IHostApplicationLifetime appLifetime,
	IOptions<DataProcessingConfiguration> configuration,
	IServiceProvider serviceProvider,
	ILogger<TestUserDataProcessor> logger)
	: DataProcessor<User>(appLifetime, configuration, serviceProvider, logger)
{
	public override async Task<IEnumerable<User>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken)
	{
		await Task.Delay(10, cancellationToken).ConfigureAwait(false);

		if (skip > 0)
		{
			return [];
		}

		var users = Enumerable.Range(0, batchSize).Select(x => new User { Id = x + 1, Name = $"TestUser_{x}" });
		return users;
	}
}

public sealed class TestUserRecordHandler() : IRecordHandler<User>
{
	public Collection<User> ProcessedRecords { get; } = [];

	public Task HandleAsync(User record, CancellationToken cancellationToken)
	{
		ProcessedRecords.Add(record);
		return Task.CompletedTask;
	}
}
