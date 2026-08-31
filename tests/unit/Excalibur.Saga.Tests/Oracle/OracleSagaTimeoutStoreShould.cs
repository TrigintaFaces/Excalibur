// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Oracle;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.Oracle;

/// <summary>
/// Tenant-partition lock for <see cref="OracleSagaTimeoutStore"/> (bead o6sw98). Mirrors
/// <c>SqlServerSagaTimeoutStoreShould</c>'s equivalent lock: the same fold
/// (<c>KeyedTenantPartition.FromContext</c>) is shared by all three timeout stores.
/// </summary>
/// <remarks>
/// The partition seam (<c>CurrentPartition</c>) fires before the store opens any Oracle connection, so
/// this fail-closed behaviour is reachable without a live database. RED against the pre-fix code, which
/// fed an ambient <c>TenantContextHolder.Current</c> read into <c>KeyedTenantPartition.FromStoredValue</c>
/// — a fold that maps a missing tenant onto the untenanted sentinel rather than refusing, so a host with
/// no established tenant silently scheduled the timeout into a partition nothing else addresses.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class OracleSagaTimeoutStoreShould
{
	private readonly ILogger<OracleSagaTimeoutStore> _logger = A.Fake<ILogger<OracleSagaTimeoutStore>>();

	[Fact]
	public async Task ScheduleTimeoutAsync_WhenTheContextResolvesNoTenant_RefusesRatherThanStampingSilently()
	{
		var store = new OracleSagaTimeoutStore(
			connectionString: "Data Source=localhost:1521/XEPDB1;User Id=test;Password=test;",
			_logger,
			new TestTenantContext(tenantId: null));

		_ = await Should.ThrowAsync<TenantRequiredException>(async () =>
			await store.ScheduleTimeoutAsync(
				new SagaTimeout(
					TimeoutId: "timeout-none",
					SagaId: "saga-none",
					SagaType: "TestSaga",
					TimeoutType: "TestTimeout",
					TimeoutData: null,
					DueAt: DateTime.UtcNow.AddMinutes(-1),
					ScheduledAt: DateTime.UtcNow),
				CancellationToken.None));
	}

	[Fact]
	public void Constructor_WithNullTenantContext_ThrowsArgumentNullException()
	{
		_ = Should.Throw<ArgumentNullException>(() => new OracleSagaTimeoutStore(
			connectionString: "Data Source=localhost:1521/XEPDB1;User Id=test;Password=test;",
			_logger,
			tenantContext: null!));
	}
}
