// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Security;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportConnectionBaseShould
{
    [Fact]
    public async Task Connect_Without_Tls_When_Not_Required()
    {
        var options = new TransportSecurityOptions { RequireTls = false };
        var sut = new TestConnection(options, isSecure: false);

        await sut.ConnectAsync(CancellationToken.None);

        sut.EstablishConnectionCalled.ShouldBeTrue();
        sut.GetTlsVerified().ShouldBeFalse();
    }

    [Fact]
    public async Task Connect_With_Tls_When_Required_And_Secure()
    {
        var options = new TransportSecurityOptions { RequireTls = true };
        var sut = new TestConnection(options, isSecure: true);

        await sut.ConnectAsync(CancellationToken.None);

        sut.EstablishConnectionCalled.ShouldBeTrue();
        sut.GetTlsVerified().ShouldBeTrue();
    }

    [Fact]
    public async Task Throw_TransportSecurityException_When_Tls_Required_But_Not_Secure()
    {
        var options = new TransportSecurityOptions { RequireTls = true };
        var sut = new TestConnection(options, isSecure: false);

        var ex = await Should.ThrowAsync<TransportSecurityException>(
            sut.ConnectAsync(CancellationToken.None));
        ex.Message.ShouldContain("TLS");
    }

    [Fact]
    public async Task Use_Default_Options_When_Null()
    {
        // Default TransportSecurityOptions has RequireTls = true,
        // so the connection must be secure for ConnectAsync to succeed.
        var sut = new TestConnection(securityOptions: null, isSecure: true);
        await sut.ConnectAsync(CancellationToken.None);

        sut.EstablishConnectionCalled.ShouldBeTrue();
        sut.GetTlsVerified().ShouldBeTrue();
    }

    [Fact]
    public async Task Throw_ObjectDisposedException_After_Dispose()
    {
        var sut = new TestConnection(null, isSecure: true);
        await sut.DisposeAsync();

        await Should.ThrowAsync<ObjectDisposedException>(
            sut.ConnectAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DisposeAsync_Is_Idempotent()
    {
        var sut = new TestConnection(null, isSecure: true);
        await sut.DisposeAsync();
        await sut.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task Call_DisposeAsyncCore()
    {
        var sut = new TestConnection(null, isSecure: true);
        await sut.DisposeAsync();
        sut.DisposeAsyncCoreCalled.ShouldBeTrue();
    }

    [Fact]
    public void Implement_IAsyncDisposable()
    {
        var sut = new TestConnection(null, isSecure: true);
        sut.ShouldBeAssignableTo<IAsyncDisposable>();
    }

    private sealed class TestConnection : TransportConnectionBase
    {
        private readonly bool _isSecure;
        public bool EstablishConnectionCalled { get; private set; }
        public bool DisposeAsyncCoreCalled { get; private set; }

        public TestConnection(TransportSecurityOptions? securityOptions, bool isSecure)
            : base(securityOptions)
        {
            _isSecure = isSecure;
        }

        public bool GetTlsVerified() => TlsVerified;

        protected override Task EstablishConnectionAsync(CancellationToken cancellationToken)
        {
            EstablishConnectionCalled = true;
            return Task.CompletedTask;
        }

        protected override bool IsConnectionSecure() => _isSecure;

        protected override async ValueTask DisposeAsyncCore()
        {
            DisposeAsyncCoreCalled = true;
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }
    }
}
