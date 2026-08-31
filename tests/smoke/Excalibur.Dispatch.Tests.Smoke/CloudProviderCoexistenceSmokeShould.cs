// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System;

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb;
using Excalibur.Data.DynamoDb;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Excalibur.Dispatch.Tests.Smoke;

/// <summary>
/// A host may target more than one cloud back end. The three cloud packages all register
/// <see cref="ICloudNativePersistenceProvider"/>, so an unkeyed <c>TryAdd</c> alone makes the second and
/// third registration a silent no-op: no exception, no log, and the wrong cloud's provider resolved.
/// These arms bind the real registration entry points and assert each provider stays distinctly
/// resolvable, matching the keying the relational and search providers already use.
/// </summary>
[Trait("Category", "Smoke")]
[Trait("Component", "Platform")]
public sealed class CloudProviderCoexistenceSmokeShould
{
	// The Cosmos SDK decodes AccountKey as Base64 inside CosmosClient's constructor, so a placeholder
	// that is not valid Base64 throws FormatException the moment the provider is resolved -- before this
	// smoke test can observe anything about coexistence. "c21va2U=" is Base64 for "smoke": still a
	// throwaway, but a decodable one.
	private const string CosmosConnectionString = "AccountEndpoint=https://localhost:8081;AccountKey=c21va2U=";

	[Fact]
	public void ResolveEachCloudProviderDistinctlyWhenAllThreeAreRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcaliburCosmosDb(cosmos => cosmos
			.ConnectionString(CosmosConnectionString)
			.DatabaseName("smoke"));
		_ = services.AddExcaliburDynamoDb(db => db.ServiceUrl("http://localhost:8000"));
		_ = services.AddExcaliburFirestore(fs => fs.ProjectId("smoke-project"));

		// Act
		using var provider = services.BuildServiceProvider();

		// Assert: two clouds registered into one container both resolve, to their own provider. Under the
		// unkeyed TryAdd this was the failing case -- the second registration was a silent no-op, so one of
		// these two keys did not exist at all.
		provider.GetRequiredKeyedService<ICloudNativePersistenceProvider>("cosmosdb")
			.ShouldBeOfType<CosmosDbPersistenceProvider>();
		provider.GetRequiredKeyedService<ICloudNativePersistenceProvider>("dynamodb")
			.ShouldBeOfType<DynamoDbPersistenceProvider>();

		// And the same keys resolve through the base contract the other eight providers use, which these
		// three previously did not expose at all.
		provider.GetRequiredKeyedService<IPersistenceProvider>("cosmosdb")
			.ShouldBeOfType<CosmosDbPersistenceProvider>();
		provider.GetRequiredKeyedService<IPersistenceProvider>("dynamodb")
			.ShouldBeOfType<DynamoDbPersistenceProvider>();

		// Firestore is asserted on the registration rather than resolved: constructing
		// FirestorePersistenceProvider opens a FirestoreDb, which requires Google Application Default
		// Credentials that no build agent has. The keyed descriptors are what the unkeyed TryAdd
		// destroyed, so their presence under a distinct key is the property this arm can honestly check.
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativePersistenceProvider)
			&& "firestore".Equals(sd.ServiceKey as string, StringComparison.Ordinal));
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPersistenceProvider)
			&& "firestore".Equals(sd.ServiceKey as string, StringComparison.Ordinal));
	}
}
