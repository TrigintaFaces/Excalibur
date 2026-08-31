// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Drives <see cref="OutboxBackoffFloorClampShould"/> against a live MongoDB container.
/// </summary>
/// <remarks>
/// <para>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. MongoDB advertises the backoff capability, so the processor prefers
/// the path these arms drive.
/// </para>
/// <para>
/// The floor this provider composes is measured from its injected <see cref="TimeProvider"/> rather than
/// from a server-side clock. That is deliberate and is not a weaker choice here: the claim predicate, the
/// lease and the plain failure path on this store all read the same clock, so the composition adds no clock
/// the provider was not already trusting. Cross-dispatcher skew remains, exactly as it does for every other
/// timestamp this store writes.
/// </para>
/// </remarks>
[Collection(MongoDbOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDB")]
public sealed class MongoDbOutboxBackoffFloorClampShould : OutboxBackoffFloorClampShould, IClassFixture<MongoDbOutboxStoreContainerFixture>
{
	private readonly MongoDbOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="MongoDbOutboxBackoffFloorClampShould"/> class.</summary>
	/// <param name="fixture">The MongoDB container fixture.</param>
	public MongoDbOutboxBackoffFloorClampShould(MongoDbOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IOutboxStore> CreateStoreAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - the backoff floor lock is never skipped.");

		var options = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			FailureBackoffFloorSeconds = floorSeconds,
		});

		return Task.FromResult<IOutboxStore>(
			new MongoDbOutboxStore(options, NullLogger<MongoDbOutboxStore>.Instance));
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupAsync();
}
