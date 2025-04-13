using Excalibur.Jobs;
using Excalibur.Tests.Shared;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs;

public class IConfigurableJobShould
{
	[Fact]
	public void BeGenericInterface()
	{
		// Arrange
		var job = A.Fake<IConfigurableJob<TestJobConfig>>();

		// Act & Assert
		_ = job.ShouldBeAssignableTo<IConfigurableJob<TestJobConfig>>();
	}

	[Fact]
	public void ConstrainConfigTypeToIJobConfig()
	{
		// Arrange
		var config = A.Fake<TestJobConfig>();

		// Act & Assert
		_ = config.ShouldBeAssignableTo<IJobConfig>();
	}

	[Fact]
	public void SupportDifferentConfigTypes()
	{
		// Arrange
		var basicJob = A.Fake<IConfigurableJob<TestJobConfig>>();
		var advancedJob = A.Fake<IConfigurableJob<AdvancedTestJobConfig>>();

		// Act & Assert
		_ = basicJob.ShouldBeAssignableTo<IConfigurableJob<TestJobConfig>>();
		_ = advancedJob.ShouldBeAssignableTo<IConfigurableJob<AdvancedTestJobConfig>>();
	}
}
