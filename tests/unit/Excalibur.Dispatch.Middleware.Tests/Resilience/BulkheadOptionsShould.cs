using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for BulkheadOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BulkheadOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new BulkheadOptions();

		// Assert
		options.MaxConcurrency.ShouldBe(10);
		options.MaxQueueLength.ShouldBe(50);
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.AllowQueueing.ShouldBeTrue();
		options.PrioritySelector.ShouldBeNull();
	}

	[Fact]
	public void MaxConcurrency_CanBeCustomized()
	{
		// Arrange
		var options = new BulkheadOptions();

		// Act
		options.MaxConcurrency = 20;

		// Assert
		options.MaxConcurrency.ShouldBe(20);
	}

	[Fact]
	public void MaxQueueLength_CanBeCustomized()
	{
		// Arrange
		var options = new BulkheadOptions();

		// Act
		options.MaxQueueLength = 100;

		// Assert
		options.MaxQueueLength.ShouldBe(100);
	}

	[Fact]
	public void OperationTimeout_CanBeCustomized()
	{
		// Arrange
		var options = new BulkheadOptions();

		// Act
		options.OperationTimeout = TimeSpan.FromMinutes(2);

		// Assert
		options.OperationTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void AllowQueueing_CanBeDisabled()
	{
		// Arrange
		var options = new BulkheadOptions();

		// Act
		options.AllowQueueing = false;

		// Assert
		options.AllowQueueing.ShouldBeFalse();
	}

	[Fact]
	public void PrioritySelector_CanBeSetWithCustomFunction()
	{
		// Arrange
		var options = new BulkheadOptions();
		Func<object?, int> selector = obj => obj is string s ? s.Length : 0;

		// Act
		options.PrioritySelector = selector;

		// Assert
		_ = options.PrioritySelector.ShouldNotBeNull();
		options.PrioritySelector("test").ShouldBe(4);
		options.PrioritySelector(null).ShouldBe(0);
	}

	[Fact]
	public void MaxConcurrency_CanBeSetToOne()
	{
		// Arrange
		var options = new BulkheadOptions();

		// Act
		options.MaxConcurrency = 1;

		// Assert
		options.MaxConcurrency.ShouldBe(1);
	}

	[Fact]
	public void MaxQueueLength_CanBeSetToZero()
	{
		// Arrange
		var options = new BulkheadOptions();

		// Act
		options.MaxQueueLength = 0;

		// Assert
		options.MaxQueueLength.ShouldBe(0);
	}

	[Fact]
	public void OperationTimeout_CanBeSetToSmallValue()
	{
		// Arrange
		var options = new BulkheadOptions();

		// Act
		options.OperationTimeout = TimeSpan.FromMilliseconds(100);

		// Assert
		options.OperationTimeout.ShouldBe(TimeSpan.FromMilliseconds(100));
	}
}
