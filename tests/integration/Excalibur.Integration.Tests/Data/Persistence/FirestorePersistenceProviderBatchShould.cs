// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Data.Firestore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Binds the two writing arms of the Firestore batch to real Firestore, and the refusal that stands where
/// they used to fall through.
/// </summary>
/// <remarks>
/// <para>
/// A batch that cannot process an operation used to skip it and then report every operation as succeeded
/// with status 200, so a caller's create or replace vanished with no error anywhere and surfaced only later
/// as absent data. Three arms hold that shut: two liveness arms that require a create and a replace to
/// reach Firestore and be readable back through a fresh provider, and one safety arm that requires an
/// operation carrying no document to raise rather than be swallowed.
/// </para>
/// <para>
/// The liveness arms are what make the safety arm mean something: a provider that refused every batch would
/// satisfy the refusal on its own. Reading the document back through a second provider instance proves the
/// write crossed the wire rather than merely being staged in the batch object.
/// </para>
/// <para>
/// Never skipped -- the fixture fails fast when Docker is unavailable, so a missing emulator is a failure
/// and not a silent pass. The provider is built from options only, which is the constructor DI resolves.
/// </para>
/// </remarks>
[Collection(FirestorePersistenceProviderTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
[Trait("Pattern", "PROVIDER")]
public sealed class FirestorePersistenceProviderBatchShould
{
	private readonly FirestorePersistenceProviderContainerFixture _fixture;

	// One collection per test instance. xUnit builds a fresh instance per arm, so no arm observes what
	// another wrote.
	private readonly string _collection = $"batch_probe_{Guid.NewGuid():N}";

	private readonly IPartitionKey _partitionKey = new PartitionKey("batch-probe", "/id");

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestorePersistenceProviderBatchShould"/> class.
	/// </summary>
	/// <param name="fixture"> The Firestore emulator container fixture. </param>
	public FirestorePersistenceProviderBatchShould(FirestorePersistenceProviderContainerFixture fixture)
	{
		fixture.DockerAvailable.ShouldBeTrue(
			"The Firestore emulator must be available -- these arms exist to prove a batch reaches real "
			+ "Firestore, so they are never skipped.");

		_fixture = fixture;
	}

	/// <summary>
	/// A create operation in a batch must land in Firestore and be readable back.
	/// </summary>
	[Fact]
	[Trait("Type", "Integration")]
	public async Task Create_a_document_that_a_fresh_provider_can_read_back()
	{
		var provider = CreateProvider();
		var document = new BatchProbeDocument("probe-create", "created", 41);

		var result = await provider.ExecuteBatchAsync(
			_partitionKey,
			[new CloudBatchCreateOperation("probe-create", document)],
			TestContext.Current.CancellationToken);

		result.Success.ShouldBeTrue(result.ErrorMessage);

		var reloaded = await CreateProvider().GetByIdAsync<BatchProbeDocument>(
			"probe-create", _partitionKey, null, TestContext.Current.CancellationToken);

		reloaded.ShouldNotBeNull(
			"the batch reported success, so the created document must exist in Firestore -- a batch that "
			+ "reports 200 for an operation it never wrote is the silent data loss this arm binds.");
		reloaded.Name.ShouldBe("created");
		reloaded.Value.ShouldBe(41);
	}

	/// <summary>
	/// A replace operation in a batch must overwrite the stored document, not be skipped.
	/// </summary>
	[Fact]
	[Trait("Type", "Integration")]
	public async Task Replace_a_document_so_the_stored_copy_carries_the_new_content()
	{
		var provider = CreateProvider();

		var seeded = await provider.ExecuteBatchAsync(
			_partitionKey,
			[new CloudBatchCreateOperation("probe-replace", new BatchProbeDocument("probe-replace", "before", 1))],
			TestContext.Current.CancellationToken);
		seeded.Success.ShouldBeTrue(seeded.ErrorMessage);

		var result = await provider.ExecuteBatchAsync(
			_partitionKey,
			[new CloudBatchReplaceOperation("probe-replace", new BatchProbeDocument("probe-replace", "after", 2))],
			TestContext.Current.CancellationToken);

		result.Success.ShouldBeTrue(result.ErrorMessage);

		var reloaded = await CreateProvider().GetByIdAsync<BatchProbeDocument>(
			"probe-replace", _partitionKey, null, TestContext.Current.CancellationToken);

		reloaded.ShouldNotBeNull();
		reloaded.Name.ShouldBe(
			"after",
			"a replace the batch skipped leaves the seeded document standing while still reporting 200.");
		reloaded.Value.ShouldBe(2);
	}

	/// <summary>
	/// An operation declaring a write but carrying no document must raise, and write nothing.
	/// </summary>
	[Fact]
	[Trait("Type", "Integration")]
	public async Task Refuse_an_operation_that_declares_a_write_and_carries_no_document()
	{
		var provider = CreateProvider();
		var operation = new PayloadlessCreateOperation("probe-refused");

		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await provider.ExecuteBatchAsync(
				_partitionKey, [operation], TestContext.Current.CancellationToken));

		var reloaded = await CreateProvider().GetByIdAsync<BatchProbeDocument>(
			"probe-refused", _partitionKey, null, TestContext.Current.CancellationToken);

		reloaded.ShouldBeNull(
			"the refusal must happen before anything is committed, so a rejected batch leaves no partial "
			+ "write behind.");
	}

	/// <summary>
	/// The single-document write path shares this provider's serializer, so it is bound here too.
	/// </summary>
	/// <remarks>
	/// The persistence-provider conformance kit asserts provider metadata, capabilities and health -- it
	/// never writes a document -- so nothing else exercises this path against real Firestore. It shares
	/// <c>SerializeDocument</c> with the batch arms above, and that serializer used to hand the SDK values
	/// it has no converter for, which made every write of a document with a field throw.
	/// </remarks>
	[Fact]
	[Trait("Type", "Integration")]
	public async Task Round_trip_a_single_document_through_the_same_serializer_the_batch_uses()
	{
		var provider = CreateProvider();
		var document = new BatchProbeDocument("probe-single", "single", 7);

		var written = await provider.CreateAsync(
			document, _partitionKey, TestContext.Current.CancellationToken);

		written.Success.ShouldBeTrue(written.ErrorMessage);

		var reloaded = await CreateProvider().GetByIdAsync<BatchProbeDocument>(
			"probe-single", _partitionKey, null, TestContext.Current.CancellationToken);

		reloaded.ShouldNotBeNull();
		reloaded.Value.ShouldBe(7);
	}

	private FirestorePersistenceProvider CreateProvider() =>
		new(
			Options.Create(new FirestoreOptions
			{
				Name = "firestore-batch-probe",
				ProjectId = _fixture.ProjectId,
				EmulatorHost = _fixture.EmulatorEndpoint,
				DefaultCollection = _collection,
			}),
			NullLogger<FirestorePersistenceProvider>.Instance);

	private sealed record BatchProbeDocument(string Id, string Name, int Value);

	/// <summary>
	/// A caller-written batch operation that declares a create and supplies no document -- the shape the
	/// provider used to skip and report as succeeded.
	/// </summary>
	private sealed record PayloadlessCreateOperation(string DocumentId) : ICloudBatchOperation
	{
		public CloudBatchOperationType OperationType => CloudBatchOperationType.Create;
	}
}
