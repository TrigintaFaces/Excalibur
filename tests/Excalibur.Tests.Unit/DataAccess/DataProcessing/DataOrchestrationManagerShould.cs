using System.Data;

using Dapper;

using Excalibur.DataAccess;
using Excalibur.DataAccess.DataProcessing;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.DataProcessing;

public class DataOrchestrationManagerShould
{
	private readonly IDataProcessorDb _db;
	private readonly IDataProcessorRegistry _processorRegistry;
	private readonly IServiceProvider _serviceProvider;
	private readonly IOptions<DataProcessingConfiguration> _configuration;
	private readonly ILogger<DataOrchestrationManager> _logger;
	private readonly IDbConnection _connection;
	private readonly DataOrchestrationManager _manager;
	private readonly DataProcessingConfiguration _config;

	public DataOrchestrationManagerShould()
	{
		_connection = A.Fake<IDbConnection>();
		_db = A.Fake<IDataProcessorDb>();
		_processorRegistry = A.Fake<IDataProcessorRegistry>();
		_serviceProvider = A.Fake<IServiceProvider>();
		_config = new DataProcessingConfiguration { TableName = "TestDataTasks", MaxAttempts = 3 };
		_configuration = Options.Create(_config);
		_logger = A.Fake<ILogger<DataOrchestrationManager>>();

		_ = A.CallTo(() => _db.Connection).Returns(_connection);
		_ = A.CallTo(() => _connection.Ready()).Returns(_connection);

		_manager = new DataOrchestrationManager(
			_db,
			_processorRegistry,
			_serviceProvider,
			_configuration,
			_logger);
	}

	[Fact]
	public void ConstructWithValidParameters()
	{
		// Already done in constructor setup

		// Assert
		_ = _manager.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowIfDbIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DataOrchestrationManager(
			null!,
			_processorRegistry,
			_serviceProvider,
			_configuration,
			_logger));
	}

	[Fact]
	public void ThrowIfProcessorRegistryIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DataOrchestrationManager(
			_db,
			null!,
			_serviceProvider,
			_configuration,
			_logger));
	}

	[Fact]
	public void ThrowIfServiceProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DataOrchestrationManager(
			_db,
			_processorRegistry,
			null!,
			_configuration,
			_logger));
	}

	[Fact]
	public void ThrowIfConfigurationIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DataOrchestrationManager(
			_db,
			_processorRegistry,
			_serviceProvider,
			null!,
			_logger));
	}

	[Fact]
	public void ThrowIfLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DataOrchestrationManager(
			_db,
			_processorRegistry,
			_serviceProvider,
			_configuration,
			null!));
	}

	[Fact]
	public async Task AddDataTaskForRecordTypeInsertsIntoDatabase()
	{
		// Arrange
		var recordType = "TestRecord";
		_ = A.CallTo(() => _connection.ExecuteAsync(A<CommandDefinition>._))
			.Returns(1);

		// Act
		var result = await _manager.AddDataTaskForRecordType(recordType).ConfigureAwait(true);

		// Assert
		result.ShouldNotBe(Guid.Empty);
		_ = A.CallTo(() => _connection.ExecuteAsync(A<CommandDefinition>.That.Matches(cmd =>
				cmd.CommandText.Contains(_config.TableName) &&
				cmd.CommandText.Contains("INSERT INTO"))))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessDataTasksReturnImmediatelyWhenNoTasksFound()
	{
		// Arrange
		_ = A.CallTo(() => _connection.QueryAsync<DataTaskRequest>(A<CommandDefinition>._))
			.Returns(Enumerable.Empty<DataTaskRequest>());

		// Act
		await _manager.ProcessDataTasks().ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => _connection.QueryAsync<DataTaskRequest>(A<CommandDefinition>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _processorRegistry.TryGetFactory(A<string>._, out A<Func<IServiceProvider, IDataProcessor>>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ProcessDataTasksHandlesUnknownRecordTypes()
	{
		// Arrange
		var dataTaskId = Guid.NewGuid();
		var tasks = new List<DataTaskRequest>
		{
			new() { DataTaskId = dataTaskId, RecordType = "UnknownType", Attempts = 0, MaxAttempts = 3 }
		};

		_ = A.CallTo(() => _connection.QueryAsync<DataTaskRequest>(A<CommandDefinition>._))
			.Returns(tasks);

		Func<IServiceProvider, IDataProcessor> factory = null!;
		_ = A.CallTo(() => _processorRegistry.TryGetFactory("UnknownType", out factory))
			.Returns(false);

		_ = A.CallTo(() => _connection.ExecuteAsync(A<CommandDefinition>._))
			.Returns(1);

		// Act
		await _manager.ProcessDataTasks().ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => _processorRegistry.TryGetFactory("UnknownType", out factory))
			.MustHaveHappenedOnceExactly();

		_ = A.CallTo(() => _connection.ExecuteAsync(A<CommandDefinition>.That.Matches(cmd =>
				cmd.CommandText.Contains("UPDATE") &&
				cmd.CommandText.Contains("Attempts = @Attempts"))))
			.MustHaveHappenedOnceExactly();

		_ = A.CallTo(() => _logger.LogWarning(A<string>._, A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessDataTasksSuccessfullyProcessesAndDeletesTasks()
	{
		// Arrange
		var dataTaskId = Guid.NewGuid();
		var recordType = "ValidType";
		var tasks = new List<DataTaskRequest> { new() { DataTaskId = dataTaskId, RecordType = recordType, Attempts = 0, MaxAttempts = 3 } };

		_ = A.CallTo(() => _connection.QueryAsync<DataTaskRequest>(A<CommandDefinition>._))
			.Returns(tasks);

		var processor = A.Fake<IDataProcessor>();
		Func<IServiceProvider, IDataProcessor> factory = _ => processor;

		A.CallTo(() => _processorRegistry.TryGetFactory(recordType, out A<Func<IServiceProvider, IDataProcessor>>._))
			.Returns(true)
			.AssignsOutAndRefParameters(factory);

		A.CallTo(() => processor.RunAsync(A<long>._, A<Func<long, CancellationToken, Task>>._, A<CancellationToken>._))
			.Returns(100);

		var scope = A.Fake<IServiceScope>();
		var scopeProvider = A.Fake<IServiceProvider>();
		_ = A.CallTo(() => scope.ServiceProvider).Returns(scopeProvider);

		var scopeFactory = A.Fake<IServiceScopeFactory>();
		_ = A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);
		_ = A.CallTo(() => _serviceProvider.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory);

		_ = A.CallTo(() => _connection.ExecuteAsync(A<CommandDefinition>._))
			.Returns(1);

		// Act
		await _manager.ProcessDataTasks().ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => _processorRegistry.TryGetFactory(recordType, out A<Func<IServiceProvider, IDataProcessor>>._))
			.MustHaveHappenedOnceExactly();

		_ = A.CallTo(() => processor.RunAsync(A<long>._, A<Func<long, CancellationToken, Task>>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		_ = A.CallTo(() => processor.DisposeAsync())
			.MustHaveHappenedOnceExactly();

		_ = A.CallTo(() => _connection.ExecuteAsync(A<CommandDefinition>.That.Matches(cmd =>
				cmd.CommandText.Contains("DELETE") &&
				cmd.CommandText.Contains(_config.TableName))))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessDataTasksHandlesExceptionsAndIncrementsAttempts()
	{
		// Arrange
		var dataTaskId = Guid.NewGuid();
		var recordType = "ErrorType";
		var tasks = new List<DataTaskRequest> { new() { DataTaskId = dataTaskId, RecordType = recordType, Attempts = 0, MaxAttempts = 3 } };

		_ = A.CallTo(() => _connection.QueryAsync<DataTaskRequest>(A<CommandDefinition>._))
			.Returns(tasks);

		var processor = A.Fake<IDataProcessor>();
		Func<IServiceProvider, IDataProcessor> factory = _ => processor;

		A.CallTo(() => _processorRegistry.TryGetFactory(recordType, out A<Func<IServiceProvider, IDataProcessor>>._))
			.Returns(true)
			.AssignsOutAndRefParameters(factory);

		_ = A.CallTo(() => processor.RunAsync(A<long>._, A<Func<long, CancellationToken, Task>>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Test error"));

		var scope = A.Fake<IServiceScope>();
		var scopeProvider = A.Fake<IServiceProvider>();
		_ = A.CallTo(() => scope.ServiceProvider).Returns(scopeProvider);

		var scopeFactory = A.Fake<IServiceScopeFactory>();
		_ = A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);
		_ = A.CallTo(() => _serviceProvider.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory);

		_ = A.CallTo(() => _connection.ExecuteAsync(A<CommandDefinition>._))
			.Returns(1);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _manager.ProcessDataTasks().ConfigureAwait(true));

		// Verify attempts were incremented
		_ = A.CallTo(() => _connection.ExecuteAsync(A<CommandDefinition>.That.Matches(cmd =>
				cmd.CommandText.Contains("UPDATE") &&
				cmd.CommandText.Contains("Attempts = @Attempts"))))
			.MustHaveHappenedOnceExactly();

		// Verify the error was logged
		_ = A.CallTo(() => _logger.LogError(
				A<Exception>.That.IsInstanceOf(typeof(InvalidOperationException)),
				A<string>._,
				A<string>._,
				A<int>._))
			.MustHaveHappenedOnceExactly();

		// Verify processor is still disposed even in case of error
		_ = A.CallTo(() => processor.DisposeAsync())
			.MustHaveHappenedOnceExactly();
	}
}
