// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Firestore;

using Google.Api.Gax;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared.Fixtures;

using Testcontainers.Firestore;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Firestore;

/// <summary>
/// Firestore-emulator container fixture for the CDC state-store optimistic-concurrency lock. Extends
/// <see cref="ContainerFixtureBase"/> so a missing emulator surfaces as a hard failure (never a silent
/// skip). Builds an emulator-connected <see cref="FirestoreDb"/> with the SDK's default serializer.
/// </summary>
#pragma warning disable CA1812 // Instantiated by the xUnit test runner as a class fixture.
internal sealed class FirestoreCdcStateStoreContainerFixture : ContainerFixtureBase
{
	private FirestoreContainer? _container;

	/// <summary>
	/// Gets the emulator-connected Firestore client (injected into the state store under test).
	/// </summary>
	public FirestoreDb Db { get; private set; } = null!;

	/// <summary>
	/// Gets the project id used for the emulator.
	/// </summary>
	public string ProjectId { get; } = "test-project";

	/// <summary>
	/// Gets the unique collection name isolating this fixture's CDC position documents.
	/// </summary>
	public string CollectionName { get; } = $"cdc_state_{Guid.NewGuid():N}";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new FirestoreBuilder()
			.WithImage("gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators")
			.WithName($"firestore-cdc-state-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// Explicit endpoint + insecure credentials with the SDK's default serializer settings —
		// env-var-based emulator discovery is unreliable.
		// EmulatorOnly makes the SDK speak EMULATOR semantics; an explicit Endpoint alone leaves it
		// behaving as though this were a real deployment, so the emulator rejects admin-ish calls with
		// PermissionDenied "Metadata operations require admin authentication." EmulatorOnly and an
		// explicit Endpoint/ChannelCredentials are mutually exclusive -- the SDK builds its own channel
		// from FIRESTORE_EMULATOR_HOST and throws from GaxPreconditions.CheckState if given both.
		// The variable is set from THIS fixture's container, so it cannot point at a foreign emulator.
		Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", _container.GetEmulatorEndpoint());

		Db = await new FirestoreDbBuilder
		{
			ProjectId = ProjectId,
			EmulatorDetection = EmulatorDetection.EmulatorOnly,
		}.BuildAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}
}
#pragma warning restore CA1812

/// <summary>
/// Real-Firestore-emulator regression lock for the optimistic-concurrency (check-and-set) guard added to
/// <see cref="FirestoreCdcStateStore.SavePositionAsync(string, FirestoreCdcPosition, CancellationToken)"/>.
/// The store now wraps the write in a native <c>RunTransactionAsync</c> that refuses to regress the CDC
/// watermark: a save whose <see cref="FirestoreCdcPosition.UpdateTime"/> is older than the stored
/// watermark throws <see cref="FirestoreStalePositionException"/> instead of blindly overwriting.
/// </summary>
/// <remarks>
/// <para>
/// Non-vacuity: this lock is RED against the pre-fix blind <c>SetAsync(data, SetOptions.Overwrite)</c>
/// implementation — with no transaction/read-back, the older save would silently succeed and clobber the
/// newer stored watermark, so no exception would be thrown (step 2 fails) and the read-back would show the
/// older watermark (step 3 fails).
/// </para>
/// <para>
/// Docker is a hard requirement (never skipped): a missing emulator fails the fixture rather than passing
/// silently, per the real-infrastructure lock bar.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "CDC")]
[Trait("Database", "Firestore")]
[Trait("SubComponent", "StateStoreOptimisticConcurrency")]
#pragma warning disable CA1812 // Instantiated by the xUnit test runner.
internal sealed class FirestoreCdcStateStoreOptimisticConcurrencyIntegrationShould
	: IClassFixture<FirestoreCdcStateStoreContainerFixture>
{
	private const string CollectionPath = "users/orders";

	private readonly FirestoreCdcStateStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the
	/// <see cref="FirestoreCdcStateStoreOptimisticConcurrencyIntegrationShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Firestore-emulator container fixture.</param>
	public FirestoreCdcStateStoreOptimisticConcurrencyIntegrationShould(
		FirestoreCdcStateStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task RejectOlderWatermarkAndPreserveNewerStoredPosition()
	{
		// Arrange — Docker/Firestore emulator is a hard requirement; this real-infra lock must never skip.
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Firestore emulator must be available - the CDC state-store optimistic-concurrency guard "
			+ "is a real-infra lock and must never be skipped.");

		await using var store = new FirestoreCdcStateStore(
			_fixture.Db,
			_fixture.CollectionName,
			NullLogger<FirestoreCdcStateStore>.Instance);

		var processorName = $"proc-{Guid.NewGuid():N}";
		var now = DateTimeOffset.UtcNow;
		var newerPosition = FirestoreCdcPosition.FromUpdateTime(CollectionPath, now, "docNew");
		var olderPosition = FirestoreCdcPosition.FromUpdateTime(CollectionPath, now.AddMinutes(-5), "docOld");

		// Act 1 — establish the newer watermark as the stored position.
		await store.SavePositionAsync(processorName, newerPosition, TestContext.Current.CancellationToken);

		// Act 2 + Assert — saving an OLDER watermark for the same processor must be rejected by the
		// check-and-set guard (RED on the pre-fix blind overwrite, which would have succeeded silently).
		_ = await Should.ThrowAsync<FirestoreStalePositionException>(
			() => store.SavePositionAsync(processorName, olderPosition, TestContext.Current.CancellationToken));

		// Assert — the rejected older save did NOT clobber the stored newer watermark.
		var stored = await store.GetPositionAsync(processorName, TestContext.Current.CancellationToken);
		_ = stored.ShouldNotBeNull();
		stored.UpdateTime.ShouldNotBeNull();
		stored.UpdateTime!.Value.ShouldBe(now);
		stored.LastDocumentId.ShouldBe("docNew");
	}

	[Fact]
	public async Task AllowNewerWatermarkOverOlderStoredPosition()
	{
		// Arrange — the guard must reject only regressions, not all writes.
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Firestore emulator must be available - the CDC state-store optimistic-concurrency guard "
			+ "is a real-infra lock and must never be skipped.");

		await using var store = new FirestoreCdcStateStore(
			_fixture.Db,
			_fixture.CollectionName,
			NullLogger<FirestoreCdcStateStore>.Instance);

		var processorName = $"proc-{Guid.NewGuid():N}";
		var baseline = DateTimeOffset.UtcNow;
		var olderPosition = FirestoreCdcPosition.FromUpdateTime(CollectionPath, baseline, "docBase");
		var newerPosition = FirestoreCdcPosition.FromUpdateTime(CollectionPath, baseline.AddMinutes(5), "docNext");

		// Act — store an older watermark, then advance it to a newer one.
		await store.SavePositionAsync(processorName, olderPosition, TestContext.Current.CancellationToken);
		await store.SavePositionAsync(processorName, newerPosition, TestContext.Current.CancellationToken);

		// Assert — the newer (non-regressing) save succeeded and advanced the stored watermark.
		var stored = await store.GetPositionAsync(processorName, TestContext.Current.CancellationToken);
		_ = stored.ShouldNotBeNull();
		stored.UpdateTime.ShouldNotBeNull();
		stored.UpdateTime!.Value.ShouldBe(baseline.AddMinutes(5));
		stored.LastDocumentId.ShouldBe("docNext");
	}
}
#pragma warning restore CA1812
