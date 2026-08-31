// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
using Excalibur.Data.DynamoDb.Snapshots;

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="DynamoDbSnapshotStore"/> using the Snapshot
/// Conformance Test Kit against a live DynamoDB-on-LocalStack container.
/// </summary>
/// <remarks>
/// These tests verify that the DynamoDB implementation correctly implements the
/// <see cref="ISnapshotStore"/> contract. They are never skipped: when the container is unavailable the
/// fixture fails fast, so a missing container surfaces as a failure rather than a silent pass. The store
/// is built with the client-injecting constructor so reads/writes hit the LocalStack endpoint through a
/// default-configured client.
/// </remarks>
[Collection(DynamoDbSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<DynamoDbSnapshotStoreContainerFixture>
{
	private readonly DynamoDbSnapshotStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The DynamoDB container fixture.</param>
	public DynamoDbSnapshotStoreConformanceShould(DynamoDbSnapshotStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// DynamoDB caps a single item at 400 KB, and that budget covers the keys, every attribute and the
	/// encoding overhead of the payload -- not the raw bytes alone. 200 KB is a large payload that leaves
	/// room for all of it, so the arm exercises the same behaviour it does elsewhere rather than sitting
	/// permanently red against a platform limit no implementation can lift.
	/// </remarks>
	protected override int MaxSnapshotPayloadBytes => 200_000;

	/// <inheritdoc/>
	protected override Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"DynamoDB (LocalStack) container must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new DynamoDbSnapshotStoreOptions
		{
			Connection = new DynamoDbConnectionOptions
			{
				ServiceUrl = _fixture.ServiceUrl,
				Region = "us-east-1",
				AccessKey = "test",
				SecretKey = "test",
			},
			TableName = _fixture.TableName,
			CreateTableIfNotExists = true,
			UseConsistentReads = true,
		});

		// Inject the fixture's default-configured client so reads/writes hit the LocalStack endpoint.
		// Ambient context, not the default null: the tenant-isolation arms establish tenants with
		// TenantContextHolder.BeginScope, and CurrentTenantScope collapses every tenant onto
		// the reserved untenanted sentinel, so they share one row per aggregate id and overwrite each
		// other. Same fix verified on SQL Server, Postgres, Redis and Mongo.
		var store = new DynamoDbSnapshotStore(
			_fixture.Client,
			options,
			NullLogger<DynamoDbSnapshotStore>.Instance,
			new AmbientTenantContext());

		return Task.FromResult<ISnapshotStore>(store);
	}

	/// <inheritdoc/>
	protected override Task DisposeSnapshotStoreAsync()
	{
		// Each test uses unique aggregate IDs; the fixture deletes the shared table on container dispose.
		return Task.CompletedTask;
	}

	/// <summary>
	/// Reads the tenant established by <see cref="TenantContextHolder.BeginScope"/>. The production
	/// equivalent is internal to Excalibur.Dispatch, so a directly-constructed store needs this here.
	/// </summary>
	private sealed class AmbientTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}
}
