// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Retention conformance for the Postgres saga store. Author≠impl (TestsDeveloper), against a real Postgres
/// container.
/// </summary>
/// <remarks>
/// <para>
/// Postgres declares <c>completed_at TIMESTAMPTZ</c>, which stores an instant, so both arms of
/// <see cref="SagaStoreRetentionConformanceTestBase"/> pass here today and passed before the SQL Server column
/// was corrected. That is the point of running them here: <b>this deriver is the control.</b>
/// </para>
/// <para>
/// Without it, two arms failing on SQL Server would be evidence of a defect somewhere between the arms, the
/// fixture, the driver, and the column, and the arms themselves would be the least examined of the four. A
/// second provider that satisfies the same arms on the same store contract narrows the defect to the one thing
/// the two providers do not share. The providers disagreed; one of them had to be wrong; <c>timestamptz</c>
/// says which.
/// </para>
/// <para>
/// A control that only ever confirms is not a control. This one would fail if the arms were unsatisfiable, and
/// that is the outcome it exists to be able to report.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Postgres")]
[Collection("PostgresSagaStore")]
public sealed class PostgresSagaStoreRetentionConformanceShould : SagaStoreRetentionConformanceTestBase
{
	private readonly PostgresSagaStoreContainerFixture _fixture;

	public PostgresSagaStoreRetentionConformanceShould(PostgresSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISagaStore> CreateStoreAsync(ITenantContext ambientTenant)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — a control that is skipped is not a control.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var options = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Schema = _fixture.Schema,
			TableName = _fixture.TableName,
			CommandTimeoutSeconds = 30
		});

		return new PostgresSagaStore(
			options,
			NullLogger<PostgresSagaStore>.Instance,
			new DispatchJsonSerializer(),
			ambientTenant);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}
