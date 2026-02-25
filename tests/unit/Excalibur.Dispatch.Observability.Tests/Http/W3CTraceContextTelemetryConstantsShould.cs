// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Http;

namespace Excalibur.Dispatch.Observability.Tests.Http;

/// <summary>
/// Unit tests for <see cref="W3CTraceContextTelemetryConstants"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Http")]
public sealed class W3CTraceContextTelemetryConstantsShould
{
	[Fact]
	public void HaveCorrectActivitySourceName()
	{
		W3CTraceContextTelemetryConstants.ActivitySourceName
			.ShouldBe("Excalibur.Dispatch.Observability.W3CTraceContext");
	}

	[Fact]
	public void HaveNonEmptyVersion()
	{
		W3CTraceContextTelemetryConstants.Version.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void HaveVersionInSemVerFormat()
	{
		// Version should be in X.Y.Z format
		var parts = W3CTraceContextTelemetryConstants.Version.Split('.');
		parts.Length.ShouldBe(3);

		foreach (var part in parts)
		{
			int.TryParse(part, out _).ShouldBeTrue($"Version part '{part}' is not a valid integer");
		}
	}
}
