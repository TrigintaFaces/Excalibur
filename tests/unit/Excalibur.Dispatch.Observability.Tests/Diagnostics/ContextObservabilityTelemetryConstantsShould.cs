// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Diagnostics;

namespace Excalibur.Dispatch.Observability.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="ContextObservabilityTelemetryConstants"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Diagnostics")]
public sealed class ContextObservabilityTelemetryConstantsShould
{
	[Fact]
	public void HaveCorrectMeterName()
	{
		ContextObservabilityTelemetryConstants.MeterName
			.ShouldBe("Excalibur.Dispatch.Observability.Context");
	}

	[Fact]
	public void HaveCorrectActivitySourceName()
	{
		ContextObservabilityTelemetryConstants.ActivitySourceName
			.ShouldBe("Excalibur.Dispatch.Observability.Context");
	}

	[Fact]
	public void HaveCorrectMiddlewareActivitySourceName()
	{
		ContextObservabilityTelemetryConstants.MiddlewareActivitySourceName
			.ShouldBe("Excalibur.Dispatch.Observability.ContextMiddleware");
	}

	[Fact]
	public void HaveNonEmptyVersion()
	{
		ContextObservabilityTelemetryConstants.Version.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void HaveVersionInSemanticFormat()
	{
		// Version should be in X.Y.Z format
		var parts = ContextObservabilityTelemetryConstants.Version.Split('.');
		parts.Length.ShouldBeGreaterThanOrEqualTo(2);
	}
}
