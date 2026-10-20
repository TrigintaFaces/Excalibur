// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using Excalibur.Dispatch;
using Excalibur.Inbox.Firestore;

using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Inbox;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Conformance tests for <see cref="FirestoreInboxStore"/> using the Inbox Conformance Test Kit
/// against a real Firestore emulator.
/// </summary>
/// <remarks>
/// These tests verify that the Firestore implementation correctly implements the IInboxStore contract
/// against real infrastructure (the Firestore emulator via TestContainers), exercising the emulator-
/// connected <see cref="Google.Cloud.Firestore.FirestoreDb"/> built with the SDK's default serializer
/// settings.
/// </remarks>
[Collection(FirestoreInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
public sealed class FirestoreInboxStoreConformanceShould : InboxStoreConformanceTestBase
{
	private readonly FirestoreInboxStoreContainerFixture _fixture;
	private SeverableProxy? _proxy;
	private GrpcChannel? _faultChannel;
	private string? _canaryId;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Firestore container fixture.</param>
	public FirestoreInboxStoreConformanceShould(FirestoreInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>The same context <see cref="CreateStoreAsync"/> hands the store.</remarks>
	protected override ITenantContext StoreTenantContext => SingleTenantTestContext.Instance;

	/// <inheritdoc/>
	protected override Task<IInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator must be available for real-infrastructure conformance — never skipped.");

		var options = Options.Create(new FirestoreInboxOptions
		{
			ProjectId = _fixture.ProjectId,
			CollectionName = _fixture.CollectionName,
		});

		var store = new FirestoreInboxStore(
			_fixture.Db,
			options,
			NullLogger<FirestoreInboxStore>.Instance,
			SingleTenantTestContext.Instance);

		return Task.FromResult<IInboxStore>(store);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupCollectionAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public override async Task ThrowNotNoOpOnPersistenceFailure()
	{
		try
		{
			await base.ThrowNotNoOpOnPersistenceFailure().ConfigureAwait(false);
		}
		finally
		{
			try
			{
				_faultChannel?.Dispose();
			}
			finally
			{
				if (_proxy is not null)
				{
					await _proxy.DisposeAsync().ConfigureAwait(false);
				}
			}
		}
	}

	/// <inheritdoc/>
	protected override async Task InjectPersistenceFaultAsync()
	{
		var endpoint = _fixture.EmulatorEndpoint;
		var upstream = new Uri(endpoint.Contains("://", StringComparison.Ordinal) ? endpoint : $"http://{endpoint}");
		_proxy = new SeverableProxy(upstream);
		_faultChannel = GrpcChannel.ForAddress(_proxy.Address);
		var settings = FirestoreSettings.GetDefault();
		settings.CommitSettings = CallSettings.FromExpiration(Expiration.FromTimeout(TimeSpan.FromSeconds(5)))
			.WithRetry(RetrySettings.FromConstantBackoff(1, TimeSpan.Zero, static _ => false));
		// Streaming RPCs reject a Retry value in CallSettings, even a single-attempt policy.
		settings.BatchGetDocumentsSettings = CallSettings.FromExpiration(Expiration.FromTimeout(TimeSpan.FromSeconds(5)));
		var client = new FirestoreClientBuilder
		{
			CallInvoker = _faultChannel.CreateCallInvoker(),
			Settings = settings,
		}.Build();
		Store = new FirestoreInboxStore(
			FirestoreDb.Create(_fixture.ProjectId, client),
			Options.Create(new FirestoreInboxOptions { ProjectId = _fixture.ProjectId, CollectionName = _fixture.CollectionName }),
			NullLogger<FirestoreInboxStore>.Instance,
			SingleTenantTestContext.Instance);
		_canaryId = $"canary-{Guid.NewGuid():N}";
		_ = await Store.CreateEntryAsync(_canaryId, "Canary", "Canary", [1],
			new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None).ConfigureAwait(false);
		(await Store.GetEntryAsync(_canaryId, "Canary", CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
		_proxy.Sever();
	}

	/// <inheritdoc/>
	protected override async Task RemovePersistenceFaultAsync()
	{
		_proxy?.Restore();
		var verification = await CreateVerificationStoreAsync().ConfigureAwait(false);
		try
		{
			(await verification.GetEntryAsync(_canaryId!, "Canary", CancellationToken.None).ConfigureAwait(false))
				.ShouldNotBeNull("severing the route must preserve existing emulator data");
		}
		finally
		{
			await ((IAsyncDisposable)verification).DisposeAsync().ConfigureAwait(false);
		}
	}

	private sealed class SeverableProxy : IAsyncDisposable
	{
		private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
		private readonly CancellationTokenSource _lifetime = new();
		private readonly ConcurrentBag<Task> _connections = [];
		private readonly HashSet<TcpClient> _clients = [];
		private readonly object _gate = new();
		private readonly Uri _upstream;
		private readonly Task _acceptLoop;
		private bool _severed;

		public SeverableProxy(Uri upstream)
		{
			_upstream = upstream;
			_listener.Start();
			Address = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
			_acceptLoop = AcceptAsync();
		}

		public string Address { get; }

		public void Sever()
		{
			lock (_gate)
			{
				_severed = true;
				foreach (var client in _clients) client.Dispose();
			}
		}

		public void Restore()
		{
			lock (_gate) _severed = false;
		}

		public async ValueTask DisposeAsync()
		{
			try
			{
				await _lifetime.CancelAsync().ConfigureAwait(false);
				_listener.Stop();
				Sever();
				try
				{
					await _acceptLoop.ConfigureAwait(false);
				}
				finally
				{
					await Task.WhenAll(_connections).ConfigureAwait(false);
				}
			}
			finally
			{
				try { _listener.Dispose(); }
				finally { _lifetime.Dispose(); }
			}
		}

		private async Task AcceptAsync()
		{
			try
			{
				while (!_lifetime.IsCancellationRequested)
				{
					var client = await _listener.AcceptTcpClientAsync(_lifetime.Token).ConfigureAwait(false);
					lock (_gate)
					{
						if (_severed) client.Dispose();
						else
						{
							_clients.Add(client);
							_connections.Add(ForwardAsync(client));
						}
					}
				}
			}
			catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
			catch (SocketException) when (_lifetime.IsCancellationRequested) { }
			catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested) { }
		}

		private async Task ForwardAsync(TcpClient client)
		{
			using (client)
			using (var upstream = new TcpClient())
			using (var connection = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
			{
				try
				{
					await upstream.ConnectAsync(_upstream.Host, _upstream.Port, connection.Token).ConfigureAwait(false);
					var incoming = client.GetStream();
					var outgoing = upstream.GetStream();
					var send = incoming.CopyToAsync(outgoing, connection.Token);
					var receive = outgoing.CopyToAsync(incoming, connection.Token);
					await Task.WhenAny(send, receive).ConfigureAwait(false);
					await connection.CancelAsync().ConfigureAwait(false);
					await Task.WhenAll(send, receive).ConfigureAwait(false);
				}
				catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
				finally
				{
					lock (_gate) _clients.Remove(client);
				}
			}
		}
	}
}
