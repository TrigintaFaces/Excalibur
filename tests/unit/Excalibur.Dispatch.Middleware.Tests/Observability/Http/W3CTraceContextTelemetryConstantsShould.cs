// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Http;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Http;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class W3CTraceContextTelemetryConstantsShould
{
	[Fact]
	public void HaveExpectedActivitySourceName()
	{
		W3CTraceContextTelemetryConstants.ActivitySourceName
			.ShouldBe("Excalibur.Dispatch.Observability.W3CTraceContext");
	}

	[Fact]
	public void HaveNonEmptyVersion()
	{
		W3CTraceContextTelemetryConstants.Version.ShouldNotBeNullOrWhiteSpace();
	}
}
