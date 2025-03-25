using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Factory class for creating instances of <see cref="DataChangeEventProcessor" />.
/// </summary>
public class DataChangeEventProcessorFactory : IDataChangeEventProcessorFactory
{
	private readonly IServiceProvider _serviceProvider;

	private readonly IDataAccessPolicyFactory _policyFactory;

	private readonly IHostApplicationLifetime _appLifetime;

	/// <summary>
	///     Initializes a new instance of the <see cref="DataChangeEventProcessorFactory" /> class.
	/// </summary>
	/// <param name="serviceProvider"> The service provider used for resolving dependencies. </param>
	/// <param name="appLifetime">
	///     An instance of <see cref="IHostApplicationLifetime" /> that allows the application to perform actions during the application's
	///     lifecycle events, such as startup, shutdown, or when the application is stopping. This parameter is used to gracefully manage
	///     tasks that need to respond to application lifecycle events.
	/// </param>
	public DataChangeEventProcessorFactory(
		IServiceProvider serviceProvider,
		IHostApplicationLifetime appLifetime,
		IDataAccessPolicyFactory policyFactory)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(appLifetime);
		ArgumentNullException.ThrowIfNull(policyFactory);

		_serviceProvider = serviceProvider;
		_appLifetime = appLifetime;
		_policyFactory = policyFactory;
	}

	/// <summary>
	///     Creates an instance of <see cref="DataChangeEventProcessor" /> using <see cref="SqlConnection" /> instances.
	/// </summary>
	/// <param name="dbConfig"> The database configuration used for CDC processing. </param>
	/// <param name="cdcConnection"> The SQL connection for interacting with CDC data. </param>
	/// <param name="stateStoreConnection"> The SQL connection for persisting CDC state. </param>
	/// <returns> A configured <see cref="IDataChangeEventProcessor" /> instance. </returns>
	/// <exception cref="ArgumentNullException">
	///     Thrown if <paramref name="dbConfig" />, <paramref name="cdcConnection" />, or <paramref name="stateStoreConnection" /> is <c>
	///     null </c>.
	/// </exception>
	public IDataChangeEventProcessor Create(IDatabaseConfig dbConfig, SqlConnection cdcConnection, SqlConnection stateStoreConnection)
	{
		ArgumentNullException.ThrowIfNull(dbConfig);
		ArgumentNullException.ThrowIfNull(cdcConnection);
		ArgumentNullException.ThrowIfNull(stateStoreConnection);

		var logger = _serviceProvider.GetRequiredService<ILogger<DataChangeEventProcessor>>();

		return new DataChangeEventProcessor(
			_appLifetime,
			dbConfig,
			cdcConnection,
			stateStoreConnection,
			_serviceProvider,
			_policyFactory,
			logger);
	}
}
