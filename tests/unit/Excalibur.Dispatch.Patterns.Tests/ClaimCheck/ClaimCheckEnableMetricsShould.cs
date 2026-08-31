// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// ClaimCheckOptions.EnableMetrics has to decide whether instruments are actually recorded. These
/// assert emission, not registration shape: a consumer who leaves the default on must see a store
/// counted, and a consumer who turns it off must see nothing recorded at all.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "ClaimCheck")]
public sealed class ClaimCheckEnableMetricsShould
{
	[Fact]
	public async Task RecordAStoreWhenMetricsAreEnabled() =>
		(await CountInstrumentsRecordedOnStoreAsync(enableMetrics: true)).ShouldBeGreaterThan(
			0,
			"EnableMetrics defaults to true, so a store has to reach the claim check instruments");

	[Fact]
	public async Task RecordNothingWhenMetricsAreDisabled() =>
		(await CountInstrumentsRecordedOnStoreAsync(enableMetrics: false)).ShouldBe(
			0,
			"setting EnableMetrics to false has to stop emission, not merely be stored");

	private static async Task<int> CountInstrumentsRecordedOnStoreAsync(bool enableMetrics)
	{
		var services = new ServiceCollection();
		_ = services.AddClaimCheck<RecordingClaimCheckProvider>(o => o.EnableMetrics = enableMetrics);

		using var provider = services.BuildServiceProvider();
		var claimCheck = provider.GetRequiredService<IClaimCheckProvider>();

		var recorded = 0;
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, l) =>
		{
			if (string.Equals(instrument.Meter.Name, ClaimCheckTelemetryConstants.MeterName, StringComparison.Ordinal))
			{
				l.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((_, _, _, _) => Interlocked.Increment(ref recorded));
		listener.SetMeasurementEventCallback<double>((_, _, _, _) => Interlocked.Increment(ref recorded));
		listener.Start();

		_ = await claimCheck.StoreAsync([1, 2, 3], CancellationToken.None);

		return recorded;
	}

	/// <summary>
	/// A minimal provider the container can activate. AddClaimCheck takes the provider as a type
	/// argument, so the stub has to be a real activatable type rather than a substitute instance.
	/// </summary>
	private sealed class RecordingClaimCheckProvider : IClaimCheckProvider
	{
		public Task<ClaimCheckReference> StoreAsync(byte[] payload, CancellationToken cancellationToken, ClaimCheckMetadata? metadata = null) =>
			Task.FromResult(new ClaimCheckReference { Id = "cc-1", Size = payload.Length });

		public Task<byte[]> RetrieveAsync(ClaimCheckReference reference, CancellationToken cancellationToken) =>
			Task.FromResult<byte[]>([]);

		public Task<bool> DeleteAsync(ClaimCheckReference reference, CancellationToken cancellationToken) =>
			Task.FromResult(true);

		public bool ShouldUseClaimCheck(byte[] payload) => true;
	}
}
