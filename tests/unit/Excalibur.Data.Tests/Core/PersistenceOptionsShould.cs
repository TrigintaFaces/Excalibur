using System.Data;

using Excalibur.Data.Persistence;

using Excalibur.Data;
namespace Excalibur.Data.Tests.Core;

/// <summary>
/// Unit tests for PersistenceOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PersistenceOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new PersistenceOptions();

		// Assert
		options.EnableTracing.ShouldBeTrue();
		options.EnableMetrics.ShouldBeTrue();
		options.EnableSensitiveDataLogging.ShouldBeFalse();
		options.DefaultCommandTimeout.ShouldBe(30);
		options.DefaultIsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		options.EnableAutoRetry.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryDelayMilliseconds.ShouldBe(100);
	}

	[Fact]
	public void EnableTracing_CanBeDisabled()
	{
		// Arrange
		var options = new PersistenceOptions();

		// Act
		options.EnableTracing = false;

		// Assert
		options.EnableTracing.ShouldBeFalse();
	}

	[Fact]
	public void DefaultCommandTimeout_CanBeCustomized()
	{
		// Arrange
		var options = new PersistenceOptions();

		// Act
		options.DefaultCommandTimeout = 60;

		// Assert
		options.DefaultCommandTimeout.ShouldBe(60);
	}

	[Fact]
	public void DefaultIsolationLevel_CanBeChangedToSerializable()
	{
		// Arrange
		var options = new PersistenceOptions();

		// Act
		options.DefaultIsolationLevel = IsolationLevel.Serializable;

		// Assert
		options.DefaultIsolationLevel.ShouldBe(IsolationLevel.Serializable);
	}

	[Fact]
	public void EnableAutoRetry_CanBeDisabled()
	{
		// Arrange
		var options = new PersistenceOptions();

		// Act
		options.EnableAutoRetry = false;

		// Assert
		options.EnableAutoRetry.ShouldBeFalse();
	}

	[Fact]
	public void MaxRetryAttempts_CanBeCustomized()
	{
		// Arrange
		var options = new PersistenceOptions();

		// Act
		options.MaxRetryAttempts = 5;

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void RetryDelayMilliseconds_CanBeCustomized()
	{
		// Arrange
		var options = new PersistenceOptions();

		// Act
		options.RetryDelayMilliseconds = 500;

		// Assert
		options.RetryDelayMilliseconds.ShouldBe(500);
	}
}
