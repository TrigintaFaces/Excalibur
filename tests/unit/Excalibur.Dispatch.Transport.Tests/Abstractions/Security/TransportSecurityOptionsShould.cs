// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Security;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.TransportAbstractions)]
public sealed class TransportSecurityOptionsShould
{
    [Fact]
    public void DefaultToRequiringTls()
    {
        var options = new TransportSecurityOptions();

        options.RequireTls.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingRequireTls(bool require)
    {
        var options = new TransportSecurityOptions { RequireTls = require };

        options.RequireTls.ShouldBe(require);
    }
}
