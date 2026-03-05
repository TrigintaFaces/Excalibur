// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

[Trait("Category", TestCategories.Unit)]
public sealed class DefaultFipsDetectorShould
{
	private readonly ILogger<DefaultFipsDetector> _logger;

	public DefaultFipsDetectorShould()
	{
		_logger = NullLogger<DefaultFipsDetector>.Instance;
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DefaultFipsDetector(null!));
	}

	[Fact]
	public void ReturnConsistentIsFipsEnabled()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var first = sut.IsFipsEnabled;
		var second = sut.IsFipsEnabled;

		// Assert - value should be consistent (cached)
		first.ShouldBe(second);
	}

	[Fact]
	public void GetStatus_ReturnValidResult()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var status = sut.GetStatus();

		// Assert
		_ = status.ShouldNotBeNull();
		status.Platform.ShouldNotBeNullOrEmpty();
		status.ValidationDetails.ShouldNotBeNullOrEmpty();
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		status.DetectedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public void GetStatus_ReturnCachedResult()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var first = sut.GetStatus();
		var second = sut.GetStatus();

		// Assert - should be same object (cached via Lazy<T>)
		ReferenceEquals(first, second).ShouldBeTrue();
	}

	[Fact]
	public void GetStatus_IncludePlatformIdentifier()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var status = sut.GetStatus();

		// Assert - platform should be recognized
		var validPlatforms = new[] { "Windows", "Linux", "macOS", "Unknown" };
		validPlatforms.ShouldContain(status.Platform);
	}

	[Fact]
	public void GetStatus_IsFipsEnabled_MatchesProperty()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var propertyValue = sut.IsFipsEnabled;
		var statusValue = sut.GetStatus().IsFipsEnabled;

		// Assert
		propertyValue.ShouldBe(statusValue);
	}

	[Fact]
	public void GetStatus_IncludeValidationDetails_ForCurrentPlatform()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var status = sut.GetStatus();

		// Assert - status should align with current platform and provide diagnostics.
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			status.Platform.ShouldBe("Windows");
			status.ValidationDetails.ShouldContain("Windows");
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			status.Platform.ShouldBe("Linux");
			status.ValidationDetails.ShouldNotBeNullOrWhiteSpace();
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			status.Platform.ShouldBe("macOS");
			status.ValidationDetails.ShouldNotBeNullOrWhiteSpace();
		}
	}

	[Fact]
	public void GetStatus_HaveRecentDetectedAtTimestamp()
	{
		// Arrange
		var beforeCreation = DateTimeOffset.UtcNow;
		var sut = CreateSut();

		// Act
		var status = sut.GetStatus();

		// Assert - detection should be recent
		status.DetectedAt.ShouldBeGreaterThanOrEqualTo(beforeCreation.AddSeconds(-1));
		var assertionUpperBound1 = DateTimeOffset.UtcNow.AddSeconds(1);
		status.DetectedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	private DefaultFipsDetector CreateSut() => new(_logger);
}

[Trait("Category", TestCategories.Unit)]
public sealed class FipsDetectionResultShould
{
	[Fact]
	public void CreateEnabled_WithCorrectProperties()
	{
		// Act
		var result = FipsDetectionResult.Enabled("Windows", "FIPS registry key enabled");

		// Assert
		result.IsFipsEnabled.ShouldBeTrue();
		result.Platform.ShouldBe("Windows");
		result.ValidationDetails.ShouldBe("FIPS registry key enabled");
	}

	[Fact]
	public void CreateDisabled_WithCorrectProperties()
	{
		// Act
		var result = FipsDetectionResult.Disabled("Linux", "FIPS not configured");

		// Assert
		result.IsFipsEnabled.ShouldBeFalse();
		result.Platform.ShouldBe("Linux");
		result.ValidationDetails.ShouldBe("FIPS not configured");
	}

	[Fact]
	public void HaveDefaultDetectedAtTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = FipsDetectionResult.Enabled("Test", "Test details");

		// Assert
		result.DetectedAt.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(-1));
		var assertionUpperBound1 = DateTimeOffset.UtcNow.AddSeconds(1);
		result.DetectedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public void AllowCustomDetectedAtTimestamp()
	{
		// Arrange
		var customTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

		// Act
		var result = new FipsDetectionResult
		{
			IsFipsEnabled = true,
			Platform = "Test",
			ValidationDetails = "Test",
			DetectedAt = customTime
		};

		// Assert
		result.DetectedAt.ShouldBe(customTime);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var result1 = new FipsDetectionResult
		{
			IsFipsEnabled = true,
			Platform = "Windows",
			ValidationDetails = "Test",
			DetectedAt = timestamp
		};
		var result2 = new FipsDetectionResult
		{
			IsFipsEnabled = true,
			Platform = "Windows",
			ValidationDetails = "Test",
			DetectedAt = timestamp
		};

		// Assert
		result1.ShouldBe(result2);
	}

	[Fact]
	public void SupportRecordInequality_WhenDifferentValues()
	{
		// Arrange
		var result1 = FipsDetectionResult.Enabled("Windows", "Test");
		var result2 = FipsDetectionResult.Disabled("Windows", "Test");

		// Assert
		result1.ShouldNotBe(result2);
	}
}
