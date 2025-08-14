using Excalibur.Data.Outbox;
using Excalibur.Jobs;
using Excalibur.Jobs.Quartz.Outbox;
using FakeItEasy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shouldly;
using Xunit;

namespace Excalibur.Tests.Unit.Jobs.Quartz.Outbox;

public class OutboxJobShould
{
    [Fact]
    public void ConstructorShouldThrowArgumentNullExceptionWhenOutboxManagerIsNull()
    {
        // Arrange
        var logger = A.Fake<ILogger<OutboxJob>>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new OutboxJob(null!, logger))
            .ParamName.ShouldBe("outboxManager");
    }

    [Fact]
    public void ConstructorShouldThrowArgumentNullExceptionWhenLoggerIsNull()
    {
        // Arrange
        var outboxManager = A.Fake<IOutboxManager>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new OutboxJob(outboxManager, null!))
            .ParamName.ShouldBe("logger");
    }

    [Fact]
    public void ConfigureJobShouldThrowArgumentNullExceptionWhenConfiguratorIsNull()
    {
        // Arrange
        var configuration = A.Fake<IConfiguration>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => OutboxJob.ConfigureJob(null!, configuration))
            .ParamName.ShouldBe("configurator");
    }

    [Fact]
    public void ConfigureJobShouldThrowArgumentNullExceptionWhenConfigurationIsNull()
    {
        // Arrange
        var configurator = A.Fake<IServiceCollectionQuartzConfigurator>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => OutboxJob.ConfigureJob(configurator, null!))
            .ParamName.ShouldBe("configuration");
    }

    [Fact]
    public void ConfigureHealthChecksShouldThrowArgumentNullExceptionWhenHealthChecksIsNull()
    {
        // Arrange
        var configuration = A.Fake<IConfiguration>();
        var loggerFactory = A.Fake<ILoggerFactory>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            OutboxJob.ConfigureHealthChecks(null!, configuration, loggerFactory))
            .ParamName.ShouldBe("healthChecks");
    }

    [Fact]
    public void ConfigureHealthChecksShouldThrowArgumentNullExceptionWhenConfigurationIsNull()
    {
        // Arrange
        var healthChecks = A.Fake<IHealthChecksBuilder>();
        var loggerFactory = A.Fake<ILoggerFactory>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            OutboxJob.ConfigureHealthChecks(healthChecks, null!, loggerFactory))
            .ParamName.ShouldBe("configuration");
    }

    [Fact]
    public void ConfigureHealthChecksShouldThrowArgumentNullExceptionWhenLoggerFactoryIsNull()
    {
        // Arrange
        var healthChecks = A.Fake<IHealthChecksBuilder>();
        var configuration = A.Fake<IConfiguration>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            OutboxJob.ConfigureHealthChecks(healthChecks, configuration, null!))
            .ParamName.ShouldBe("loggerFactory");
    }

    [Fact]
    public void ConfigureJobShouldNotAddJobWhenDisabled()
    {
        // Arrange
        var configurator = A.Fake<IServiceCollectionQuartzConfigurator>();
        var configuration = A.Fake<IConfiguration>();
        var jobConfig = new OutboxJobConfig
        {
            JobName = "OutboxJob",
            JobGroup = "TestGroup",
            Disabled = true,
            CronSchedule = "0/15 * * * * ?"
        };
        
        A.CallTo(() => configuration.GetSection(OutboxJob.JobConfigSectionName).Get<OutboxJobConfig>())
            .Returns(jobConfig);

        // Act
        OutboxJob.ConfigureJob(configurator, configuration);

        // Assert
        A.CallTo(() => configurator.AddJob<OutboxJob>(A<JobKey>._, A<Action<IJobConfigurator>>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public void ConfigureJobShouldAddJobAndTriggerWhenEnabled()
    {
        // Arrange
        var configurator = A.Fake<IServiceCollectionQuartzConfigurator>();
        var configuration = A.Fake<IConfiguration>();
        var jobConfig = new OutboxJobConfig
        {
            JobName = "OutboxJob",
            JobGroup = "TestGroup",
            Disabled = false,
            CronSchedule = "0/15 * * * * ?"
        };
        
        A.CallTo(() => configuration.GetSection(OutboxJob.JobConfigSectionName).Get<OutboxJobConfig>())
            .Returns(jobConfig);

        // Setup for the nested configurator chains
        var jobConfigurator = A.Fake<IJobConfigurator>();
        A.CallTo(() => configurator.AddJob<OutboxJob>(A<JobKey>._, A<Action<IJobConfigurator>>._))
            .Invokes((JobKey key, Action<IJobConfigurator> action) => action(jobConfigurator))
            .Returns(jobConfigurator);

        var triggerConfigurator = A.Fake<ITriggerConfigurator>();
        A.CallTo(() => configurator.AddTrigger(A<Action<ITriggerConfigurator>>._))
            .Invokes((Action<ITriggerConfigurator> action) => action(triggerConfigurator));

        // Act
        OutboxJob.ConfigureJob(configurator, configuration);

        // Assert
        A.CallTo(() => configurator.AddJob<OutboxJob>(
                A<JobKey>.That.Matches(k => k.Name == jobConfig.JobName && k.Group == jobConfig.JobGroup),
                A<Action<IJobConfigurator>>._))
            .MustHaveHappenedOnceExactly();
        
        A.CallTo(() => configurator.AddTrigger(A<Action<ITriggerConfigurator>>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void ConfigureHealthChecksShouldAddHealthCheck()
    {
        // Arrange
        var healthChecks = A.Fake<IHealthChecksBuilder>();
        var configuration = A.Fake<IConfiguration>();
        var loggerFactory = A.Fake<ILoggerFactory>();
        var logger = A.Fake<ILogger<JobHealthCheck>>();
        
        var jobConfig = new OutboxJobConfig
        {
            JobName = "OutboxJob",
            JobGroup = "TestGroup",
            Disabled = false,
            CronSchedule = "0/15 * * * * ?"
        };
        
        A.CallTo(() => configuration.GetSection(OutboxJob.JobConfigSectionName).Get<OutboxJobConfig>())
            .Returns(jobConfig);
        
        A.CallTo(() => loggerFactory.CreateLogger<JobHealthCheck>())
            .Returns(logger);

        // Act
        OutboxJob.ConfigureHealthChecks(healthChecks, configuration, loggerFactory);

        // Assert
        A.CallTo(() => healthChecks.AddCheck(
                $"{jobConfig.JobName}HealthCheck", 
                A<JobHealthCheck>.That.Matches(hc => hc.JobName == jobConfig.JobName)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteShouldCallRunOutboxDispatchAsync()
    {
        // Arrange
        var outboxManager = A.Fake<IOutboxManager>();
        var logger = A.Fake<ILogger<OutboxJob>>();
        var job = new OutboxJob(outboxManager, logger);
        
        var jobExecutionContext = A.Fake<IJobExecutionContext>();
        var jobDetail = A.Fake<IJobDetail>();
        var jobKey = new JobKey("OutboxJob", "TestGroup");
        
        A.CallTo(() => jobExecutionContext.JobDetail).Returns(jobDetail);
        A.CallTo(() => jobDetail.Key).Returns(jobKey);
        
        A.CallTo(() => outboxManager.RunOutboxDispatchAsync(A<string>._))
            .Returns(Task.FromResult(10));

        // Act
        await job.Execute(jobExecutionContext).ConfigureAwait(true);

        // Assert
        A.CallTo(() => outboxManager.RunOutboxDispatchAsync(A<string>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ExecuteShouldCatchAndLogExceptions()
    {
        // Arrange
        var outboxManager = A.Fake<IOutboxManager>();
        var logger = A.Fake<ILogger<OutboxJob>>();
        var job = new OutboxJob(outboxManager, logger);
        
        var jobExecutionContext = A.Fake<IJobExecutionContext>();
        var jobDetail = A.Fake<IJobDetail>();
        var jobKey = new JobKey("OutboxJob", "TestGroup");
        
        A.CallTo(() => jobExecutionContext.JobDetail).Returns(jobDetail);
        A.CallTo(() => jobDetail.Key).Returns(jobKey);
        
        var expectedException = new InvalidOperationException("Test exception");
        A.CallTo(() => outboxManager.RunOutboxDispatchAsync(A<string>._))
            .Throws(expectedException);

        // Act - this should not throw even though RunOutboxDispatchAsync throws
        await job.Execute(jobExecutionContext).ConfigureAwait(true);

        // Assert - exception should be caught and logged
        A.CallTo(() => logger.Log(
            LogLevel.Error,
            A<EventId>._,
            A<object>.That.Matches(o => o.ToString().Contains("Test exception")),
            expectedException,
            A<Func<object, Exception, string>>._
        )).MustHaveHappened();
    }

    [Fact]
    public async Task ExecuteShouldThrowArgumentNullExceptionWhenContextIsNull()
    {
        // Arrange
        var outboxManager = A.Fake<IOutboxManager>();
        var logger = A.Fake<ILogger<OutboxJob>>();
        var job = new OutboxJob(outboxManager, logger);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await job.Execute(null!).ConfigureAwait(true))
            .ConfigureAwait(true);
    }

    [Fact]
    public async Task ExecuteShouldDisposeOutboxManagerWhenFinished()
    {
        // Arrange
        var outboxManager = A.Fake<IOutboxManager>();
        var logger = A.Fake<ILogger<OutboxJob>>();
        var job = new OutboxJob(outboxManager, logger);
        
        var jobExecutionContext = A.Fake<IJobExecutionContext>();
        var jobDetail = A.Fake<IJobDetail>();
        var jobKey = new JobKey("OutboxJob", "TestGroup");
        
        A.CallTo(() => jobExecutionContext.JobDetail).Returns(jobDetail);
        A.CallTo(() => jobDetail.Key).Returns(jobKey);

        // Act
        await job.Execute(jobExecutionContext).ConfigureAwait(true);

        // Assert
        A.CallTo(() => outboxManager.DisposeAsync()).MustHaveHappened();
    }
}