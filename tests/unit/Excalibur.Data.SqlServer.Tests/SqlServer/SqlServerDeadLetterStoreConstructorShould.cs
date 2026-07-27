using Excalibur.Data.SqlServer.ErrorHandling;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
public sealed class SqlServerDeadLetterStoreConstructorShould
{
    [Fact]
    public void ThrowOnNullOptions()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new SqlServerDeadLetterStore(
                null!,
                tenantContext: null,
                NullLogger<SqlServerDeadLetterStore>.Instance));
    }

    [Fact]
    public void ThrowOnNullLogger()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new SqlServerDeadLetterStore(
                Options.Create(new SqlServerDeadLetterOptions { ConnectionString = "Server=x" }),
                tenantContext: null,
                null!));
    }

    [Fact]
    public void ThrowOnEmptyConnectionString()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            new SqlServerDeadLetterStore(
                Options.Create(new SqlServerDeadLetterOptions { ConnectionString = "" }),
                tenantContext: null,
                NullLogger<SqlServerDeadLetterStore>.Instance));
    }

    [Fact]
    public void ThrowOnWhitespaceConnectionString()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            new SqlServerDeadLetterStore(
                Options.Create(new SqlServerDeadLetterOptions { ConnectionString = "   " }),
                tenantContext: null,
                NullLogger<SqlServerDeadLetterStore>.Instance));
    }

    [Fact]
    public void CreateSuccessfullyWithValidOptions()
    {
        // Arrange & Act
        var store = new SqlServerDeadLetterStore(
            Options.Create(new SqlServerDeadLetterOptions { ConnectionString = "Server=localhost;Database=Test" }),
            tenantContext: null,
            NullLogger<SqlServerDeadLetterStore>.Instance);

        // Assert
        store.ShouldNotBeNull();
    }
}
