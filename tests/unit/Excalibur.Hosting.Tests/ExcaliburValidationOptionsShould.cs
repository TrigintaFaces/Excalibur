using Excalibur.Hosting.Configuration;

namespace Excalibur.Hosting.Tests;

/// <summary>
/// Unit tests for ExcaliburValidationOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExcaliburValidationOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new ExcaliburValidationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.FailFast.ShouldBeTrue();
		options.ValidateDatabases.ShouldBeTrue();
		options.TestDatabaseConnections.ShouldBeFalse();
		_ = options.DatabaseConnections.ShouldNotBeNull();
		options.DatabaseConnections.ShouldBeEmpty();
		options.ValidateCloudProviders.ShouldBeTrue();
		options.UseAws.ShouldBeFalse();
		options.UseAzure.ShouldBeFalse();
		options.UseGoogleCloud.ShouldBeFalse();
		options.ValidateMessageBrokers.ShouldBeTrue();
		options.UseRabbitMq.ShouldBeFalse();
		options.UseKafka.ShouldBeFalse();
	}

	[Fact]
	public void Enabled_CanBeDisabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void FailFast_CanBeDisabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.FailFast = false;

		// Assert
		options.FailFast.ShouldBeFalse();
	}

	[Fact]
	public void ValidateDatabases_CanBeDisabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.ValidateDatabases = false;

		// Assert
		options.ValidateDatabases.ShouldBeFalse();
	}

	[Fact]
	public void TestDatabaseConnections_CanBeEnabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.TestDatabaseConnections = true;

		// Assert
		options.TestDatabaseConnections.ShouldBeTrue();
	}

	[Fact]
	public void UseAws_CanBeEnabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.UseAws = true;

		// Assert
		options.UseAws.ShouldBeTrue();
	}

	[Fact]
	public void UseRabbitMq_CanBeEnabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.UseRabbitMq = true;

		// Assert
		options.UseRabbitMq.ShouldBeTrue();
	}

	[Fact]
	public void UseKafka_CanBeEnabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.UseKafka = true;

		// Assert
		options.UseKafka.ShouldBeTrue();
	}
}
