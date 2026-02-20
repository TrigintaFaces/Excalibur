// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessPlatformShould
{
    [Fact]
    public void HaveUnknownAsDefaultValue()
    {
        // Arrange & Act
        var platform = default(ServerlessPlatform);

        // Assert
        platform.ShouldBe(ServerlessPlatform.Unknown);
    }

    [Fact]
    public void DefineExpectedEnumValues()
    {
        // Assert
        ((int)ServerlessPlatform.Unknown).ShouldBe(0);
        ((int)ServerlessPlatform.AwsLambda).ShouldBe(1);
        ((int)ServerlessPlatform.AzureFunctions).ShouldBe(2);
        ((int)ServerlessPlatform.GoogleCloudFunctions).ShouldBe(3);
    }

    [Theory]
    [InlineData(ServerlessPlatform.Unknown, "Unknown")]
    [InlineData(ServerlessPlatform.AwsLambda, "AwsLambda")]
    [InlineData(ServerlessPlatform.AzureFunctions, "AzureFunctions")]
    [InlineData(ServerlessPlatform.GoogleCloudFunctions, "GoogleCloudFunctions")]
    public void ConvertToStringCorrectly(ServerlessPlatform platform, string expected)
    {
        // Act & Assert
        platform.ToString().ShouldBe(expected);
    }

    [Fact]
    public void HaveExactlyFourValues()
    {
        // Act
        var values = Enum.GetValues<ServerlessPlatform>();

        // Assert
        values.Length.ShouldBe(4);
    }
}
