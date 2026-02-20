// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions.Telemetry;

namespace Excalibur.Dispatch.Abstractions.Tests.Telemetry;

/// <summary>
/// Unit tests for the <see cref="NullTelemetrySanitizer"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class NullTelemetrySanitizerShould
{
	[Fact]
	public void Instance_Should_ReturnSingleton()
	{
		// Act
		var a = NullTelemetrySanitizer.Instance;
		var b = NullTelemetrySanitizer.Instance;

		// Assert
		a.ShouldBeSameAs(b);
	}

	[Fact]
	public void Implement_ITelemetrySanitizer()
	{
		// Assert
		NullTelemetrySanitizer.Instance.ShouldBeAssignableTo<ITelemetrySanitizer>();
	}

	[Fact]
	public void SanitizeTag_Should_PassThroughValue()
	{
		// Act
		var result = NullTelemetrySanitizer.Instance.SanitizeTag("user.id", "john@example.com");

		// Assert
		result.ShouldBe("john@example.com");
	}

	[Fact]
	public void SanitizeTag_Should_ReturnNull_WhenValueIsNull()
	{
		// Act
		var result = NullTelemetrySanitizer.Instance.SanitizeTag("user.id", null);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SanitizePayload_Should_PassThroughPayload()
	{
		// Arrange
		var payload = "{\"userId\":\"secret-123\",\"email\":\"test@test.com\"}";

		// Act
		var result = NullTelemetrySanitizer.Instance.SanitizePayload(payload);

		// Assert
		result.ShouldBe(payload);
	}
}
