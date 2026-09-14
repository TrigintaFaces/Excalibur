using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for PollyRetryOptions configuration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class PollyRetryOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new PollyRetryOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
		options.UseJitter.ShouldBeTrue();
		options.ShouldRetry.ShouldBeNull();
	}

	[Fact]
	public void MaxRetries_CanBeCustomized()
	{
		// Arrange
		var options = new PollyRetryOptions();

		// Act
		options.MaxRetryAttempts = 5;

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void BaseDelay_CanBeCustomized()
	{
		// Arrange
		var options = new PollyRetryOptions();

		// Act
		options.BaseDelay = TimeSpan.FromMilliseconds(500);

		// Assert
		options.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void BackoffStrategy_CanBeChangedToLinear()
	{
		// Arrange
		var options = new PollyRetryOptions();

		// Act
		options.BackoffStrategy = BackoffStrategy.Linear;

		// Assert
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Linear);
	}

	[Fact]
	public void BackoffStrategy_CanBeChangedToFixed()
	{
		// Arrange
		var options = new PollyRetryOptions();

		// Act
		options.BackoffStrategy = BackoffStrategy.Fixed;

		// Assert
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Fixed);
	}

	[Fact]
	public void UseJitter_CanBeDisabled()
	{
		// Arrange
		var options = new PollyRetryOptions();

		// Act
		options.UseJitter = false;

		// Assert
		options.UseJitter.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_CanBeSetWithCustomPredicate()
	{
		// Arrange
		var options = new PollyRetryOptions();
		Func<Exception, bool> predicate = ex => ex is TimeoutException;

		// Act
		options.ShouldRetry = predicate;

		// Assert
		_ = options.ShouldRetry.ShouldNotBeNull();
		options.ShouldRetry(new TimeoutException()).ShouldBeTrue();
		options.ShouldRetry(new InvalidOperationException()).ShouldBeFalse();
	}

	[Fact]
	public void MaxRetries_CanBeSetToZero()
	{
		// Arrange
		var options = new PollyRetryOptions();

		// Act
		options.MaxRetryAttempts = 0;

		// Assert
		options.MaxRetryAttempts.ShouldBe(0);
	}

	[Fact]
	public void BaseDelay_CanBeSetToZero()
	{
		// Arrange
		var options = new PollyRetryOptions();

		// Act
		options.BaseDelay = TimeSpan.Zero;

		// Assert
		options.BaseDelay.ShouldBe(TimeSpan.Zero);
	}
}
