// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Unit tests for the middleware partial of <see cref="SigningOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Signing")]
public sealed class SigningOptionsMiddlewareShould
{
    [Fact]
    public void DefaultRequireValidSignatureToTrue()
    {
        // Arrange & Act
        var options = new SigningOptions();

        // Assert
        options.RequireValidSignature.ShouldBeTrue();
    }

    [Fact]
    public void AllowSettingRequireValidSignatureToFalse()
    {
        // Arrange
        var options = new SigningOptions();

        // Act
        options.RequireValidSignature = false;

        // Assert
        options.RequireValidSignature.ShouldBeFalse();
    }

    [Fact]
    public void RetainSigningOptionsBaseProperties()
    {
        // Arrange & Act
        var options = new SigningOptions
        {
            Enabled = true,
            RequireValidSignature = false,
        };

        // Assert - middleware partial property
        options.RequireValidSignature.ShouldBeFalse();
        // Assert - base property
        options.Enabled.ShouldBeTrue();
    }
}
