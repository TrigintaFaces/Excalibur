// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Firestore;

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
public sealed class FirestoreInboxStoreConformanceShould : InboxStoreConformanceTestBase, IClassFixture<FirestoreInboxStoreContainerFixture>
{
	private readonly FirestoreInboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Firestore container fixture.</param>
	public FirestoreInboxStoreConformanceShould(FirestoreInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

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
			NullLogger<FirestoreInboxStore>.Instance);

		return Task.FromResult<IInboxStore>(store);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupCollectionAsync().ConfigureAwait(false);
	}
}
