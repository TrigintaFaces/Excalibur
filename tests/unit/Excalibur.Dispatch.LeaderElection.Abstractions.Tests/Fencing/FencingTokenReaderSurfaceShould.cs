// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests.Fencing;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class FencingTokenReaderSurfaceShould
{
    [Fact]
    public void ExposeObservationWithoutAllowingAConsumerToMintThroughTheReaderContract()
    {
        var reader = typeof(IFencingTokenProvider);
        reader.GetMethod("GetTokenAsync").ShouldNotBeNull().ReturnType.ShouldBe(typeof(ValueTask<long?>));
        reader.GetMethod("ValidateTokenAsync").ShouldNotBeNull().ReturnType.ShouldBe(typeof(ValueTask<bool>));
        reader.GetMethod("IssueTokenAsync").ShouldBeNull(
            "a read-native provider must not promise a mint through the observation contract");
    }
}
