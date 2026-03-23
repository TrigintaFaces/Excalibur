using Npgsql;

using Excalibur.Data.Postgres;
namespace Excalibur.Data.Tests.Postgres;

/// <summary>
/// Unit tests for PostgresProviderOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
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
		options.Advanced.PrepareStatements.ShouldBeTrue();
		options.Advanced.MaxAutoPrepare.ShouldBe(20);
		options.Advanced.AutoPrepareMinUsages.ShouldBe(2);
		options.Pool.MaxPoolSize.ShouldBe(100);
		options.Pool.MinPoolSize.ShouldBe(0);
		options.Pool.EnablePooling.ShouldBeTrue();
		options.ApplicationName.ShouldBeNull();
		options.Advanced.KeepAlive.ShouldBe(30);
		options.Pool.ConnectionIdleLifetime.ShouldBe(300);
		options.Pool.ConnectionPruningInterval.ShouldBe(10);
		options.Advanced.UseSsl.ShouldBeFalse();
		options.Advanced.SslMode.ShouldBe(SslMode.Prefer);
		options.IncludeErrorDetail.ShouldBeTrue();
		options.Advanced.EnableJsonb.ShouldBeTrue();
		options.UseDataSource.ShouldBeTrue();
		options.RetryCount.ShouldBe(3);
		options.Pool.OpenConnectionImmediately.ShouldBeFalse();
		options.Pool.ClearPoolOnDispose.ShouldBeFalse();
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
		options.Pool.MaxPoolSize = 200;

		// Assert
		options.Pool.MaxPoolSize.ShouldBe(200);
	}

	[Fact]
	public void SslMode_CanBeChangedToRequire()
	{
		// Arrange
		var options = new PostgresProviderOptions();

		// Act
		options.Advanced.SslMode = SslMode.Require;

		// Assert
		options.Advanced.SslMode.ShouldBe(SslMode.Require);
	}

	[Fact]
	public void EnableJsonb_CanBeDisabled()
	{
		// Arrange
		var options = new PostgresProviderOptions();

		// Act
		options.Advanced.EnableJsonb = false;

		// Assert
		options.Advanced.EnableJsonb.ShouldBeFalse();
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
