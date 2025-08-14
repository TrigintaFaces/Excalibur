using System.Collections.ObjectModel;

using Excalibur.DataAccess.SqlServer.Cdc;
using Excalibur.Jobs.Quartz.Cdc;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using Shouldly;

namespace Excalibur.Tests.Integration.Jobs.Quartz.Cdc;

public class CdcJobIntegrationTests : SqlServerPersistenceOnlyTestBase
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IConfiguration _configuration;
	private readonly ILoggerFactory _loggerFactory;

	public CdcJobIntegrationTests()
	{
		var services = new ServiceCollection();

		// Setup configuration with test values
		var configValues = new Dictionary<string, string>
		{
			["ConnectionStrings:TestDb"] = DbConnectionString,
			["ConnectionStrings:TestState"] = DbConnectionString,
			[$"{CdcJob.JobConfigSectionName}:JobName"] = "TestCdcJob",
			[$"{CdcJob.JobConfigSectionName}:JobGroup"] = "TestJobGroup",
			[$"{CdcJob.JobConfigSectionName}:CronSchedule"] = "0 */5 * * *",
			[$"{CdcJob.JobConfigSectionName}:Disabled"] = "false",
			[$"{CdcJob.JobConfigSectionName}:DegradedThreshold"] = "00:10:00",
			[$"{CdcJob.JobConfigSectionName}:UnhealthyThreshold"] = "00:30:00",
			[$"{CdcJob.JobConfigSectionName}:DatabaseConfigs:0:DatabaseName"] = "TestDb",
			[$"{CdcJob.JobConfigSectionName}:DatabaseConfigs:0:DatabaseConnectionIdentifier"] = "TestDb",
			[$"{CdcJob.JobConfigSectionName}:DatabaseConfigs:0:StateConnectionIdentifier"] = "TestState",
			[$"{CdcJob.JobConfigSectionName}:DatabaseConfigs:0:SchemaName"] = "dbo",
			[$"{CdcJob.JobConfigSectionName}:DatabaseConfigs:0:TableConfigs:0:TableName"] = "TestTable",
			[$"{CdcJob.JobConfigSectionName}:DatabaseConfigs:0:TableConfigs:0:CdcEnableDate"] = DateTimeOffset.UtcNow.AddDays(-1).ToString("O")
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configValues)
			.Build();

		services.AddSingleton<IConfiguration>(configuration);
		services.Configure<CdcJobConfig>(configuration.GetSection(CdcJob.JobConfigSectionName));

		// Add required services
		services.AddLogging(builder => builder.AddDebug());
		services.AddTransient<IDataChangeEventProcessorFactory, DataChangeEventProcessorFactory>();

		// Register the CdcJob
		services.AddTransient<CdcJob>();

		_serviceProvider = services.BuildServiceProvider();
		_configuration = _serviceProvider.GetRequiredService<IConfiguration>();
		_loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
	}

	[Fact(Skip = "This test requires actual CDC tables to be set up in SQL Server")]
	public async Task Execute_IntegratesWithActualDependencies()
	{
		// Arrange - Get the CdcJob from DI
		var cdcJob = _serviceProvider.GetRequiredService<CdcJob>();
		cdcJob.ShouldNotBeNull();

		// Create a mock execution context
		var context = new TestJobExecutionContext();

		// Act
		await cdcJob.Execute(context).ConfigureAwait(true);

		// No assertions - this test verifies the job executes without exceptions when using real dependencies
	}

	[Fact]
	public void ConfigureJob_RegistersJobAndTriggerCorrectly()
	{
		// Arrange
		var configurator = new TestQuartzConfigurator();

		// Act
		CdcJob.ConfigureJob(configurator, _configuration);

		// Assert
		configurator.JobsAdded.Count.ShouldBe(1);
		configurator.JobsAdded[0].JobType.ShouldBe(typeof(CdcJob));
		configurator.JobsAdded[0].JobKey.Name.ShouldBe("TestCdcJob");
		configurator.JobsAdded[0].JobKey.Group.ShouldBe("TestJobGroup");

		configurator.TriggersAdded.Count.ShouldBe(1);
		configurator.TriggersAdded[0].JobKey.Name.ShouldBe("TestCdcJob");
		configurator.TriggersAdded[0].JobKey.Group.ShouldBe("TestJobGroup");
	}

	[Fact]
	public void ConfigureHealthChecks_AddsHealthCheckWithCorrectName()
	{
		// Arrange
		var healthChecks = new TestHealthChecksBuilder();

		// Act
		CdcJob.ConfigureHealthChecks(healthChecks, _configuration, _loggerFactory);

		// Assert
		healthChecks.Checks.Count.ShouldBe(1);
		healthChecks.Checks[0].Name.ShouldBe("TestCdcJobHealthCheck");
	}

	#region Test Helper Classes

	private class TestJobExecutionContext : IJobExecutionContext
	{
		public TestJobExecutionContext()
		{
			JobDetail = new TestJobDetail
			{
				Key = new JobKey("TestCdcJob", "TestJobGroup")
			};
		}

		public IScheduler Scheduler => throw new NotImplementedException();
		public ITrigger Trigger => throw new NotImplementedException();
		public ICalendar? Calendar => throw new NotImplementedException();
		public bool Recovering => throw new NotImplementedException();
		public TriggerKey? RecoveringTriggerKey => throw new NotImplementedException();
		public int RefireCount => throw new NotImplementedException();
		public JobDataMap MergedJobDataMap => throw new NotImplementedException();
		public IJobDetail JobDetail { get; }
		public IJob? JobInstance => throw new NotImplementedException();
		public DateTimeOffset FireTimeUtc => throw new NotImplementedException();
		public DateTimeOffset? ScheduledFireTimeUtc => throw new NotImplementedException();
		public DateTimeOffset? PreviousFireTimeUtc => throw new NotImplementedException();
		public DateTimeOffset? NextFireTimeUtc => throw new NotImplementedException();
		public string FireInstanceId => throw new NotImplementedException();
		public object? Result { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public TimeSpan JobRunTime => throw new NotImplementedException();
		public CancellationToken CancellationToken => CancellationToken.None;

		public void Put(object key, object value) => throw new NotImplementedException();
		public object? Get(object key) => throw new NotImplementedException();
	}

	private class TestJobDetail : IJobDetail
	{
		public JobKey Key { get; set; } = null!;
		public string? Description { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public Type JobType => typeof(CdcJob);
		public JobDataMap JobDataMap => throw new NotImplementedException();
		public bool Durable => throw new NotImplementedException();
		public bool PersistJobDataAfterExecution => throw new NotImplementedException();
		public bool ConcurrentExecutionDisallowed => throw new NotImplementedException();
		public bool RequestsRecovery => throw new NotImplementedException();

		public IJobDetail GetJobBuilder() => throw new NotImplementedException();
		public JobBuilder GetJobBuilder(JobKey? key) => throw new NotImplementedException();
	}

	private class TestQuartzConfigurator : IServiceCollectionQuartzConfigurator
	{
		public List<TestJobDetail> JobsAdded { get; } = new();
		public List<TestTriggerDetail> TriggersAdded { get; } = new();

		public void AddJob<T>(JobKey jobKey, Action<IJobConfigurator> configure) where T : IJob
		{
			var jobDetail = new TestJobDetail
			{
				JobType = typeof(T),
				JobKey = jobKey
			};

			JobsAdded.Add(jobDetail);
		}

		public void AddTrigger(Action<ITriggerConfigurator> configure)
		{
			var triggerDetail = new TestTriggerDetail();
			var configurator = new TestTriggerConfigurator(triggerDetail);

			configure(configurator);

			TriggersAdded.Add(triggerDetail);
		}

		public IServiceCollectionQuartzConfigurator AddCalendar<T>(string calendarName, bool replace, bool updateTriggers) where T : ICalendar => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator AddCalendar(string calendarName, ICalendar calendar, bool replace, bool updateTriggers) => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator AddJob(Type jobType, JobKey jobKey, Action<IJobConfigurator> configure) => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator AddJob(Type jobType, string name, Action<IJobConfigurator> configure) => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator AddJob(Type jobType, string name, string group, Action<IJobConfigurator> configure) => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator AddJob<T>(string name, Action<IJobConfigurator> configure) where T : IJob => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator AddJob<T>(string name, string group, Action<IJobConfigurator> configure) where T : IJob => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator AddSchedulerListener<T>() where T : class, ISchedulerListener => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator AddTriggerListener<T>() where T : class, ITriggerListener => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator AddJobListener<T>() where T : class, IJobListener => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UseMicrosoftDependencyInjectionJobFactory() => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UseSimpleTypeLoader() => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UseDefaultThreadPool(Action<Quartz.Simpl.SimpleThreadPool> configure) => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UseThreadPool<T>(Action<T> configure) where T : class, IThreadPool => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UseDefaultThreadPool(Action<Quartz.Simpl.SimpleThreadPool> configure, IsolationLevel? isolationLevel) => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UseThreadPool<T>(Action<T> configure, IsolationLevel? isolationLevel) where T : class, IThreadPool => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UseInMemoryStore(Action<Quartz.Simpl.RAMJobStore>? configure = null) => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UseInMemoryStore(Action<Quartz.Simpl.RAMJobStore>? configure = null, IsolationLevel? isolationLevel = null) => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UseJobFactory<T>(Action<T>? configure = null) where T : class, IJobFactory => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UsePersistentStore<T>(Action<T>? configure = null) where T : class, IJobStore => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UsePersistentStore(Action<Quartz.Impl.AdoJobStore.JobStoreTX>? configure = null) => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UsePersistentStore<T>(Action<T>? configure = null, IsolationLevel? isolationLevel = null) where T : class, IJobStore => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator UsePersistentStore(Action<Quartz.Impl.AdoJobStore.JobStoreTX>? configure = null, IsolationLevel? isolationLevel = null) => throw new NotImplementedException();
		public IServiceCollectionQuartzConfigurator SetProperty(string name, string value) => throw new NotImplementedException();

		private class TestJobDetail
		{
			public Type JobType { get; set; } = null!;
			public JobKey JobKey { get; set; } = null!;
		}

		private class TestTriggerDetail
		{
			public JobKey JobKey { get; set; } = null!;
			public string? TriggerName { get; set; }
		}

		private class TestTriggerConfigurator : ITriggerConfigurator
		{
			private readonly TestTriggerDetail _triggerDetail;

			public TestTriggerConfigurator(TestTriggerDetail triggerDetail)
			{
				_triggerDetail = triggerDetail;
			}

			public ITriggerConfigurator ForJob(JobKey jobKey)
			{
				_triggerDetail.JobKey = jobKey;
				return this;
			}

			public ITriggerConfigurator ForJob(string jobName) => throw new NotImplementedException();
			public ITriggerConfigurator ForJob(string jobName, string jobGroup) => throw new NotImplementedException();
			public ITriggerConfigurator ForJob<T>() where T : IJob => throw new NotImplementedException();
			public ITriggerConfigurator StartAt(DateTimeOffset startTimeUtc) => this;
			public ITriggerConfigurator StartNow() => throw new NotImplementedException();
			public ITriggerConfigurator EndAt(DateTimeOffset? endTimeUtc) => throw new NotImplementedException();
			public ITriggerConfigurator WithIdentity(string name)
			{
				_triggerDetail.TriggerName = name;
				return this;
			}
			public ITriggerConfigurator WithIdentity(string name, string group) => throw new NotImplementedException();
			public ITriggerConfigurator WithIdentity(TriggerKey key) => throw new NotImplementedException();
			public ITriggerConfigurator WithDescription(string? description) => this;
			public ITriggerConfigurator WithPriority(int priority) => throw new NotImplementedException();
			public ITriggerConfigurator ModifiedByCalendar(string? calendarName) => throw new NotImplementedException();
			public ITriggerConfigurator WithSimpleSchedule(Action<SimpleScheduleBuilder> scheduleBuilder) => throw new NotImplementedException();
			public ITriggerConfigurator WithDailyTimeIntervalSchedule(Action<DailyTimeIntervalScheduleBuilder> scheduleBuilder) => throw new NotImplementedException();
			public ITriggerConfigurator WithCalendarIntervalSchedule(Action<CalendarIntervalScheduleBuilder> scheduleBuilder) => throw new NotImplementedException();
			public ITriggerConfigurator WithCronSchedule(string cronExpression) => this;
			public ITriggerConfigurator WithCronSchedule(string cronExpression, Action<CronScheduleBuilder>? scheduleBuilder) => throw new NotImplementedException();
			public ITriggerConfigurator WithSchedule(IScheduleBuilder scheduleBuilder) => throw new NotImplementedException();
		}
	}

	private class TestHealthChecksBuilder : IHealthChecksBuilder
	{
		public List<TestHealthCheck> Checks { get; } = new();
		public IServiceCollection Services => throw new NotImplementedException();

		public IHealthChecksBuilder Add(HealthCheckRegistration registration)
		{
			Checks.Add(new TestHealthCheck
			{
				Name = registration.Name
			});

			return this;
		}

		private class TestHealthCheck
		{
			public string Name { get; set; } = string.Empty;
		}
	}

	#endregion
}
