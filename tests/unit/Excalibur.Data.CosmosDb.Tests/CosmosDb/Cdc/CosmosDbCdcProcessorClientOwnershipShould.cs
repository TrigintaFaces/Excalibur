// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Data.Tests.CosmosDb.Cdc;

/// <summary>
/// Holds the CDC processor to borrowing the <see cref="CosmosClient"/> it is constructed with, rather than
/// disposing a client it never owned.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms catch.</b> Both <see cref="CosmosDbCdcProcessor.Dispose"/> and
/// <see cref="CosmosDbCdcProcessor.DisposeAsync"/> called <c>Dispose</c> on the client handed to the
/// constructor. That client is resolved from the container, where it is a shared singleton the host also
/// hands to its inbox, outbox, snapshot and projection stores. Disposing the processor therefore tore down
/// the connection pool for every one of them, and the failure surfaced somewhere else entirely — as an
/// <see cref="ObjectDisposedException"/> from an unrelated store, at whatever time it next issued a
/// request — which is why nothing pointed at the processor.
/// </para>
/// <para>
/// <b>Ownership is not a flag here.</b> This type has exactly one constructor and it always receives the
/// client; there is no self-constructing path, so there is nothing an <c>_ownsClient</c> field could
/// distinguish. Sibling types that <em>can</em> build their own client carry that flag; this one is a pure
/// borrower, and the correct fix is the absence of the disposal, not a field with one reachable value.
/// </para>
/// <para>
/// <b>Both directions are asserted.</b> "Does not dispose the client" is satisfied just as well by a
/// <c>Dispose</c> that does nothing at all, and a processor that stopped releasing the state store or
/// stopped latching itself disposed would pass the safety arm alone while leaking and while answering
/// calls after disposal. The liveness arms below assert what disposal must still do, so gutting the method
/// cannot pass for fixing it.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CosmosDbCdcProcessorClientOwnershipShould : UnitTestBase
{
	// Never contacted: every arm here constructs or disposes, and none reads the change feed. The key is
	// only shaped like one so the SDK's connection-string parser accepts it.
	private const string ConnectionString =
		"AccountEndpoint=https://cdc-processor.documents.azure.com:443/;AccountKey=dGVzdA==;";

	private static CosmosDbCdcOptions ValidOptions() => new()
	{
		ConnectionString = ConnectionString,
		ProcessorName = "ownership-probe",
		DatabaseId = "cdc",
		ContainerId = "cdc-source",
	};

	private static CosmosDbCdcProcessor CreateProcessor(
		CosmosClient client,
		ICosmosDbCdcStateStore stateStore) =>
		new(
			client,
			stateStore,
			MsOptions.Create(ValidOptions()),
			NullLogger<CosmosDbCdcProcessor>.Instance);

	[Fact]
	public void LeaveTheInjectedClientUsableAfterTheProcessorIsDisposed()
	{
		// SAFETY. The client is a shared singleton the host also hands to its other stores, and the
		// processor is disposed first in exactly the hosts most likely to share one.
		using var injected = new CosmosClient(ConnectionString);
		var processor = CreateProcessor(injected, A.Fake<ICosmosDbCdcStateStore>());

		processor.Dispose();

		_ = Should.NotThrow(
			() => injected.GetDatabase("cdc"),
			"the processor disposed a client it did not own. The host still holds this client and its "
			+ "other stores still read and write through it.");
	}

	[Fact]
	public async Task LeaveTheInjectedClientUsableAfterTheProcessorIsDisposedAsynchronously()
	{
		// SAFETY, async path. DisposeAsync carried the same call, and a host using `await using` would hit
		// this one and not the arm above.
		using var injected = new CosmosClient(ConnectionString);
		var processor = CreateProcessor(injected, A.Fake<ICosmosDbCdcStateStore>());

		await processor.DisposeAsync();

		_ = Should.NotThrow(
			() => injected.GetDatabase("cdc"),
			"the asynchronous disposal path disposed a borrowed client. Fixing only the synchronous path "
			+ "leaves the defect reachable through `await using`, which is how a hosted service disposes.");
	}

	[Fact]
	public void StillReleaseTheStateStoreItWasGivenToRelease()
	{
		// LIVENESS. Not disposing the borrowed client must not become disposing nothing: the state store
		// is the resource this processor is responsible for releasing, and a Dispose emptied to satisfy the
		// safety arm would leak it.
		using var injected = new CosmosClient(ConnectionString);
		var stateStore = A.Fake<ICosmosDbCdcStateStore>();
		var processor = CreateProcessor(injected, stateStore);

		processor.Dispose();

		A.CallTo(() => stateStore.Dispose()).MustHaveHappened();
	}

	[Fact]
	public async Task StillLatchItselfDisposedSoLaterCallsAreRefused()
	{
		// LIVENESS. The other thing disposal must still do: a processor that answered calls after being
		// disposed would satisfy "did not dispose the client" while reading a change feed the host believes
		// it has stopped.
		using var injected = new CosmosClient(ConnectionString);
		var processor = CreateProcessor(injected, A.Fake<ICosmosDbCdcStateStore>());

		processor.Dispose();

		_ = await Should.ThrowAsync<ObjectDisposedException>(
			() => processor.GetCurrentPositionAsync(CancellationToken.None),
			"disposal must still take effect. An empty Dispose passes the ownership arms and leaves a "
			+ "processor that keeps working after the host has torn it down.");
	}
}
