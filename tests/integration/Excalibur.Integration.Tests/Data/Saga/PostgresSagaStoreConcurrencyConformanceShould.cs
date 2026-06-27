// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Saga;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Optimistic-concurrency conformance for the Postgres saga store (e1tsq2 / skl8r7, S853) — the 4th of
/// the five distributed providers. Author≠impl (TestsDeveloper); runs the shared
/// <see cref="SagaStoreConformanceTestBase"/> contract with <see cref="SupportsOptimisticConcurrency"/>
/// enabled, so the version-gated <c>no-overwrite</c> (<c>StaleSave_ThrowsConcurrencyException_NoLostUpdate</c>)
/// and <c>no-resurrect</c> (<c>StaleSave_OnMissingSaga_Throws_DoesNotResurrect</c>) facts are enforced
/// against a real Postgres container.
/// </summary>
/// <remarks>
/// RED on the pre-fix unchecked <c>INSERT ON CONFLICT</c> last-writer-wins upsert; GREEN on skl8r7's
/// version-gated <c>WHERE version = @ExpectedVersion</c> + no-resurrect insert guard. Integration-class
/// (TestContainers, serial), shares the <c>PostgresSagaStore</c> container collection.
/// </remarks>
[Collection("PostgresSagaStore")]
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Postgres")]
public sealed class PostgresSagaStoreConcurrencyConformanceShould : SagaStoreConformanceTestBase
{
	private readonly PostgresSagaStoreContainerFixture _fixture;

	public PostgresSagaStoreConcurrencyConformanceShould(PostgresSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override bool SupportsOptimisticConcurrency => true;

	/// <inheritdoc/>
	protected override async Task<ISagaStore> CreateStoreAsync()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		// Fresh table per test-class instance (xUnit constructs one per [Fact]) → per-test isolation.
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var options = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Schema = _fixture.Schema,
			TableName = _fixture.TableName,
			CommandTimeoutSeconds = 30,
		});

		return new PostgresSagaStore(options, NullLogger<PostgresSagaStore>.Instance, new DispatchJsonSerializer());
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}
