// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;
using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Testing.Conformance;

using global::Oracle.ManagedDataAccess.Client;

namespace Excalibur.Integration.Tests.EventSourcing.Oracle;

/// <summary>
/// A duplicate-key violation raised by a constraint that is NOT this row's key must surface, not be
/// reported as one of the positioned contract's declared refusals.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this arm exists.</b> Oracle has no atomic match-or-insert, so the store reads the engine's
/// duplicate-key violation as "a row exists" -- which is the fact zero-rows-affected carries on an
/// atomic engine. That reading is only true if a row actually does exist. A consumer who adds their own
/// unique index over a business key raises the SAME violation while this row is absent, and classifying
/// that as <c>Vanished</c> would hand back a declared outcome for an undeclared fault and discard the
/// engine's own diagnosis.
/// </para>
/// <para>
/// <b>It also pins the decision to the row rather than to a constraint NAME.</b> The shipped primary key
/// is named so a violation message is legible, but the separation here is made by looking for the row.
/// A name-matching implementation would be defeated by exactly this index, and by every table a consumer
/// created before the key was named.
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Database", "Oracle")]
[Trait("Pattern", "STORE")]
public sealed class OracleNonKeyConstraintViolationShould(OracleEventStoreContainerFixture fixture)
	: IClassFixture<OracleEventStoreContainerFixture>, IAsyncLifetime
{
	private const string TenantId = "tenant-foreign-constraint";

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Usage",
		"CA2213:Disposable fields should be disposed",
		Justification = "xUnit owns and disposes the class fixture.")]
	private readonly OracleEventStoreContainerFixture _fixture = fixture;

	private readonly string _tableName = "FGNCON_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

	/// <inheritdoc />
	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue("this arm is about a real engine's constraint behaviour");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	[Fact]
	public async Task SurfaceAViolationRaisedByAConstraintThatIsNotTheRowKey()
	{
		var store = new OracleProjectionStore<ConformanceProjection>(
			_fixture.ConnectionString,
			tenantContext: new FixedTenantContext(TenantId),
			tableName: _tableName,
			jsonOptions: null,
			schemaName: _fixture.Schema);

		var identity = ((IProjectionDestinationIdentity)store).Identity;
		var qualified = identity["oracle:".Length..];
		var physical = qualified[(qualified.LastIndexOf('.') + 1)..].Trim('"');

		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		_ = await connection.ExecuteAsync(
			OracleProjectionSchema.CreateTransactionalTableScript(physical, _fixture.Schema)).ConfigureAwait(false);

		// A consumer's own unique index, over a column that is NOT part of the primary key. Nothing in
		// the framework creates this; the point is that a consumer may, and the shipped store must not
		// mistake its violation for a lost insert race on the row's key.
		_ = await connection.ExecuteAsync(
			$"CREATE UNIQUE INDEX {qualified[..qualified.LastIndexOf('.')]}.\"UX_{physical}\" "
			+ $"ON {qualified} (LastAppliedPosition)").ConfigureAwait(false);

		var positioned = store.GetService(typeof(IPositionedProjectionStore<ConformanceProjection>))
			as IPositionedProjectionStore<ConformanceProjection>;
		positioned.ShouldNotBeNull();

		// One row occupies position 7 under its OWN key.
		var first = await positioned.UpsertAtPositionAsync(
			"row-one", new ConformanceProjection { Id = "row-one", Total = 1 }, null, 7, CancellationToken.None)
			.ConfigureAwait(false);
		first.Outcome.ShouldBe(ProjectionAdvanceOutcome.Applied);

		// A DIFFERENT key claiming absence, writing the same position. The row for this key does not
		// exist, so the insert is correct and the violation comes from the consumer's index.
		var error = await Should.ThrowAsync<OracleException>(async () =>
			await positioned.UpsertAtPositionAsync(
					"row-two", new ConformanceProjection { Id = "row-two", Total = 1 }, null, 7, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		error.Number.ShouldBe(1);
		error.Message.ShouldContain("UX_" + physical);

		// SAFETY: nothing was written for the refused key, and the engine's refusal left no partial row.
		(await store.GetByIdAsync("row-two", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();

		// LIVENESS: the store still works for a position the index permits, so the arm above is not
		// satisfied by a store that has stopped writing.
		var third = await positioned.UpsertAtPositionAsync(
			"row-two", new ConformanceProjection { Id = "row-two", Total = 1 }, null, 8, CancellationToken.None)
			.ConfigureAwait(false);
		third.Outcome.ShouldBe(ProjectionAdvanceOutcome.Applied);
	}

	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}
