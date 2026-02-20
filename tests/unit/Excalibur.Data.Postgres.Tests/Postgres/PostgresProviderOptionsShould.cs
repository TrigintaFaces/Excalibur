using Npgsql;

using Excalibur.Data.Postgres;
namespace Excalibur.Data.Tests.Postgres;

/// <summary>
/// Unit tests for PostgresProviderOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PostgresProviderOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new PostgresProviderOptions();

		// Assert
		options.Name.ShouldBeNull();
		options.ConnectionString.ShouldBe(string.Empty);
		options.CommandTimeout.ShouldBe(30);
		options.ConnectTimeout.ShouldBe(15);
		options.PrepareStatements.ShouldBeTrue();
		options.MaxAutoPrepare.ShouldBe(20);
		options.AutoPrepareMinUsages.ShouldBe(2);
		options.MaxPoolSize.ShouldBe(100);
		options.MinPoolSize.ShouldBe(0);
		options.EnablePooling.ShouldBeTrue();
		options.ApplicationName.ShouldBeNull();
		options.KeepAlive.ShouldBe(30);
		options.ConnectionIdleLifetime.ShouldBe(300);
		options.ConnectionPruningInterval.ShouldBe(10);
		options.UseSsl.ShouldBeFalse();
		options.SslMode.ShouldBe(SslMode.Prefer);
		options.IncludeErrorDetail.ShouldBeTrue();
		options.EnableJsonb.ShouldBeTrue();
		options.UseDataSource.ShouldBeTrue();
		options.RetryCount.ShouldBe(3);
		options.OpenConnectionImmediately.ShouldBeFalse();
		options.ClearPoolOnDispose.ShouldBeFalse();
	}

	[Fact]
	public void ConnectionString_CanBeSet()
	{
		// Arrange
		var options = new PostgresProviderOptions();

		// Act
		options.ConnectionString = "Host=localhost;Database=test;";

		// Assert
		options.ConnectionString.ShouldBe("Host=localhost;Database=test;");
	}

	[Fact]
	public void MaxPoolSize_CanBeCustomized()
	{
		// Arrange
		var options = new PostgresProviderOptions();

		// Act
		options.MaxPoolSize = 200;

		// Assert
		options.MaxPoolSize.ShouldBe(200);
	}

	[Fact]
	public void SslMode_CanBeChangedToRequire()
	{
		// Arrange
		var options = new PostgresProviderOptions();

		// Act
		options.SslMode = SslMode.Require;

		// Assert
		options.SslMode.ShouldBe(SslMode.Require);
	}

	[Fact]
	public void EnableJsonb_CanBeDisabled()
	{
		// Arrange
		var options = new PostgresProviderOptions();

		// Act
		options.EnableJsonb = false;

		// Assert
		options.EnableJsonb.ShouldBeFalse();
	}

	[Fact]
	public void RetryCount_CanBeCustomized()
	{
		// Arrange
		var options = new PostgresProviderOptions();

		// Act
		options.RetryCount = 5;

		// Assert
		options.RetryCount.ShouldBe(5);
	}
}
