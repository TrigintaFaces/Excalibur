// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Depth coverage tests for <see cref="TelemetryClaimCheckProvider"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TelemetryClaimCheckProviderDepthShould : IDisposable
{
	private readonly IClaimCheckProvider _inner = A.Fake<IClaimCheckProvider>();
	private readonly Meter _meter = new("test.claimcheck");
	private readonly ActivitySource _activitySource = new("test.claimcheck");
	private readonly TelemetryClaimCheckProvider _sut;

	public TelemetryClaimCheckProviderDepthShould()
	{
		_sut = new TelemetryClaimCheckProvider(_inner, _meter, _activitySource);
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenInnerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryClaimCheckProvider(null!, _meter, _activitySource));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenMeterIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryClaimCheckProvider(_inner, null!, _activitySource));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenActivitySourceIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TelemetryClaimCheckProvider(_inner, _meter, null!));
	}

	[Fact]
	public async Task StoreAsync_DelegatesToInner()
	{
		var payload = new byte[] { 1, 2, 3 };
		var expected = new ClaimCheckReference { Id = "cc-1" };
		A.CallTo(() => _inner.StoreAsync(payload, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.Returns(expected);

		var result = await _sut.StoreAsync(payload, CancellationToken.None);

		result.ShouldBeSameAs(expected);
		A.CallTo(() => _inner.StoreAsync(payload, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StoreAsync_PropagatesException()
	{
		A.CallTo(() => _inner.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.ThrowsAsync(new InvalidOperationException("storage error"));

		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.StoreAsync([1], CancellationToken.None));
	}

	[Fact]
	public async Task RetrieveAsync_DelegatesToInner()
	{
		var reference = new ClaimCheckReference { Id = "cc-1" };
		var expected = new byte[] { 4, 5, 6 };
		A.CallTo(() => _inner.RetrieveAsync(reference, A<CancellationToken>._))
			.Returns(expected);

		var result = await _sut.RetrieveAsync(reference, CancellationToken.None);

		result.ShouldBe(expected);
	}

	[Fact]
	public async Task RetrieveAsync_PropagatesException()
	{
		var reference = new ClaimCheckReference { Id = "cc-1" };
		A.CallTo(() => _inner.RetrieveAsync(reference, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("not found"));

		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.RetrieveAsync(reference, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_DelegatesToInner_ReturnsTrue()
	{
		var reference = new ClaimCheckReference { Id = "cc-1" };
		A.CallTo(() => _inner.DeleteAsync(reference, A<CancellationToken>._))
			.Returns(true);

		var result = await _sut.DeleteAsync(reference, CancellationToken.None);

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task DeleteAsync_DelegatesToInner_ReturnsFalse()
	{
		var reference = new ClaimCheckReference { Id = "cc-1" };
		A.CallTo(() => _inner.DeleteAsync(reference, A<CancellationToken>._))
			.Returns(false);

		var result = await _sut.DeleteAsync(reference, CancellationToken.None);

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task DeleteAsync_PropagatesException()
	{
		var reference = new ClaimCheckReference { Id = "cc-1" };
		A.CallTo(() => _inner.DeleteAsync(reference, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("delete error"));

		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.DeleteAsync(reference, CancellationToken.None));
	}

	[Fact]
	public void ShouldUseClaimCheck_DelegatesToInner()
	{
		var payload = new byte[] { 1, 2, 3 };
		A.CallTo(() => _inner.ShouldUseClaimCheck(payload)).Returns(true);

		_sut.ShouldUseClaimCheck(payload).ShouldBeTrue();
	}

	public void Dispose()
	{
		_meter.Dispose();
		_activitySource.Dispose();
	}
}
