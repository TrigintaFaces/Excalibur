// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Cloud;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudWatchMetricsCollectorShould
{
    [Fact]
    public void Be_Disposable()
    {
        var sut = new CloudWatchMetricsCollector();
        sut.ShouldBeAssignableTo<IDisposable>();
    }

    [Fact]
    public void Dispose_Without_Throwing()
    {
        var sut = new CloudWatchMetricsCollector();
        Should.NotThrow(() => sut.Dispose());
    }

    [Fact]
    public void Allow_Multiple_Dispose_Calls()
    {
        var sut = new CloudWatchMetricsCollector();
        Should.NotThrow(() =>
        {
            sut.Dispose();
            sut.Dispose();
        });
    }
}
