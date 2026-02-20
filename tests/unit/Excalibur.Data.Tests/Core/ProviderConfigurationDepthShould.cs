using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProviderConfigurationDepthShould
{
    [Fact]
    public void DefaultValues_SetCorrectly()
    {
        // Act
        var config = new ProviderConfiguration
        {
            Name = "test",
            Type = PersistenceProviderType.SqlServer,
            ConnectionString = "Server=localhost",
        };

        // Assert
        config.Name.ShouldBe("test");
        config.Type.ShouldBe(PersistenceProviderType.SqlServer);
        config.ConnectionString.ShouldBe("Server=localhost");
        config.IsReadOnly.ShouldBeFalse();
        config.MaxPoolSize.ShouldBe(100);
        config.ConnectionTimeout.ShouldBe(30);
        config.CommandTimeout.ShouldBe(30);
        config.EnableConnectionPooling.ShouldBeTrue();
        config.MinPoolSize.ShouldBe(0);
        config.MaxRetryAttempts.ShouldBe(3);
        config.RetryDelayMilliseconds.ShouldBe(1000);
        config.EnableDetailedLogging.ShouldBeFalse();
        config.EnableMetrics.ShouldBeTrue();
        config.ProviderSpecificOptions.ShouldNotBeNull();
        config.ProviderSpecificOptions.ShouldBeEmpty();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        // Arrange & Act
        var config = new ProviderConfiguration
        {
            Name = "primary",
            Type = PersistenceProviderType.Postgres,
            ConnectionString = "Host=pg;Database=test",
            IsReadOnly = true,
            MaxPoolSize = 50,
            ConnectionTimeout = 15,
            CommandTimeout = 60,
            EnableConnectionPooling = false,
            MinPoolSize = 5,
            MaxRetryAttempts = 5,
            RetryDelayMilliseconds = 500,
            EnableDetailedLogging = true,
            EnableMetrics = false,
        };

        // Assert
        config.Name.ShouldBe("primary");
        config.Type.ShouldBe(PersistenceProviderType.Postgres);
        config.ConnectionString.ShouldBe("Host=pg;Database=test");
        config.IsReadOnly.ShouldBeTrue();
        config.MaxPoolSize.ShouldBe(50);
        config.ConnectionTimeout.ShouldBe(15);
        config.CommandTimeout.ShouldBe(60);
        config.EnableConnectionPooling.ShouldBeFalse();
        config.MinPoolSize.ShouldBe(5);
        config.MaxRetryAttempts.ShouldBe(5);
        config.RetryDelayMilliseconds.ShouldBe(500);
        config.EnableDetailedLogging.ShouldBeTrue();
        config.EnableMetrics.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ThrowOnEmptyConnectionString()
    {
        // Arrange
        var config = new ProviderConfiguration
        {
            Name = "test",
            Type = PersistenceProviderType.SqlServer,
            ConnectionString = "",
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => config.Validate())
            .Message.ShouldContain("Connection string is required");
    }

    [Fact]
    public void Validate_ThrowOnWhitespaceConnectionString()
    {
        // Arrange
        var config = new ProviderConfiguration
        {
            Name = "test",
            Type = PersistenceProviderType.SqlServer,
            ConnectionString = "   ",
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_ThrowOnZeroMaxPoolSize()
    {
        // Arrange
        var config = new ProviderConfiguration
        {
            Name = "test",
            Type = PersistenceProviderType.SqlServer,
            ConnectionString = "Server=localhost",
            MaxPoolSize = 0,
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => config.Validate())
            .Message.ShouldContain("MaxPoolSize");
    }

    [Fact]
    public void Validate_ThrowOnNegativeMaxPoolSize()
    {
        // Arrange
        var config = new ProviderConfiguration
        {
            Name = "test",
            Type = PersistenceProviderType.SqlServer,
            ConnectionString = "Server=localhost",
            MaxPoolSize = -1,
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_ThrowOnZeroConnectionTimeout()
    {
        // Arrange
        var config = new ProviderConfiguration
        {
            Name = "test",
            Type = PersistenceProviderType.SqlServer,
            ConnectionString = "Server=localhost",
            ConnectionTimeout = 0,
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => config.Validate())
            .Message.ShouldContain("ConnectionTimeout");
    }

    [Fact]
    public void Validate_ThrowOnZeroCommandTimeout()
    {
        // Arrange
        var config = new ProviderConfiguration
        {
            Name = "test",
            Type = PersistenceProviderType.SqlServer,
            ConnectionString = "Server=localhost",
            CommandTimeout = 0,
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => config.Validate())
            .Message.ShouldContain("CommandTimeout");
    }

    [Fact]
    public void Validate_SucceedWithValidConfig()
    {
        // Arrange
        var config = new ProviderConfiguration
        {
            Name = "test",
            Type = PersistenceProviderType.SqlServer,
            ConnectionString = "Server=localhost",
        };

        // Act & Assert - should not throw
        config.Validate();
    }

    [Fact]
    public void ProviderSpecificOptions_SupportKeyValuePairs()
    {
        // Arrange
        var config = new ProviderConfiguration
        {
            Name = "test",
            Type = PersistenceProviderType.SqlServer,
            ConnectionString = "Server=localhost",
        };

        // Act
        config.ProviderSpecificOptions["TrustServerCertificate"] = true;
        config.ProviderSpecificOptions["ApplicationName"] = "MyApp";

        // Assert
        config.ProviderSpecificOptions.Count.ShouldBe(2);
        config.ProviderSpecificOptions["TrustServerCertificate"].ShouldBe(true);
        config.ProviderSpecificOptions["ApplicationName"].ShouldBe("MyApp");
    }

    [Fact]
    public void ImplementsIPersistenceOptions()
    {
        // Arrange
        var config = new ProviderConfiguration
        {
            Name = "test",
            Type = PersistenceProviderType.SqlServer,
            ConnectionString = "Server=localhost",
        };

        // Assert
        config.ShouldBeAssignableTo<IPersistenceOptions>();
    }
}
