// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization.Grants;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Regression tests for GrantScope ArgumentNullException paramName fix (Sprint 727 T.3 zi9zuk).
/// Verifies that ParamName is the property name, not the property value.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantScopeParamNameShould
{
    [Fact]
    public void ReportTenantIdParamNameWhenNull()
    {
        var ex = Should.Throw<ArgumentNullException>(
            () => new GrantScope(null!, "Activity", "ReadOrders"));
        ex.ParamName.ShouldBe("TenantId");
    }

    [Fact]
    public void ReportTenantIdParamNameWhenEmpty()
    {
        var ex = Should.Throw<ArgumentNullException>(
            () => new GrantScope("", "Activity", "ReadOrders"));
        ex.ParamName.ShouldBe("TenantId");
    }

    [Fact]
    public void ReportGrantTypeParamNameWhenNull()
    {
        var ex = Should.Throw<ArgumentNullException>(
            () => new GrantScope("tenant-1", null!, "ReadOrders"));
        ex.ParamName.ShouldBe("GrantType");
    }

    [Fact]
    public void ReportGrantTypeParamNameWhenEmpty()
    {
        var ex = Should.Throw<ArgumentNullException>(
            () => new GrantScope("tenant-1", "", "ReadOrders"));
        ex.ParamName.ShouldBe("GrantType");
    }

    [Fact]
    public void ReportQualifierParamNameWhenNull()
    {
        var ex = Should.Throw<ArgumentNullException>(
            () => new GrantScope("tenant-1", "Activity", null!));
        ex.ParamName.ShouldBe("Qualifier");
    }

    [Fact]
    public void ReportQualifierParamNameWhenEmpty()
    {
        var ex = Should.Throw<ArgumentNullException>(
            () => new GrantScope("tenant-1", "Activity", ""));
        ex.ParamName.ShouldBe("Qualifier");
    }
}
