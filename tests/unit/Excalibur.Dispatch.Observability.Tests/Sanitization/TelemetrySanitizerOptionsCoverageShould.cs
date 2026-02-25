// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TelemetrySanitizerOptionsCoverageShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        // Act
        var options = new TelemetrySanitizerOptions();

        // Assert
        options.IncludeRawPii.ShouldBeFalse();
        options.SensitiveTagNames.ShouldNotBeNull();
        options.SensitiveTagNames.Count.ShouldBeGreaterThan(0);
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
        // Act
        var options = new TelemetrySanitizerOptions();

        // Assert
        options.SuppressedTagNames.ShouldNotBeNull();
        options.SuppressedTagNames.ShouldContain("auth.email");
        options.SuppressedTagNames.ShouldContain("auth.token");
    }

    [Fact]
    public void AllowCustomSensitiveTagNames()
    {
        // Act
        var options = new TelemetrySanitizerOptions
        {
            SensitiveTagNames = new List<string> { "custom.tag1", "custom.tag2" },
        };

        // Assert
        options.SensitiveTagNames.Count.ShouldBe(2);
        options.SensitiveTagNames.ShouldContain("custom.tag1");
        options.SensitiveTagNames.ShouldContain("custom.tag2");
    }

    [Fact]
    public void AllowCustomSuppressedTagNames()
    {
        // Act
        var options = new TelemetrySanitizerOptions
        {
            SuppressedTagNames = new List<string> { "sensitive.data" },
        };

        // Assert
        options.SuppressedTagNames.Count.ShouldBe(1);
        options.SuppressedTagNames.ShouldContain("sensitive.data");
    }

    [Fact]
    public void AllowBypassSanitization()
    {
        // Act
        var options = new TelemetrySanitizerOptions { IncludeRawPii = true };

        // Assert
        options.IncludeRawPii.ShouldBeTrue();
    }
}
