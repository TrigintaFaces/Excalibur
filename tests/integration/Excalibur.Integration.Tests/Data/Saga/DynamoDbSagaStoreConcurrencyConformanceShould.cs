// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Saga;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Optimistic-concurrency conformance for the DynamoDB saga store (e1tsq2 / skl8r7, S853) — one of the
/// five distributed providers. Author≠impl (TestsDeveloper); runs the shared
/// <see cref="SagaStoreConformanceTestBase"/> contract with <see cref="SupportsOptimisticConcurrency"/>
/// enabled against DynamoDB-on-LocalStack, so the version-gated <c>no-overwrite</c> and
/// <c>no-resurrect</c> facts are enforced.
/// </summary>
/// <remarks>
/// RED on the pre-fix blind <c>PutItem</c> (no <c>ConditionExpression</c>); GREEN on skl8r7's conditional
/// PutItem CAS (<c>attribute_not_exists(#pk)</c> for a new saga / <c>#v = :expectedVersion</c> for an
/// update; <c>ConditionalCheckFailedException</c> → <see cref="ConcurrencyException"/>). A fresh table per
/// test (the options ctor auto-creates it) gives isolation on the shared LocalStack container.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "DynamoDB")]
public sealed class DynamoDbSagaStoreConcurrencyConformanceShould : SagaStoreConformanceTestBase, IClassFixture<DynamoDbSagaStoreContainerFixture>
{
	private readonly DynamoDbSagaStoreContainerFixture _fixture;
	private readonly string _tableName = $"sagas_{Guid.NewGuid():N}";

	public DynamoDbSagaStoreConcurrencyConformanceShould(DynamoDbSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override bool SupportsOptimisticConcurrency => true;

	// Purge IS supported: DynamoDbSagaStore implements PurgeCompletedBeforeAsync rather than falling
	// through to the interface default. This was declared unsupported back when it did fall through, and
	// the declaration outlived the implementation -- so the suite asserted the store throws
	// NotSupportedException while it was quietly purging correctly. The real retention arms were not
	// running here at all; a purge that ignored the threshold or deleted a running saga would have gone
	// unnoticed on this provider.
	//
	// It stayed hidden because the arms were failing anyway for an unrelated reason (auto-create was
	// broken, so every operation hit a missing table), which masked the wrong-contract assertion behind
	// the wrong-exception one.

	/// <inheritdoc/>
	protected override Task<ISagaStore> CreateStoreAsync()
	{
		var options = Options.Create(new DynamoDbSagaOptions
		{
			Connection = new DynamoDbConnectionOptions
			{
				ServiceUrl = _fixture.ServiceUrl,
				Region = "us-east-1",
				AccessKey = "test",
				SecretKey = "test",
			},
			TableName = _tableName,
			CreateTableIfNotExists = true,
			UseConsistentReads = true,
		});

		// The options ctor auto-creates the (per-test, unique) table — the injected-client ctor would skip it.
		return Task.FromResult<ISagaStore>(
			new DynamoDbSagaStore(options, NullLogger<DynamoDbSagaStore>.Instance, new DispatchJsonSerializer(), SingleTenantTestContext.Instance));
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => Task.CompletedTask; // throwaway per-test table; container disposed at end
}
