using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Security;

public class TransportConnectionBaseShould
{
    private sealed class SecureTestConnection : TransportConnectionBase
    {
        public SecureTestConnection(TransportSecurityOptions? options = null) : base(options) { }

        public bool EstablishConnectionCalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public bool ExposedTlsVerified => TlsVerified;
        public TransportSecurityOptions ExposedSecurityOptions => SecurityOptions;

        protected override Task EstablishConnectionAsync(CancellationToken cancellationToken)
        {
            EstablishConnectionCalled = true;
            return Task.CompletedTask;
        }

        protected override bool IsConnectionSecure() => true;

        protected override async ValueTask DisposeAsyncCore()
        {
            DisposeCalled = true;
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }
    }

    private sealed class InsecureTestConnection : TransportConnectionBase
    {
        public InsecureTestConnection(TransportSecurityOptions? options = null) : base(options) { }

        protected override Task EstablishConnectionAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override bool IsConnectionSecure() => false;
    }

    [Fact]
    public async Task ConnectAsync_Should_Call_EstablishConnection()
    {
        var conn = new SecureTestConnection(new TransportSecurityOptions { RequireTls = false });

        await conn.ConnectAsync(CancellationToken.None);

        conn.EstablishConnectionCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ConnectAsync_Should_Verify_Tls_When_Required()
    {
        var conn = new SecureTestConnection(new TransportSecurityOptions { RequireTls = true });

        await conn.ConnectAsync(CancellationToken.None);

        conn.ExposedTlsVerified.ShouldBeTrue();
    }

    [Fact]
    public async Task ConnectAsync_Should_Not_Verify_Tls_When_Not_Required()
    {
        var conn = new SecureTestConnection(new TransportSecurityOptions { RequireTls = false });

        await conn.ConnectAsync(CancellationToken.None);

        conn.ExposedTlsVerified.ShouldBeFalse();
    }

    [Fact]
    public async Task ConnectAsync_Should_Throw_When_Insecure_And_Tls_Required()
    {
        var conn = new InsecureTestConnection(new TransportSecurityOptions { RequireTls = true });

        var ex = await Should.ThrowAsync<TransportSecurityException>(
            () => conn.ConnectAsync(CancellationToken.None));

        ex.Message.ShouldContain("TLS-secured connection");
    }

    [Fact]
    public async Task ConnectAsync_Should_Not_Throw_When_Insecure_And_Tls_Not_Required()
    {
        var conn = new InsecureTestConnection(new TransportSecurityOptions { RequireTls = false });

        // Should not throw
        await conn.ConnectAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ConnectAsync_Should_Throw_ObjectDisposedException_After_Dispose()
    {
        var conn = new SecureTestConnection();
        await conn.DisposeAsync();

        await Should.ThrowAsync<ObjectDisposedException>(
            () => conn.ConnectAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DisposeAsync_Should_Call_DisposeAsyncCore()
    {
        var conn = new SecureTestConnection();

        await conn.DisposeAsync();

        conn.DisposeCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAsync_Should_Be_Idempotent()
    {
        var conn = new SecureTestConnection();

        await conn.DisposeAsync();
        await conn.DisposeAsync(); // Should not throw

        conn.DisposeCalled.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_SecurityOptions_When_None_Provided()
    {
        var conn = new SecureTestConnection();

        conn.ExposedSecurityOptions.ShouldNotBeNull();
        conn.ExposedSecurityOptions.RequireTls.ShouldBeTrue();
    }
}
