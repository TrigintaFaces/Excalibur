using System.Data;

using Excalibur.DataAccess.SqlServer.Cdc;
using Excalibur.Jobs;
using Excalibur.Jobs.Quartz.Cdc;

using FakeItEasy;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs.Quartz.Cdc;

public class CdcJobShould
{
	private readonly IConfiguration _configuration;
	private readonly IDataChangeEventProcessorFactory _factory;
	private readonly IOptions<CdcJobConfig> _cdcConfigOptions;
	private readonly ILogger<CdcJob> _logger;
	private readonly IJobExecutionContext _context;
	private readonly JobKey _jobKey;
	private readonly IDbConnection _fakeDbConnection;
	private readonly IDataChangeEventProcessor _processor;
	private readonly CdcJobConfig _cdcJobConfig;

	public CdcJobShould()
	{
		_configuration = A.Fake<IConfiguration>();
		_factory = A.Fake<IDataChangeEventProcessorFactory>();
		_logger = A.Fake<ILogger<CdcJob>>();
		_context = A.Fake<IJobExecutionContext>();
		_jobKey = new JobKey("TestCdcJob", "TestGroup");
		_fakeDbConnection = A.Fake<IDbConnection>();
		_processor = A.Fake<IDataChangeEventProcessor>();

		// Setup job key
		var jobDetail = A.Fake<IJobDetail>();
		A.CallTo(() => jobDetail.Key).Returns(_jobKey);
		A.CallTo(() => _context.JobDetail).Returns(jobDetail);

		// Setup CDC job config
		_cdcJobConfig = CdcJobTestHelpers.CreateCdcJobConfig();

		_cdcConfigOptions = Options.Create(_cdcJobConfig);

		// Setup connection factory - using the extension method defined in CdcJobTestHelpers
		A.CallTo(() => _configuration.GetSqlConnection(A<string>._))
			.ReturnsLazily((string _) => _fakeDbConnection);

		// Setup processor
		A.CallTo(() => _factory.Create(A<DatabaseConfig>._, A<IDbConnection>._, A<IDbConnection>._))
			.Returns(_processor);

		A.CallTo(() => _processor.ProcessCdcChangesAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(10)); // Return 10 processed events
	}

	[Fact]
	public void ConstructWithValidParameters()
	{
		// Act
		var job = new CdcJob(
			_configuration,
			_factory,
			_cdcConfigOptions,
			_logger);

		// Assert
		job.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowIfConfigurationIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new CdcJob(
			null!,
			_factory,
			_cdcConfigOptions,
			_logger));
	}

	[Fact]
	public void ThrowIfFactoryIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new CdcJob(
			_configuration,
			null!,
			_cdcConfigOptions,
			_logger));
	}

	[Fact]
	public void ThrowIfOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new CdcJob(
			_configuration,
			_factory,
			null!,
			_logger));
	}

	[Fact]
	public void ThrowIfLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new CdcJob(
			_configuration,
			_factory,
			_cdcConfigOptions,
			null!));
	}

	[Fact]
	public async Task ExecuteProcessesChangesForAllConfiguredDatabases()
	{
		// Arrange
		var job = new CdcJob(
			_configuration,
			_factory,
			_cdcConfigOptions,
			_logger);

		// Act
		await job.Execute(_context).ConfigureAwait(true);

		// Assert
		A.CallTo(() => _configuration.GetSqlConnection("TestDbConnection")).MustHaveHappenedOnceExactly();
		A.CallTo(() => _configuration.GetSqlConnection("TestStateConnection")).MustHaveHappenedOnceExactly();
		A.CallTo(() => _factory.Create(A<DatabaseConfig>._, A<IDbConnection>._, A<IDbConnection>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _processor.ProcessCdcChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _processor.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteHandlesExceptionsDuringProcessing()
	{
		// Arrange
		A.CallTo(() => _processor.ProcessCdcChangesAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("Test exception"));

		var job = new CdcJob(
			_configuration,
			_factory,
			_cdcConfigOptions,
			_logger);

		// Act - Should not throw exception
		await job.Execute(_context).ConfigureAwait(true);

		// Assert - Exception should be logged
		A.CallTo(() => _logger.LogError(
			A<Exception>.That.IsInstanceOf(typeof(InvalidOperationException)),
			A<string>._,
			A<object[]>._)).MustHaveHappened();

		// Cleanup should still happen
		A.CallTo(() => _processor.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ConfigureJobRegistersJobAndTriggerWithQuartz()
	{
		// Arrange
		var configurator = A.Fake<IServiceCollectionQuartzConfigurator>();
		var configuration = A.Fake<IConfiguration>();
		var jobConfig = CdcJobTestHelpers.CreateCdcJobConfig();

		A.CallTo(() => configuration.GetJobConfiguration<CdcJobConfig>(CdcJob.JobConfigSectionName))
			.Returns(jobConfig);

		// Act
		CdcJob.ConfigureJob(configurator, configuration);

		// Assert
		A.CallTo(() => configurator.AddJob<CdcJob>(
			A<JobKey>.That.Matches(k => k.Name == "TestCdcJob" && k.Group == "TestGroup"),
			A<Action<IJobConfigurator>>._)).MustHaveHappenedOnceExactly();

		A.CallTo(() => configurator.AddTrigger(A<Action<ITriggerConfigurator>>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ConfigureHealthChecksAddsHealthCheckForJob()
	{
		// Arrange
		var healthChecks = new TestHealthChecksBuilder();
		var configuration = A.Fake<IConfiguration>();
		var loggerFactory = A.Fake<ILoggerFactory>();
		var jobConfig = CdcJobTestHelpers.CreateCdcJobConfig();

		A.CallTo(() => configuration.GetJobConfiguration<CdcJobConfig>(CdcJob.JobConfigSectionName))
			.Returns(jobConfig);

		A.CallTo(() => loggerFactory.CreateLogger<JobHealthCheck>())
			.Returns(A.Fake<ILogger<JobHealthCheck>>());

		// Act
		CdcJob.ConfigureHealthChecks(healthChecks, configuration, loggerFactory);

		// Assert
		healthChecks.Registrations.Count.ShouldBe(1);
		healthChecks.Registrations[0].Name.ShouldBe("TestCdcJobHealthCheck");
	}

	[Fact]
	public void ConfigureJobThrowsIfConfiguratorIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => CdcJob.ConfigureJob(null!, _configuration));
	}

	[Fact]
	public void ConfigureJobThrowsIfConfigurationIsNull()
	{
		// Arrange
		var configurator = A.Fake<IServiceCollectionQuartzConfigurator>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => CdcJob.ConfigureJob(configurator, null!));
	}

	[Fact]
	public void ConfigureHealthChecksThrowsIfHealthChecksIsNull()
	{
		// Arrange
		var loggerFactory = A.Fake<ILoggerFactory>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => CdcJob.ConfigureHealthChecks(null!, _configuration, loggerFactory));
	}

	[Fact]
	public void ConfigureHealthChecksThrowsIfConfigurationIsNull()
	{
		// Arrange
		var healthChecks = new TestHealthChecksBuilder();
		var loggerFactory = A.Fake<ILoggerFactory>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => CdcJob.ConfigureHealthChecks(healthChecks, null!, loggerFactory));
	}

	[Fact]
	public void ConfigureHealthChecksThrowsIfLoggerFactoryIsNull()
	{
		// Arrange
		var healthChecks = new TestHealthChecksBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => CdcJob.ConfigureHealthChecks(healthChecks, _configuration, null!));
	}
}
