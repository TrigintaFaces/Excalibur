// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Security;

/// <summary>
/// Functional tests for <see cref="TransportConnectionBase"/> verifying
/// TLS verification, connection lifecycle, and disposal behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransportConnectionBaseFunctionalShould
{
	private sealed class SecureConnection : TransportConnectionBase
	{
		public bool EstablishCalled { get; private set; }
		public bool DisposeCalled { get; private set; }

		public SecureConnection(TransportSecurityOptions? options = null) : base(options) { }

		protected override Task EstablishConnectionAsync(CancellationToken cancellationToken)
		{
			EstablishCalled = true;
			return Task.CompletedTask;
		}

		protected override bool IsConnectionSecure() => true;

		protected override async ValueTask DisposeAsyncCore()
		{
			DisposeCalled = true;
			await base.DisposeAsyncCore().ConfigureAwait(false);
		}
	}

	private sealed class InsecureConnection : TransportConnectionBase
	{
		public InsecureConnection(TransportSecurityOptions? options = null) : base(options) { }

		protected override Task EstablishConnectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		protected override bool IsConnectionSecure() => false;
	}

	[Fact]
	public async Task Connect_successfully_when_tls_verified()
	{
		var options = new TransportSecurityOptions { RequireTls = true };
		await using var conn = new SecureConnection(options);

		await conn.ConnectAsync(CancellationToken.None);

		conn.EstablishCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task Throw_security_exception_when_tls_required_but_not_secure()
	{
		var options = new TransportSecurityOptions { RequireTls = true };
		await using var conn = new InsecureConnection(options);

		await Should.ThrowAsync<TransportSecurityException>(
			() => conn.ConnectAsync(CancellationToken.None));
	}

	[Fact]
	public async Task Connect_without_tls_check_when_not_required()
	{
		var options = new TransportSecurityOptions { RequireTls = false };
		await using var conn = new InsecureConnection(options);

		// Should not throw even though connection is not secure
		await conn.ConnectAsync(CancellationToken.None);
	}

	[Fact]
	public async Task Use_default_security_options_when_none_provided()
	{
		await using var conn = new SecureConnection();

		// Default options have RequireTls = false, so connect should succeed
		await conn.ConnectAsync(CancellationToken.None);

		conn.EstablishCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task Invoke_dispose_async_core()
	{
		var conn = new SecureConnection();

		await conn.DisposeAsync();

		conn.DisposeCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task Handle_dispose_idempotently()
	{
		var conn = new SecureConnection();

		await conn.DisposeAsync();
		await conn.DisposeAsync(); // Should not throw
	}

	[Fact]
	public async Task Throw_object_disposed_when_connecting_after_disposal()
	{
		var conn = new SecureConnection();
		await conn.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => conn.ConnectAsync(CancellationToken.None));
	}
}
