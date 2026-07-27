using Excalibur.Hosting.Configuration;

namespace Excalibur.Hosting.Tests;

/// <summary>
/// Unit tests for ExcaliburValidationOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
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
		options.Databases.Enabled.ShouldBeTrue();
		options.Databases.TestConnections.ShouldBeFalse();
		_ = options.Databases.Connections.ShouldNotBeNull();
		options.Databases.Connections.ShouldBeEmpty();
		options.CloudProviders.Enabled.ShouldBeTrue();
		options.CloudProviders.UseAws.ShouldBeFalse();
		options.CloudProviders.UseAzure.ShouldBeFalse();
		options.CloudProviders.UseGoogleCloud.ShouldBeFalse();
		options.MessageBrokers.Enabled.ShouldBeTrue();
		options.MessageBrokers.UseRabbitMq.ShouldBeFalse();
		options.MessageBrokers.UseKafka.ShouldBeFalse();
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
		options.Databases.Enabled = false;

		// Assert
		options.Databases.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void TestDatabaseConnections_CanBeEnabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.Databases.TestConnections = true;

		// Assert
		options.Databases.TestConnections.ShouldBeTrue();
	}

	[Fact]
	public void UseAws_CanBeEnabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.CloudProviders.UseAws = true;

		// Assert
		options.CloudProviders.UseAws.ShouldBeTrue();
	}

	[Fact]
	public void UseRabbitMq_CanBeEnabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.MessageBrokers.UseRabbitMq = true;

		// Assert
		options.MessageBrokers.UseRabbitMq.ShouldBeTrue();
	}

	[Fact]
	public void UseKafka_CanBeEnabled()
	{
		// Arrange
		var options = new ExcaliburValidationOptions();

		// Act
		options.MessageBrokers.UseKafka = true;

		// Assert
		options.MessageBrokers.UseKafka.ShouldBeTrue();
	}
}
