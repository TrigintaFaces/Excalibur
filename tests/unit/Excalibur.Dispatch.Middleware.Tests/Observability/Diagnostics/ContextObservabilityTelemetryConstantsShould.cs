// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Diagnostics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ContextObservabilityTelemetryConstantsShould
{
	[Fact]
	public void HaveNonEmptyActivitySourceName()
	{
		ContextObservabilityTelemetryConstants.ActivitySourceName.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void HaveNonEmptyVersion()
	{
		ContextObservabilityTelemetryConstants.Version.ShouldNotBeNullOrWhiteSpace();
	}
}
