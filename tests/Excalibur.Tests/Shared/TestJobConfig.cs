using Excalibur.Jobs;

using Quartz;

namespace Excalibur.Tests.Shared;

public class TestJobConfig : IJobConfig
{
	public string JobName { get; init; } = "TestJob";
	public string JobGroup { get; init; } = "TestGroup";
	public string CronSchedule { get; init; } = "0 */5 * * *";
	public TimeSpan DegradedThreshold { get; init; } = TimeSpan.FromMinutes(10);
	public bool Disabled { get; init; }
	public TimeSpan UnhealthyThreshold { get; init; } = TimeSpan.FromMinutes(30);

	// Additional properties
	public bool IsEnabled { get; set; } = true;

	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);
}

public class AdvancedTestJobConfig : IJobConfig
{
	public string JobName { get; init; } = "AdvancedTestJob";
	public string JobGroup { get; init; } = "AdvancedTestGroup";
	public string CronSchedule { get; init; } = "0 0 * * *";
	public TimeSpan DegradedThreshold { get; init; } = TimeSpan.FromMinutes(15);
	public bool Disabled { get; init; }
	public TimeSpan UnhealthyThreshold { get; init; } = TimeSpan.FromHours(1);

	// Additional properties
	public bool IsEnabled { get; set; } = true;

	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(10);
	public int MaxAttempts { get; set; } = 3;
}

public class TestJob : IConfigurableJob<TestJobConfig>
{
	public Task Execute(IJobExecutionContext _) => Task.CompletedTask;
}
