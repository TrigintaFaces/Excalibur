using Excalibur.Data.SqlServer.ErrorHandling;

using Excalibur.Dispatch;
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
                tenantContext: TestTenantContext.SingleTenant,
                NullLogger<SqlServerDeadLetterStore>.Instance));
    }

    [Fact]
    public void ThrowOnNullLogger()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new SqlServerDeadLetterStore(
                Options.Create(new SqlServerDeadLetterOptions { ConnectionString = "Server=x" }),
                tenantContext: TestTenantContext.SingleTenant,
                null!));
    }

    [Fact]
    public void ThrowOnEmptyConnectionString()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            new SqlServerDeadLetterStore(
                Options.Create(new SqlServerDeadLetterOptions { ConnectionString = "" }),
                tenantContext: TestTenantContext.SingleTenant,
                NullLogger<SqlServerDeadLetterStore>.Instance));
    }

    [Fact]
    public void ThrowOnWhitespaceConnectionString()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            new SqlServerDeadLetterStore(
                Options.Create(new SqlServerDeadLetterOptions { ConnectionString = "   " }),
                tenantContext: TestTenantContext.SingleTenant,
                NullLogger<SqlServerDeadLetterStore>.Instance));
    }

    [Fact]
    public void CreateSuccessfullyWithValidOptions()
    {
        // Arrange & Act
        var store = new SqlServerDeadLetterStore(
            Options.Create(new SqlServerDeadLetterOptions { ConnectionString = "Server=localhost;Database=Test" }),
            tenantContext: TestTenantContext.SingleTenant,
            NullLogger<SqlServerDeadLetterStore>.Instance);

        // Assert
        store.ShouldNotBeNull();
    }
}
