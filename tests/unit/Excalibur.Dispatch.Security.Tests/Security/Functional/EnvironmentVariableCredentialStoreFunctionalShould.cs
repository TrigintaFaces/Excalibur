// Functional tests for EnvironmentVariableCredentialStore — env var lookup, prefix handling, key conversion

using System.Security;

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Functional;

[Trait("Category", "Unit")]
public sealed class EnvironmentVariableCredentialStoreFunctionalShould : IDisposable
{
    private readonly List<string> _envVarsToCleanup = [];

    public void Dispose()
    {
        foreach (var key in _envVarsToCleanup)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    private void SetEnvVar(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _envVarsToCleanup.Add(name);
    }

    [Fact]
    public async Task RetrieveCredentialFromPrefixedEnvVar()
    {
        SetEnvVar("DISPATCH_MY_SECRET", "super-secret-value");

        var store = new EnvironmentVariableCredentialStore(
            NullLogger<EnvironmentVariableCredentialStore>.Instance,
            "DISPATCH_");

        var result = await store.GetCredentialAsync("my.secret", CancellationToken.None);

        result.ShouldNotBeNull();
        ConvertSecureString(result).ShouldBe("super-secret-value");
    }

    [Fact]
    public async Task RetrieveCredentialFromNonPrefixedFallback()
    {
        SetEnvVar("API_KEY", "fallback-key");

        var store = new EnvironmentVariableCredentialStore(
            NullLogger<EnvironmentVariableCredentialStore>.Instance,
            "APP_"); // Prefix that won't match

        var result = await store.GetCredentialAsync("api.key", CancellationToken.None);

        result.ShouldNotBeNull();
        ConvertSecureString(result).ShouldBe("fallback-key");
    }

    [Fact]
    public async Task ReturnNullWhenCredentialNotFound()
    {
        var store = new EnvironmentVariableCredentialStore(
            NullLogger<EnvironmentVariableCredentialStore>.Instance,
            "DISPATCH_");

        var result = await store.GetCredentialAsync("nonexistent.key", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ConvertDotsAndColonsToUnderscoresInEnvVarName()
    {
        SetEnvVar("DISPATCH_DB_CONNECTION_STRING", "Server=localhost;");

        var store = new EnvironmentVariableCredentialStore(
            NullLogger<EnvironmentVariableCredentialStore>.Instance,
            "DISPATCH_");

        var result = await store.GetCredentialAsync("db:connection.string", CancellationToken.None);

        result.ShouldNotBeNull();
        ConvertSecureString(result).ShouldBe("Server=localhost;");
    }

    [Fact]
    public async Task UppercaseKeyForLookup()
    {
        SetEnvVar("DISPATCH_LOWER_CASE_KEY", "value");

        var store = new EnvironmentVariableCredentialStore(
            NullLogger<EnvironmentVariableCredentialStore>.Instance,
            "DISPATCH_");

        var result = await store.GetCredentialAsync("lower.case.key", CancellationToken.None);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReturnReadOnlySecureString()
    {
        SetEnvVar("DISPATCH_TEST_KEY", "read-only-value");

        var store = new EnvironmentVariableCredentialStore(
            NullLogger<EnvironmentVariableCredentialStore>.Instance,
            "DISPATCH_");

        var result = await store.GetCredentialAsync("test.key", CancellationToken.None);

        result.ShouldNotBeNull();
        result.IsReadOnly().ShouldBeTrue();
    }

    [Fact]
    public async Task ThrowOnNullOrWhitespaceKey()
    {
        var store = new EnvironmentVariableCredentialStore(
            NullLogger<EnvironmentVariableCredentialStore>.Instance,
            "DISPATCH_");

        await Should.ThrowAsync<ArgumentException>(async () =>
            await store.GetCredentialAsync(null!, CancellationToken.None));

        await Should.ThrowAsync<ArgumentException>(async () =>
            await store.GetCredentialAsync("", CancellationToken.None));

        await Should.ThrowAsync<ArgumentException>(async () =>
            await store.GetCredentialAsync("   ", CancellationToken.None));
    }

    [Fact]
    public void ThrowOnNullLogger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new EnvironmentVariableCredentialStore(null!, "PREFIX_"));
    }

    [Fact]
    public async Task HandleEmptyPrefix()
    {
        SetEnvVar("MY_KEY", "no-prefix-value");

        var store = new EnvironmentVariableCredentialStore(
            NullLogger<EnvironmentVariableCredentialStore>.Instance,
            "");

        var result = await store.GetCredentialAsync("my.key", CancellationToken.None);

        result.ShouldNotBeNull();
        ConvertSecureString(result).ShouldBe("no-prefix-value");
    }

    private static string ConvertSecureString(SecureString secure)
    {
        var ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(secure);
        try
        {
            return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
        }
    }
}
