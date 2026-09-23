// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc;
using Excalibur.Cdc.InMemory;

namespace Excalibur.Tests.Cdc.InMemory;

/// <summary>
/// Locks that processed-change history is reachable through the store interface a consumer holds.
/// </summary>
/// <remarks>
/// <para>
/// Every variable in these arms is typed as the shipped <see cref="IInMemoryCdcStore"/> interface, never
/// the concrete store, and that is deliberate. The history extension decides what to return by testing
/// whether the store also implements the history interface. The pre-existing history tests all hold the
/// CONCRETE store, so the compiler binds its instance method and the extension is never called - they
/// passed throughout the period in which every consumer holding the interface received an empty list.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCdcStoreHistoryReachableShould : UnitTestBase
{
	/// <summary>
	/// SAFETY: with history preservation on, the changes a processor marked as processed are visible
	/// through the interface.
	/// </summary>
	[Fact]
	public async Task ExposeProcessedChangesThroughTheStoreInterface()
	{
		IInMemoryCdcStore store = new InMemoryCdcStore(
			Options.Create(new InMemoryCdcOptions { PreserveHistory = true }));
		store.AddChange(Change(1));
		store.AddChange(Change(2));

		using var processor = new InMemoryCdcProcessor(
			store,
			Options.Create(new InMemoryCdcOptions { PreserveHistory = true }),
			A.Fake<ILogger<InMemoryCdcProcessor>>());

		_ = await processor.ProcessBatchAsync((_, _) => Task.CompletedTask, CancellationToken.None)
			.ConfigureAwait(false);

		store.GetHistory().Count.ShouldBe(
			2,
			"history was preserved, so a consumer holding the store interface must be able to read it");
	}

	/// <summary>
	/// LIVENESS: with history preservation OFF, the interface still reports an empty history.
	/// </summary>
	/// <remarks>
	/// Without this arm the one above is satisfied by an extension that returns every change ever
	/// processed regardless of the option, which would make the option inert in the other direction.
	/// </remarks>
	[Fact]
	public async Task ReportNoHistoryThroughTheStoreInterfaceWhenPreservationIsOff()
	{
		IInMemoryCdcStore store = new InMemoryCdcStore(
			Options.Create(new InMemoryCdcOptions { PreserveHistory = false }));
		store.AddChange(Change(1));

		using var processor = new InMemoryCdcProcessor(
			store,
			Options.Create(new InMemoryCdcOptions()),
			A.Fake<ILogger<InMemoryCdcProcessor>>());

		_ = await processor.ProcessBatchAsync((_, _) => Task.CompletedTask, CancellationToken.None)
			.ConfigureAwait(false);

		store.GetHistory().ShouldBeEmpty();
	}

	private static InMemoryCdcChange Change(int id) =>
		InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange { ColumnName = "Id", NewValue = id });
}
