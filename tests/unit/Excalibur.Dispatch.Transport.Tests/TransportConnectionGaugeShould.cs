// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests;

/// <summary>
/// The connection gauge must report what was observed about the broker, not that an adapter was asked to
/// start. Starting an adapter opens no connection, so it establishes nothing to report.
/// </summary>
/// <remarks>
/// The gauge enumerates the transports whose state is known, so a transport nobody has observed is absent
/// from it. Absence is the honest representation of "not yet known" for a boolean gauge that has no such
/// value.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class TransportConnectionGaugeShould : IDisposable
{
	private const string GaugeName = "dispatch.transport.connection_status";

	private readonly string _transport = "gauge-probe-" + Guid.NewGuid().ToString("N");
	private readonly MeterListener _listener = new();
	private readonly List<Measurement<int>> _observed = [];
	private bool _disposed;

	public TransportConnectionGaugeShould()
	{
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Name == GaugeName)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		// Capture every reading; the readers below select the transport they care about. Filtering here
		// would leave an assertion about another transport permanently unable to see its own measurement.
		_listener.SetMeasurementEventCallback<int>((_, measurement, tags, _) =>
		{
			lock (_observed)
			{
				_observed.Add(new Measurement<int>(measurement, tags.ToArray()));
			}
		});
		_listener.Start();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		TransportMeter.RemoveTransport(_transport);
		_listener.Dispose();
	}

	/// <summary>
	/// A transport nobody has observed reports nothing, rather than reporting connected.
	/// </summary>
	[Fact]
	public void ReportNothingForATransportWhoseConnectionWasNeverObserved()
	{
		_listener.RecordObservableInstruments();

		Readings().ShouldBeEmpty();
	}

	/// <summary>
	/// Liveness arm: the gauge is not simply inert. A state that IS known is reported.
	/// </summary>
	[Fact]
	public void ReportDisconnectedOnceThatIsKnown()
	{
		TransportMeter.UpdateTransportState(_transport, "kafka", isConnected: false);

		_listener.RecordObservableInstruments();

		Readings().ShouldContain(0);
	}

	/// <summary>
	/// Liveness arm: the gauge can still report connected, so the fix removed a false claim rather than the
	/// ability to make a true one.
	/// </summary>
	[Fact]
	public void ReportConnectedOnceThatIsKnown()
	{
		TransportMeter.UpdateTransportState(_transport, "kafka", isConnected: true);

		_listener.RecordObservableInstruments();

		Readings().ShouldContain(1);
	}

	private static string? TransportNameOf(Measurement<int> measurement)
	{
		foreach (var tag in measurement.Tags)
		{
			if (tag.Key == "transport_name")
			{
				return (string?)tag.Value;
			}
		}

		return null;
	}

	private List<Measurement<int>> AllReadings()
	{
		lock (_observed)
		{
			return [.. _observed];
		}
	}

	private List<int> Readings() =>
		[.. AllReadings().Where(m => TransportNameOf(m) == _transport).Select(static m => m.Value)];
}
