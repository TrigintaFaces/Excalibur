// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CosmosDb;
using Excalibur.Dispatch;
using Excalibur.Inbox.CosmosDb;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Inbox;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="CosmosDbInboxStore"/> against the shared
/// <see cref="InboxStoreConformanceTestBase"/>, running on the live Cosmos DB emulator.
/// </summary>
/// <remarks>
/// <para>
/// Cosmos was, until this suite existed, the one <see cref="IInboxStore"/> implementor asserted ONLY by
/// its own composition-based kit suite. An arm added to the shared base therefore reached every other
/// provider and silently missed this one — which is how a provider drifts with nothing going red. This
/// suite binds Cosmos to the same base every other provider inherits, so a new base arm lands here too.
/// The sibling kit suite stays: every provider carries both, and parity is the point.
/// </para>
/// <para>
/// The store is built through its options-only constructor, so it constructs its own
/// <c>CosmosClient</c> — the surface a consumer actually uses. Gateway mode and a certificate-accepting
/// handler are both required because the emulator presents a self-signed certificate.
/// </para>
/// <para>
/// Isolation is a per-instance container. xUnit constructs a fresh instance per arm, so each arm starts
/// against an empty container and drops it on the way out. The container NAME is an instance field and
/// is deliberately NOT randomised per <see cref="CreateStoreAsync"/> call: the base builds a second,
/// independent store for its fresh-instance read-backs, and that store has to point at the SAME physical
/// container or the read-back could never observe what the first instance wrote — vacuous rather than
/// red or green on the property it exists to check.
/// </para>
/// </remarks>
[Collection(CosmosDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "CosmosDb")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Pattern", "STORE")]
public sealed class CosmosDbInboxStoreConformanceShould : InboxStoreConformanceTestBase
{
	private readonly CosmosDbEventStoreContainerFixture _fixture;
	private readonly string _containerName = $"inbox_conf_{Guid.NewGuid():N}";

	// One severed-route handler per test-class instance, shared by every store this instance builds
	// (including the base's verification stores) so the fault applies to whichever one is writing.
	//
	// It wraps the FIXTURE's transport rather than a hand-built HttpClientHandler. That is load-bearing,
	// not tidiness: the emulator advertises its own address on the container-internal port, and the
	// fixture's handler is what re-aims each request at the mapped host port. A hand-built handler skips
	// that rewrite, so EVERY request is refused — the suite then fails in setup, which looks exactly like
	// a fault that fired too early.
	private readonly SeveredRouteHandler _route;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Cosmos DB emulator fixture, shared by the collection.</param>
	public CosmosDbInboxStoreConformanceShould(CosmosDbEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
		_route = new SeveredRouteHandler { InnerHandler = fixture.EmulatorHttpMessageHandler };
	}

	/// <inheritdoc/>
	/// <remarks>The same context <see cref="CreateStoreAsync"/> hands the store.</remarks>
	protected override ITenantContext StoreTenantContext => SingleTenantTestContext.Instance;

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Cosmos DB emulator must be available - real-infra conformance is never skipped. "
			+ _fixture.InitializationError);

		var store = new CosmosDbInboxStore(
			Options.Create(BuildOptions()),
			NullLogger<CosmosDbInboxStore>.Instance,
			SingleTenantTestContext.Instance);

		// Provision eagerly rather than on first use. The durability arm's faulted call would otherwise be
		// the call that creates the container, so the read-back that follows would be inspecting a
		// container that never existed when the write was attempted rather than the one it was aimed at.
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		return store;
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync() =>
		await _fixture.DeleteContainerAsync(_containerName).ConfigureAwait(false);

	// 2mek4x / 6tfdx3: a real fault, never a mocked CosmosClient. The bead listed three candidate
	// Cosmos-side rejections; all three were measured against this emulator image
	// (@sha256:a8b93e25…, Cosmos SDK 3.58.0) and all three are unusable here:
	//
	//   * A container-level JS trigger. Creatable, but Cosmos only runs a trigger a request explicitly
	//     names via ItemRequestOptions.PreTriggers, and the store names none. It would never fire —
	//     a fault that cannot reach the write is an arm that passes without running.
	//   * An RU/throughput cap producing a hard 429. ReadThroughputAsync on a container of this
	//     emulator returns 404 "Throughput is not configured", so there is no provisioned throughput to
	//     cap; and a floor of 400 RU/s would not reject one ~10 RU insert deterministically anyway.
	//   * A 403 from a read-only permission. The emulator ACCEPTS CreateUserAsync and
	//     CreatePermissionAsync(PermissionMode.Read) and then HONOURS A WRITE made with the resulting
	//     resource token. The permission is inert, so this "fault" injects nothing — it would have made
	//     the arm green by never faulting, which is the exact failure the arm exists to catch.
	//
	// The remaining Cosmos-side rejection, a 400 from a partition-key mismatch, is real (measured:
	// BadRequest/1001) but only obtainable from a container whose partition-key path differs, and a
	// container's path is immutable. Reaching one means pointing the store at a DIFFERENT container,
	// which moves the write away from the container the base's read-back inspects — the same defect
	// DynamoDB's arm was rewritten to remove, where the read-back queries somewhere the faulted write
	// could never have reached.
	//
	// So the fault moves to the ROUTING layer, exactly as DynamoDB's did and for the same reason.
	// Weaker than a provider-side rejection and deliberately so: a fault that erases or relocates the
	// evidence cannot prove the property. Nothing here fabricates a Cosmos RESPONSE — the request never
	// leaves the process, which is what a severed route is; the real SDK then surfaces the transport
	// failure through its own pipeline. The container and every document in it survive untouched, so the
	// fresh-instance read-back inspects precisely where the write was aimed.

	/// <inheritdoc/>
	protected override Task InjectPersistenceFaultAsync()
	{
		_route.Severed = true;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	protected override Task RemovePersistenceFaultAsync()
	{
		// Idempotent by construction — the base calls this from a finally, so it must be safe even when
		// the fault was never applied. Nothing about the backing store was altered, so there is nothing
		// to repair beyond the route itself.
		_route.Severed = false;
		return Task.CompletedTask;
	}

	private CosmosDbInboxOptions BuildOptions() => new()
	{
		DatabaseName = _fixture.DatabaseName,
		ContainerName = _containerName,
		CreateContainerIfNotExists = true,

		// TTL auto-reap off, so it can never race the explicit-cleanup arms.
		DefaultTimeToLiveSeconds = 0,
		Client = new CosmosDbClientOptions
		{
			ConnectionString = _fixture.ConnectionString,
			UseDirectMode = false,
			HttpClientFactory = () => new HttpClient(_route, disposeHandler: false),
			Resilience = new CosmosDbClientResilienceOptions
			{
				// Bound how long a severed route can be retried, so the durability arm fails fast
				// instead of spending the default budget on a port that will keep refusing.
				MaxRetryAttempts = 0,
				MaxRetryWaitTimeInSeconds = 1,
				RequestTimeoutInSeconds = 20,
			},
		},
	};

	/// <summary>
	/// Severs this store's route to Cosmos on demand: while severed, no request reaches the transport
	/// beneath it. When not severed it is a pass-through over the emulator's real handler.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A <see cref="DelegatingHandler"/> is the framework's own interception seam, so an un-severed
	/// request still travels the real SDK pipeline — serialization, headers, authorization and all — and
	/// reaches the real emulator.
	/// </para>
	/// <para>
	/// The severed path fails the send rather than re-aiming it at a dead port, and that choice is
	/// deliberate: the handler underneath rewrites each request's host and port to the container's mapped
	/// address, so a re-aimed request would simply be rewritten back and the fault would silently not
	/// happen. Failing the send is order-independent and cannot be undone by a handler further in.
	/// </para>
	/// </remarks>
	private sealed class SeveredRouteHandler : DelegatingHandler
	{
		private bool _severed;

		/// <summary>
		/// Gets or sets a value indicating whether the route is currently severed. Volatile because the
		/// SDK issues requests from pool threads while the arm flips this from the test thread.
		/// </summary>
		public bool Severed
		{
			get => Volatile.Read(ref _severed);
			set => Volatile.Write(ref _severed, value);
		}

		/// <inheritdoc/>
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken) =>
			Severed
				? throw new HttpRequestException(
					"2mek4x durability fault: the route to Cosmos is severed for this request.")
				: base.SendAsync(request, cancellationToken);
	}
}
