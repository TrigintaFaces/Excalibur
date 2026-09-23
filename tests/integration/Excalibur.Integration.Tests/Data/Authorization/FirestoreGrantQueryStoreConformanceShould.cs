// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.Data.Firestore.Authorization;
using Excalibur.Dispatch;
using Excalibur.Integration.Tests.Data.Inbox;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Grants;

namespace Excalibur.Integration.Tests.Data.Authorization;

/// <summary>
/// Holds <see cref="FirestoreGrantStore"/> to the shared <see cref="IGrantQueryStore"/> contract on a real
/// Firestore emulator.
/// </summary>
/// <remarks>
/// The store borrows the fixture's emulator-connected <c>FirestoreDb</c> and writes to a per-instance
/// collection, whose documents are deleted on the way out.
/// </remarks>
[Collection(FirestoreInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
[Trait("Pattern", "STORE")]
public sealed class FirestoreGrantQueryStoreConformanceShould : GrantQueryStoreConformanceTestBase
{
	private readonly FirestoreInboxStoreContainerFixture _fixture;
	private readonly string _collectionName = $"grants_conf_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreGrantQueryStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Firestore emulator fixture, shared by the collection.</param>
	public FirestoreGrantQueryStoreConformanceShould(FirestoreInboxStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync().ConfigureAwait(false);
		if (_fixture.DockerAvailable)
		{
			await foreach (var document in _fixture.Db.Collection(_collectionName).ListDocumentsAsync().ConfigureAwait(false))
			{
				_ = await document.DeleteAsync().ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	protected override Task<IGrantStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator must be available for real-infrastructure conformance - never skipped.");

		return Task.FromResult<IGrantStore>(new FirestoreGrantStore(
			_fixture.Db,
			Options.Create(new FirestoreAuthorizationOptions
			{
				ProjectId = _fixture.ProjectId,
				GrantsCollectionName = _collectionName,
			}),
			NullLogger<FirestoreGrantStore>.Instance));
	}
}
