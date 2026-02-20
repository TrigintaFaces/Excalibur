// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Cloud;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudProviderServiceCollectionExtensionsShould
{
    private readonly IDispatchBuilder _builder = A.Fake<IDispatchBuilder>();
    private readonly IServiceCollection _services = new ServiceCollection();

    public CloudProviderServiceCollectionExtensionsShould()
    {
        A.CallTo(() => _builder.Services).Returns(_services);
    }

    [Fact]
    public void AddAwsProviders_Returns_Builder()
    {
        var result = _builder.AddAwsProviders();
        result.ShouldBeSameAs(_builder);
    }

    [Fact]
    public void AddAwsProviders_Throws_When_Builder_Null()
    {
        Should.Throw<ArgumentNullException>(() =>
            CloudProviderServiceCollectionExtensions.AddAwsProviders(null!));
    }

    [Fact]
    public void AddAwsProviders_Invokes_Configure_Action()
    {
        var invoked = false;
        _builder.AddAwsProviders(services =>
        {
            invoked = true;
            services.ShouldBeSameAs(_services);
        });
        invoked.ShouldBeTrue();
    }

    [Fact]
    public void AddAwsProviders_Handles_Null_Configure()
    {
        Should.NotThrow(() => _builder.AddAwsProviders(null));
    }

    [Fact]
    public void AddAzureProviders_Returns_Builder()
    {
        var result = _builder.AddAzureProviders();
        result.ShouldBeSameAs(_builder);
    }

    [Fact]
    public void AddAzureProviders_Throws_When_Builder_Null()
    {
        Should.Throw<ArgumentNullException>(() =>
            CloudProviderServiceCollectionExtensions.AddAzureProviders(null!));
    }

    [Fact]
    public void AddAzureProviders_Invokes_Configure_Action()
    {
        var invoked = false;
        _builder.AddAzureProviders(services =>
        {
            invoked = true;
            services.ShouldBeSameAs(_services);
        });
        invoked.ShouldBeTrue();
    }

    [Fact]
    public void AddGoogleCloudProviders_Returns_Builder()
    {
        var result = _builder.AddGoogleCloudProviders();
        result.ShouldBeSameAs(_builder);
    }

    [Fact]
    public void AddGoogleCloudProviders_Throws_When_Builder_Null()
    {
        Should.Throw<ArgumentNullException>(() =>
            CloudProviderServiceCollectionExtensions.AddGoogleCloudProviders(null!));
    }

    [Fact]
    public void AddGoogleCloudProviders_Invokes_Configure_Action()
    {
        var invoked = false;
        _builder.AddGoogleCloudProviders(services =>
        {
            invoked = true;
        });
        invoked.ShouldBeTrue();
    }

    [Fact]
    public void AddAllCloudProviders_Returns_Builder()
    {
        var result = _builder.AddAllCloudProviders();
        result.ShouldBeSameAs(_builder);
    }
}
