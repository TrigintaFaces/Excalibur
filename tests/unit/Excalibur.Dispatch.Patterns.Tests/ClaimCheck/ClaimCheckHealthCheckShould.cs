// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="ClaimCheckHealthCheck"/>.
/// Sprint 569 -- Task S569.21: Tests for S569.6 (TelemetryClaimCheckProvider + health check).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ClaimCheck")]
public sealed class ClaimCheckHealthCheckShould
{
	private readonly IClaimCheckProvider _provider;
	private readonly ClaimCheckHealthCheck _sut;

	public ClaimCheckHealthCheckShould()
	{
		_provider = A.Fake<IClaimCheckProvider>();
		_sut = new ClaimCheckHealthCheck(_provider);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ClaimCheckHealthCheck(null!));
	}

	[Fact]
	public void ImplementIHealthCheck()
	{
		_sut.ShouldBeAssignableTo<IHealthCheck>();
	}

	[Fact]
	public async Task ReturnHealthy_WhenRoundTripSucceeds()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "probe-ref" };
		var probePayload = "healthcheck-probe"u8.ToArray();

		A.CallTo(() => _provider.StoreAsync(
				A<byte[]>._,
				A<CancellationToken>._,
				A<ClaimCheckMetadata>._))
			.Returns(Task.FromResult(reference));

		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.Returns(Task.FromResult(probePayload));

		A.CallTo(() => _provider.DeleteAsync(reference, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("operational");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenStoreThrows()
	{
		// Arrange
		A.CallTo(() => _provider.StoreAsync(
				A<byte[]>._,
				A<CancellationToken>._,
				A<ClaimCheckMetadata>._))
			.Throws(new InvalidOperationException("Store failed"));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Exception.ShouldNotBeNull();
		result.Exception.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenRetrieveThrows()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "probe-ref" };

		A.CallTo(() => _provider.StoreAsync(
				A<byte[]>._,
				A<CancellationToken>._,
				A<ClaimCheckMetadata>._))
			.Returns(Task.FromResult(reference));

		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Retrieve failed"));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Exception.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenDataIntegrityCheckFails()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "probe-ref" };
		var corruptPayload = new byte[] { 0xFF, 0xFE };

		A.CallTo(() => _provider.StoreAsync(
				A<byte[]>._,
				A<CancellationToken>._,
				A<ClaimCheckMetadata>._))
			.Returns(Task.FromResult(reference));

		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.Returns(Task.FromResult(corruptPayload));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("integrity");
	}

	[Fact]
	public async Task StoreRetrieveDelete_InSequence()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "probe-ref" };
		var probePayload = "healthcheck-probe"u8.ToArray();

		A.CallTo(() => _provider.StoreAsync(
				A<byte[]>._,
				A<CancellationToken>._,
				A<ClaimCheckMetadata>._))
			.Returns(Task.FromResult(reference));

		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.Returns(Task.FromResult(probePayload));

		A.CallTo(() => _provider.DeleteAsync(reference, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Act
		await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert — verify all three operations were called
		A.CallTo(() => _provider.StoreAsync(
				A<byte[]>._,
				A<CancellationToken>._,
				A<ClaimCheckMetadata>._))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => _provider.DeleteAsync(reference, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassMetadata_WithHealthCheckProbeType()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "probe-ref" };
		var probePayload = "healthcheck-probe"u8.ToArray();
		ClaimCheckMetadata? capturedMetadata = null;

		A.CallTo(() => _provider.StoreAsync(
				A<byte[]>._,
				A<CancellationToken>._,
				A<ClaimCheckMetadata>._))
			.Invokes((byte[] _, CancellationToken _, ClaimCheckMetadata meta) => capturedMetadata = meta)
			.Returns(Task.FromResult(reference));

		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.Returns(Task.FromResult(probePayload));

		A.CallTo(() => _provider.DeleteAsync(reference, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Act
		await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert — metadata should indicate health check probe
		capturedMetadata.ShouldNotBeNull();
		capturedMetadata.MessageType.ShouldBe("HealthCheckProbe");
		capturedMetadata.ContentType.ShouldBe("application/octet-stream");
	}
}
