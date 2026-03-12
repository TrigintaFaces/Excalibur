// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Hosting.Builders;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Cdc.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="CdcExcaliburBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class CdcExcaliburBuilderExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddCdc_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IExcaliburBuilder builder = null!;
		Action<ICdcBuilder> configure = _ => { };

		Should.Throw<ArgumentNullException>(() =>
			builder.AddCdc(configure));
	}

	[Fact]
	public void AddCdc_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		var builder = A.Fake<IExcaliburBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		Should.Throw<ArgumentNullException>(() =>
			builder.AddCdc(null!));
	}

	[Fact]
	public void AddCdc_ReturnsSameBuilder_ForFluentChaining()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IExcaliburBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		var result = builder.AddCdc(_ => { });

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddCdc_RegistersCdcOptions()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IExcaliburBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		builder.AddCdc(_ => { });

		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<CdcOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void AddCdc_InvokesConfigureAction()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IExcaliburBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		var configureInvoked = false;

		builder.AddCdc(_ => configureInvoked = true);

		configureInvoked.ShouldBeTrue();
	}
}
