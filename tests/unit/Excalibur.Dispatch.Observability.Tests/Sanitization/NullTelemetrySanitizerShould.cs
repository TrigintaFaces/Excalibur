// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Telemetry;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Unit tests for <see cref="NullTelemetrySanitizer"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class NullTelemetrySanitizerShould
{
	[Fact]
	public void Instance_BeASingleton()
	{
		// Act
		var instance1 = NullTelemetrySanitizer.Instance;
		var instance2 = NullTelemetrySanitizer.Instance;

		// Assert
		instance1.ShouldBeSameAs(instance2);
	}

	[Fact]
	public void Instance_ImplementITelemetrySanitizer()
	{
		// Assert
		NullTelemetrySanitizer.Instance.ShouldBeAssignableTo<ITelemetrySanitizer>();
	}

	[Theory]
	[InlineData("user.id", "alice")]
	[InlineData("auth.email", "alice@example.com")]
	[InlineData("auth.token", "secret-token")]
	[InlineData("unknown.tag", "any-value")]
	public void SanitizeTag_ReturnRawValueUnchanged(string tagName, string rawValue)
	{
		// Act
		var result = NullTelemetrySanitizer.Instance.SanitizeTag(tagName, rawValue);

		// Assert
		result.ShouldBe(rawValue);
	}

	[Fact]
	public void SanitizeTag_ReturnNullWhenRawValueIsNull()
	{
		// Act
		var result = NullTelemetrySanitizer.Instance.SanitizeTag("user.id", null);

		// Assert
		result.ShouldBeNull();
	}

	[Theory]
	[InlineData("{\"userId\":\"alice\"}")]
	[InlineData("plain text payload")]
	[InlineData("")]
	public void SanitizePayload_ReturnPayloadUnchanged(string payload)
	{
		// Act
		var result = NullTelemetrySanitizer.Instance.SanitizePayload(payload);

		// Assert
		result.ShouldBe(payload);
	}
}
