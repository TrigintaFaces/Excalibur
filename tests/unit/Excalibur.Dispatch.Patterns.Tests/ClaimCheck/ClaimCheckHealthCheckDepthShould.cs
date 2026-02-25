// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Depth coverage tests for <see cref="ClaimCheckHealthCheck"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ClaimCheckHealthCheckDepthShould
{
	private readonly IClaimCheckProvider _provider = A.Fake<IClaimCheckProvider>();

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ClaimCheckHealthCheck(null!));
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsHealthy_WhenRoundTripSucceeds()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "probe-1" };
		A.CallTo(() => _provider.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.Returns(reference);
		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.Returns("healthcheck-probe"u8.ToArray());
		A.CallTo(() => _provider.DeleteAsync(reference, A<CancellationToken>._))
			.Returns(true);

		var healthCheck = new ClaimCheckHealthCheck(_provider);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthy_WhenDataIntegrityFails()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "probe-1" };
		A.CallTo(() => _provider.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.Returns(reference);
		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.Returns(new byte[] { 0xFF, 0xFE }); // Different from probe payload

		var healthCheck = new ClaimCheckHealthCheck(_provider);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("integrity");
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthy_WhenStoreThrows()
	{
		// Arrange
		A.CallTo(() => _provider.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.ThrowsAsync(new InvalidOperationException("storage unavailable"));

		var healthCheck = new ClaimCheckHealthCheck(_provider);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Exception.ShouldNotBeNull();
		result.Exception.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthy_WhenRetrieveThrows()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "probe-1" };
		A.CallTo(() => _provider.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.Returns(reference);
		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.ThrowsAsync(new KeyNotFoundException("not found"));

		var healthCheck = new ClaimCheckHealthCheck(_provider);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Exception.ShouldBeOfType<KeyNotFoundException>();
	}

	[Fact]
	public async Task CheckHealthAsync_StillHealthy_WhenDeleteFails()
	{
		// Arrange - Store and retrieve succeed, but delete returns false
		var reference = new ClaimCheckReference { Id = "probe-1" };
		A.CallTo(() => _provider.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.Returns(reference);
		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.Returns("healthcheck-probe"u8.ToArray());
		A.CallTo(() => _provider.DeleteAsync(reference, A<CancellationToken>._))
			.Returns(false);

		var healthCheck = new ClaimCheckHealthCheck(_provider);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert - Still healthy because store+retrieve succeeded with matching data
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public async Task CheckHealthAsync_PassesMetadata_ToStoreAsync()
	{
		// Arrange
		var reference = new ClaimCheckReference { Id = "probe-1" };
		ClaimCheckMetadata? capturedMetadata = null;
		A.CallTo(() => _provider.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.Invokes((byte[] _, CancellationToken _, ClaimCheckMetadata? m) => capturedMetadata = m)
			.Returns(reference);
		A.CallTo(() => _provider.RetrieveAsync(reference, A<CancellationToken>._))
			.Returns("healthcheck-probe"u8.ToArray());
		A.CallTo(() => _provider.DeleteAsync(reference, A<CancellationToken>._))
			.Returns(true);

		var healthCheck = new ClaimCheckHealthCheck(_provider);

		// Act
		await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		capturedMetadata.ShouldNotBeNull();
		capturedMetadata.MessageType.ShouldBe("HealthCheckProbe");
		capturedMetadata.ContentType.ShouldBe("application/octet-stream");
	}
}
