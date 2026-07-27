// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
///     Verifies the observability counter emitted by <see cref="InMemoryDeduplicator" /> when a duplicate
///     is suppressed (the <c>dispatch.exactlyonce.duplicates.suppressed</c> instrument).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class InMemoryDeduplicatorMetricsShould
{
	private const string MeterName = "Excalibur.Dispatch.ExactlyOnce";
	private const string InstrumentName = "dispatch.exactlyonce.duplicates.suppressed";

	private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(5);

	[Fact]
	public async Task EmitDuplicatesSuppressedCounter_WhenADuplicateIsSuppressed()
	{
		long recorded = 0;

		using var listener = new MeterListener
		{
			InstrumentPublished = (instrument, l) =>
			{
				if (instrument.Meter.Name == MeterName && instrument.Name == InstrumentName)
				{
					l.EnableMeasurementEvents(instrument);
				}
			},
		};
		listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
		{
			if (instrument.Name == InstrumentName)
			{
				_ = Interlocked.Add(ref recorded, measurement);
			}
		});
		listener.Start();

		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions
		{
			EnableAutomaticCleanup = false,
			CleanupInterval = TimeSpan.FromHours(1),
		});
		using var sut = new InMemoryDeduplicator(options, NullLogger<InMemoryDeduplicator>.Instance);

		const string messageId = "duplicate-message-1";

		// First sighting is not a duplicate — no suppression, no counter.
		(await sut.IsDuplicateAsync(messageId, Expiry, CancellationToken.None)).ShouldBeFalse();
		await sut.MarkProcessedAsync(messageId, Expiry, CancellationToken.None);
		recorded.ShouldBe(0, "no duplicate has been suppressed yet");

		// Second sighting of the same id (unexpired) is a suppressed duplicate — the counter must increment.
		(await sut.IsDuplicateAsync(messageId, Expiry, CancellationToken.None)).ShouldBeTrue();

		recorded.ShouldBeGreaterThanOrEqualTo(
			1,
			"a suppressed duplicate must emit the dispatch.exactlyonce.duplicates.suppressed counter");
	}
}
