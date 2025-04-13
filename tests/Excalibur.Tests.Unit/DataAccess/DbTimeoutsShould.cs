using Excalibur.DataAccess;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class DbTimeoutsShould
{
	[Fact]
	public void HaveCorrectRegularTimeoutValue()
	{
		// Arrange & Act
		var regularTimeout = DbTimeouts.RegularTimeoutSeconds;

		// Assert
		regularTimeout.ShouldBe(60);
	}

	[Fact]
	public void HaveCorrectLongRunningTimeoutValue()
	{
		// Arrange & Act
		var longRunningTimeout = DbTimeouts.LongRunningTimeoutSeconds;

		// Assert
		longRunningTimeout.ShouldBe(600);
	}

	[Fact]
	public void HaveCorrectExtraLongRunningTimeoutValue()
	{
		// Arrange & Act
		var extraLongRunningTimeout = DbTimeouts.ExtraLongRunningTimeoutSeconds;

		// Assert
		extraLongRunningTimeout.ShouldBe(1200);
	}

	[Fact]
	public void MaintainConsistentRelationshipBetweenTimeoutValues()
	{
		// Arrange & Act
		var regularTimeout = DbTimeouts.RegularTimeoutSeconds;
		var longRunningTimeout = DbTimeouts.LongRunningTimeoutSeconds;
		var extraLongRunningTimeout = DbTimeouts.ExtraLongRunningTimeoutSeconds;

		// Assert
		longRunningTimeout.ShouldBeGreaterThan(regularTimeout);
		extraLongRunningTimeout.ShouldBeGreaterThan(longRunningTimeout);

		// The long running timeout should be 10x the regular timeout
		longRunningTimeout.ShouldBe(regularTimeout * 10);

		// The extra long running timeout should be 2x the long running timeout
		extraLongRunningTimeout.ShouldBe(longRunningTimeout * 2);
	}
}
