// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Caching.Diagnostics;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests verifying OTel metric emission from <see cref="InMemoryCacheTagTracker"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class InMemoryCacheTagTrackerOTelShould : IDisposable
{
	private readonly TestMeterFactory _meterFactory = new();
	private readonly MeterListener _listener = new();
	private long _registrationCount;
	private long _lookupCount;
	private long _unregistrationCount;

	public InMemoryCacheTagTrackerOTelShould()
	{
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == DispatchCachingTelemetryConstants.MeterName)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			switch (instrument.Name)
			{
				case "dispatch.cache.tag_tracker.registrations":
					Interlocked.Add(ref _registrationCount, measurement);
					break;
				case "dispatch.cache.tag_tracker.lookups":
					Interlocked.Add(ref _lookupCount, measurement);
					break;
				case "dispatch.cache.tag_tracker.unregistrations":
					Interlocked.Add(ref _unregistrationCount, measurement);
					break;
			}
		});

		_listener.Start();
	}

	public void Dispose()
	{
		_listener.Dispose();
		_meterFactory.Dispose();
	}

	[Fact]
	public async Task RecordRegistrationCounter_WhenKeyRegistered()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker(_meterFactory);

		// Act
		await tracker.RegisterKeyAsync("key1", ["tag1", "tag2"], CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert
		_registrationCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task RecordLookupCounter_WhenTagsQueried()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker(_meterFactory);
		await tracker.RegisterKeyAsync("key1", ["tag1"], CancellationToken.None);

		// Act
		_ = await tracker.GetKeysByTagsAsync(["tag1"], CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert
		_lookupCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task RecordUnregistrationCounter_WhenKeyUnregistered()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker(_meterFactory);
		await tracker.RegisterKeyAsync("key1", ["tag1"], CancellationToken.None);

		// Act
		await tracker.UnregisterKeyAsync("key1", CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert
		_unregistrationCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task RecordMultipleMetrics_DuringNormalWorkflow()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker(_meterFactory);

		// Act
		await tracker.RegisterKeyAsync("key1", ["tagA", "tagB"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key2", ["tagA"], CancellationToken.None);
		_ = await tracker.GetKeysByTagsAsync(["tagA"], CancellationToken.None);
		await tracker.UnregisterKeyAsync("key1", CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert
		_registrationCount.ShouldBeGreaterThanOrEqualTo(2);
		_lookupCount.ShouldBeGreaterThanOrEqualTo(1);
		_unregistrationCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task NotRecordRegistration_WhenTagsAreEmpty()
	{
		// Arrange
		var tracker = new InMemoryCacheTagTracker(_meterFactory);

		// Act
		await tracker.RegisterKeyAsync("key1", [], CancellationToken.None);
		var keys = await tracker.GetKeysByTagsAsync(["tag1"], CancellationToken.None);

		// Assert — empty tags should short-circuit and not register anything in the tracker
		keys.ShouldBeEmpty();
	}

	[Fact]
	public void ParameterlessConstructor_CreatesWorkingInstance()
	{
		// Arrange & Act — verify the no-arg constructor doesn't throw
		var tracker = new InMemoryCacheTagTracker();

		// Assert
		tracker.ShouldNotBeNull();
	}
}
