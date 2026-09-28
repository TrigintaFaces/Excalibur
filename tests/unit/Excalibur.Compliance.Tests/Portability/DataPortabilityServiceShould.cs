// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Portability;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Portability;

/// <summary>
/// The built-in data-portability service REFUSES; it does not report an export it did not make.
/// </summary>
/// <remarks>
/// <para>
/// <b>These arms replace eleven that certified a defect.</b> The previous suite asserted
/// <c>ExportStatus.Completed</c>, an expiry computed from the retention period, and — most plainly —
/// <c>Export_with_inventory_service_estimates_data_size</c>, which pinned
/// <c>inventory.Locations.Count * 1024L</c> as the byte count of exported data. Nothing was read,
/// serialised or written by any of it. Every one of those arms passed, and what they protected was a
/// GDPR Article 20 response that reported success having done nothing.
/// </para>
/// <para>
/// They are FLIPPED rather than deleted, because the behaviour they described is the behaviour that had
/// to change: what a caller gets now is a refusal that names the remedy.
/// </para>
/// <para>
/// <b>Why refusing is the right implementation and not a cop-out.</b> The framework cannot perform
/// Article 20 for a consumer: it does not know their data's shape, how to serialise it, or where the
/// export should go — <c>DataExportResult</c> carries no payload, path or URI, so there is nowhere for
/// one to land. Between reporting a completed export that does not exist and refusing loudly, only one
/// of those is honest to the regulator who eventually reads it.
/// </para>
/// </remarks>
public sealed class DataPortabilityServiceShould
{
	/// <summary>SAFETY: the export refuses instead of reporting a completed export.</summary>
	[Fact]
	public async Task Refuse_to_export_rather_than_report_an_export_it_did_not_make()
	{
		var sut = CreateService();

		var thrown = await Should.ThrowAsync<NotSupportedException>(
			async () => await sut.ExportAsync("user-1", ExportFormat.Json, CancellationToken.None));

		thrown.Message.ShouldContain(
			"NOT performed",
			customMessage: "the caller has to be able to tell that nothing happened. A Completed status "
			+ "with a plausible size is indistinguishable from a real export, and the audience for that "
			+ "answer is a regulator.");
	}

	/// <summary>The refusal names the remedy, so a consumer can act on it.</summary>
	/// <remarks>
	/// A refusal that does not say what to do instead is only marginally better than the lie. The
	/// registration is <c>TryAdd</c>, so the remedy is genuinely available to them.
	/// </remarks>
	[Fact]
	public async Task Name_the_remedy_in_the_refusal()
	{
		var sut = CreateService();

		var thrown = await Should.ThrowAsync<NotSupportedException>(
			async () => await sut.ExportAsync("user-1", ExportFormat.Json, CancellationToken.None));

		thrown.Message.ShouldContain("IDataPortabilityService");
		thrown.Message.ShouldContain("AddDataPortability");
	}

	/// <summary>The subject id is still validated before anything else.</summary>
	/// <remarks>
	/// Refusing is not an excuse to stop checking arguments: a caller passing nothing should be told
	/// that, not told about the missing exporter.
	/// </remarks>
	[Fact]
	public async Task Still_reject_a_missing_subject_id()
	{
		var sut = CreateService();

		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await sut.ExportAsync("  ", ExportFormat.Json, CancellationToken.None));
	}

	/// <summary>There are no exports, so there is no status to report.</summary>
	/// <remarks>
	/// The honest answer once the export refuses. This arm exists so that a future change which starts
	/// recording exports has to come back here and say what a status now means.
	/// </remarks>
	[Fact]
	public async Task Report_no_status_because_no_export_can_exist()
	{
		var sut = CreateService();

		var status = await sut.GetExportStatusAsync("any-id", CancellationToken.None);

		status.ShouldBeNull();
	}

	private static DataPortabilityService CreateService() =>
		new(
			Options.Create(new DataPortabilityOptions()),
			NullLogger<DataPortabilityService>.Instance,
			dataInventoryService: null);
}
