// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

using FakeItEasy;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Security.Tests.Security;

/// <summary>
/// Unit tests for <see cref="DispatchBuilderSecurityExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "DI")]
public sealed class DispatchBuilderSecurityExtensionsShould
{
    [Fact]
    public void ThrowWhenBuilderIsNull()
    {
        // Arrange
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            DispatchBuilderSecurityExtensions.AddSecurity(null!, config));
    }

    [Fact]
    public void ThrowWhenConfigurationIsNull()
    {
        // Arrange
        var builder = A.Fake<IDispatchBuilder>();
        A.CallTo(() => builder.Services).Returns(new ServiceCollection());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.AddSecurity(null!));
    }

    [Fact]
    public void ReturnBuilderForFluentChaining()
    {
        // Arrange
        var builder = A.Fake<IDispatchBuilder>();
        A.CallTo(() => builder.Services).Returns(new ServiceCollection());
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        // Act
        var result = builder.AddSecurity(config);

        // Assert
        result.ShouldBe(builder);
    }
}
