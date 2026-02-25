// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Unit tests for <see cref="TelemetrySanitizerOptions"/> defaults and configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class TelemetrySanitizerOptionsShould
{
	[Fact]
	public void HaveIncludeRawPiiFalseByDefault()
	{
		var options = new TelemetrySanitizerOptions();
		options.IncludeRawPii.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultSensitiveTagNames()
	{
		var options = new TelemetrySanitizerOptions();

		options.SensitiveTagNames.ShouldNotBeNull();
		options.SensitiveTagNames.ShouldContain("user.id");
		options.SensitiveTagNames.ShouldContain("user.name");
		options.SensitiveTagNames.ShouldContain("auth.user_id");
		options.SensitiveTagNames.ShouldContain("auth.subject_id");
		options.SensitiveTagNames.ShouldContain("auth.identity_name");
		options.SensitiveTagNames.ShouldContain("auth.tenant_id");
		options.SensitiveTagNames.ShouldContain("audit.user_id");
		options.SensitiveTagNames.ShouldContain("tenant.id");
		options.SensitiveTagNames.ShouldContain("tenant.name");
		options.SensitiveTagNames.ShouldContain("dispatch.messaging.tenant_id");
	}

	[Fact]
	public void HaveDefaultSuppressedTagNames()
	{
		var options = new TelemetrySanitizerOptions();

		options.SuppressedTagNames.ShouldNotBeNull();
		options.SuppressedTagNames.ShouldContain("auth.email");
		options.SuppressedTagNames.ShouldContain("auth.token");
	}

	[Fact]
	public void AllowOverridingSensitiveTagNames()
	{
		var options = new TelemetrySanitizerOptions
		{
			SensitiveTagNames = ["custom.tag"],
		};

		options.SensitiveTagNames.Count.ShouldBe(1);
		options.SensitiveTagNames.ShouldContain("custom.tag");
	}

	[Fact]
	public void AllowOverridingSuppressedTagNames()
	{
		var options = new TelemetrySanitizerOptions
		{
			SuppressedTagNames = ["custom.suppressed"],
		};

		options.SuppressedTagNames.Count.ShouldBe(1);
		options.SuppressedTagNames.ShouldContain("custom.suppressed");
	}
}
