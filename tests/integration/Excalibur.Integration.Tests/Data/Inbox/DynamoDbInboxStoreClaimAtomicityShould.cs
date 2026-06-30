// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
using Excalibur.Dispatch;
using Excalibur.Inbox.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure atomicity engage-test for <see cref="DynamoDbInboxStore"/>'s
/// <see cref="IClaimableInboxStore.TryClaimAsync"/> claim-before-execute primitive against a live LocalStack DynamoDB.
/// </summary>
/// <remarks>
/// N callers race the SAME (messageId, handlerType); exactly one claim must win (first-writer-wins via the conditional
/// put), the rest see <see langword="false"/>. Determinism comes from the provider's atomic conditional write, not
/// timing — so the <c>== 1</c> assertion is non-vacuous. Never skipped: a missing container fails fast.
/// </remarks>
[Collection(DynamoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "DynamoDb")]
[Trait("Component", "Inbox")]
public sealed class DynamoDbInboxStoreClaimAtomicityShould : IClassFixture<DynamoDbInboxStoreContainerFixture>
{
	private const int Concurrency = 16;
	private readonly DynamoDbInboxStoreContainerFixture _fixture;

	public DynamoDbInboxStoreClaimAtomicityShould(DynamoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_exactly_one_claim_when_concurrent_callers_race_the_same_message()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB container must be available - real-infra atomicity is never skipped: "
			+ $"{_fixture.InitializationError}");

		var tableName = $"{_fixture.TableName}_{Guid.NewGuid():N}";
		var options = Options.Create(new DynamoDbInboxOptions
		{
			TableName = tableName,
			CreateTableIfNotExists = true,
			DefaultTtlSeconds = 0,
			Connection = new DynamoDbConnectionOptions { ServiceUrl = _fixture.ServiceUrl },
		});
		var store = new DynamoDbInboxStore(_fixture.Client, options, NullLogger<DynamoDbInboxStore>.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		try
		{
			const string messageId = "msg-claim-atomicity";
			const string handlerType = "TestHandler";

			var tasks = Enumerable.Range(0, Concurrency)
				.Select(_ => Task.Run(() => store.TryClaimAsync(messageId, handlerType, CancellationToken.None).AsTask()))
				.ToArray();

			var results = await Task.WhenAll(tasks).ConfigureAwait(false);

			results.Count(claimed => claimed).ShouldBe(
				1,
				$"the atomic claim must admit exactly one of {Concurrency} concurrent callers; got [{string.Join(",", results)}]");

			(await store.TryClaimAsync(messageId, handlerType, CancellationToken.None)).ShouldBeFalse(
				"a claim already held must be denied to a later caller");

			await store.ReleaseAsync(messageId, handlerType, CancellationToken.None);
			(await store.TryClaimAsync(messageId, handlerType, CancellationToken.None)).ShouldBeTrue(
				"after release the message must be re-admitted on the real provider");
		}
		finally
		{
			await _fixture.DeleteTableAsync(tableName, CancellationToken.None).ConfigureAwait(false);
		}
	}
}
