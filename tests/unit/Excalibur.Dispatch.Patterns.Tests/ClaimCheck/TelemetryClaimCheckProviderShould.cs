// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Patterns.ClaimCheck;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="TelemetryClaimCheckProvider"/> OTel decorator.
/// Sprint 569 — Task S569.21: Tests for S569.6 (TelemetryClaimCheckProvider + health check).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ClaimCheck")]
public sealed class TelemetryClaimCheckProviderShould : IDisposable
{
	private readonly IClaimCheckProvider _innerProvider;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;
	private readonly TelemetryClaimCheckProvider _sut;

	public TelemetryClaimCheckProviderShould()
	{
		_innerProvider = A.Fake<IClaimCheckProvider>();
		_meter = new Meter("test-meter");
		_activitySource = new ActivitySource("test-source");
		_sut = new TelemetryClaimCheckProvider(_innerProvider, _meter, _activitySource);
	}

	public void Dispose()
	{
		_meter.Dispose();
		_activitySource.Dispose();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenInnerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryClaimCheckProvider(null!, _meter, _activitySource));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenMeterIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryClaimCheckProvider(_innerProvider, null!, _activitySource));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenActivitySourceIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryClaimCheckProvider(_innerProvider, _meter, null!));
	}

	[Fact]
	public async Task StoreAsync_DelegatesToInner()
	{
		// Arrange
		var payload = new byte[] { 1, 2, 3 };
		var expectedRef = new ClaimCheckReference { Id = "ref-1" };

		A.CallTo(() => _innerProvider.StoreAsync(payload, A<CancellationToken>._, A<ClaimCheckMetadata>._))
			.Returns(Task.FromResult(expectedRef));

		// Act
		var result = await _sut.StoreAsync(payload, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedRef);
		A.CallTo(() => _innerProvider.StoreAsync(payload, A<CancellationToken>._, A<ClaimCheckMetadata>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StoreAsync_PropagatesExceptionFromInner()
	{
		// Arrange
		var payload = new byte[] { 1, 2 };
		A.CallTo(() => _innerProvider.StoreAsync(payload, A<CancellationToken>._, A<ClaimCheckMetadata>._))
			.Throws(new InvalidOperationException("Storage failure"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.StoreAsync(payload, CancellationToken.None));
	}

	[Fact]
	public async Task RetrieveAsync_DelegatesToInner()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "ref-2" };
		var expectedPayload = new byte[] { 10, 20, 30 };

		A.CallTo(() => _innerProvider.RetrieveAsync(reference, A<CancellationToken>._))
			.Returns(Task.FromResult(expectedPayload));

		// Act
		var result = await _sut.RetrieveAsync(reference, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedPayload);
	}

	[Fact]
	public async Task RetrieveAsync_PropagatesExceptionFromInner()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "ref-err" };
		A.CallTo(() => _innerProvider.RetrieveAsync(reference, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Retrieve failure"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.RetrieveAsync(reference, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_DelegatesToInner_AndReturnsTrue()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "ref-del" };
		A.CallTo(() => _innerProvider.DeleteAsync(reference, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Act
		var result = await _sut.DeleteAsync(reference, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task DeleteAsync_ReturnsFalse_WhenInnerReturnsFalse()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "ref-missing" };
		A.CallTo(() => _innerProvider.DeleteAsync(reference, A<CancellationToken>._))
			.Returns(Task.FromResult(false));

		// Act
		var result = await _sut.DeleteAsync(reference, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldUseClaimCheck_DelegatesToInner()
	{
		// Arrange
		var payload = new byte[500];
		A.CallTo(() => _innerProvider.ShouldUseClaimCheck(payload)).Returns(true);

		// Act
		var result = _sut.ShouldUseClaimCheck(payload);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ImplementIClaimCheckProvider()
	{
		_sut.ShouldBeAssignableTo<IClaimCheckProvider>();
	}

	[Fact]
	public async Task StoreAsync_RecordsMetrics()
	{
		// Arrange
		var payload = new byte[] { 1, 2, 3, 4, 5 };
		var expectedRef = new ClaimCheckReference { Id = "metric-ref" };
		long storedCount = 0;

		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, listener) =>
		{
			if (ReferenceEquals(instrument.Meter, _meter))
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
		{
			if (instrument.Name == ClaimCheckTelemetryConstants.MetricNames.PayloadsStored)
			{
				Interlocked.Increment(ref storedCount);
			}
		});
		listener.Start();

		A.CallTo(() => _innerProvider.StoreAsync(payload, A<CancellationToken>._, A<ClaimCheckMetadata>._))
			.Returns(Task.FromResult(expectedRef));

		// Act
		await _sut.StoreAsync(payload, CancellationToken.None);
		listener.RecordObservableInstruments();

		// Assert
		storedCount.ShouldBe(1);
	}
}
