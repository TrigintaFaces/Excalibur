// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using MongoDB.Driver;
namespace Excalibur.LeaderElection.MongoDB;
internal static class MongoDbLeaderElectionSchema
{
	internal static async Task EnsureTtlIndexAsync(IMongoCollection<MongoDbLeaderElectionDocument> collection, CancellationToken cancellationToken)
	{
		var indexKeysDefinition = Builders<MongoDbLeaderElectionDocument>.IndexKeys.Ascending(d => d.ExpiresAt);
		var indexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "ttl_expiresAt" };
		var indexModel = new CreateIndexModel<MongoDbLeaderElectionDocument>(indexKeysDefinition, indexOptions);

		try
		{
			await collection.Indexes
				.CreateOneAsync(indexModel, cancellationToken: cancellationToken)
				.ConfigureAwait(false);
		}
		catch (MongoCommandException)
		{
			// Index may already exist with different options - tolerate
		}
	}

}
