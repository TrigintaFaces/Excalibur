// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Smoke;

/// <summary>
/// Null guard tests for experience metapackage extension methods (Sprint 724 T.3 jyb3ow).
/// Each metapackage has two required parameters: services and provider-specific configure delegate.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MetapackageNullGuardTests
{
	#region AddDispatchRabbitMQ

	[Fact]
	public void AddDispatchRabbitMQ_ThrowsOnNullServices()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchRabbitMQ(_ => { }))
			.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddDispatchRabbitMQ_ThrowsOnNullConfigure()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchRabbitMQ(null!))
			.ParamName.ShouldBe("configureRabbitMQ");
	}

	[Fact]
	public void AddDispatchRabbitMQ_ReturnsServiceCollectionForChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddDispatchRabbitMQ(_ => { });
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region AddDispatchKafka

	[Fact]
	public void AddDispatchKafka_ThrowsOnNullServices()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchKafka(_ => { }))
			.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddDispatchKafka_ThrowsOnNullConfigure()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchKafka(null!))
			.ParamName.ShouldBe("configureKafka");
	}

	[Fact]
	public void AddDispatchKafka_ReturnsServiceCollectionForChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddDispatchKafka(_ => { });
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region AddDispatchAzure

	[Fact]
	public void AddDispatchAzure_ThrowsOnNullServices()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchAzure(_ => { }))
			.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddDispatchAzure_ThrowsOnNullConfigure()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchAzure(null!))
			.ParamName.ShouldBe("configureAzure");
	}

	[Fact]
	public void AddDispatchAzure_ReturnsServiceCollectionForChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddDispatchAzure(_ => { });
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region AddDispatchAws

	[Fact]
	public void AddDispatchAws_ThrowsOnNullServices()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchAws(_ => { }))
			.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddDispatchAws_ThrowsOnNullConfigure()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchAws(null!))
			.ParamName.ShouldBe("configureAws");
	}

	[Fact]
	public void AddDispatchAws_ReturnsServiceCollectionForChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddDispatchAws(_ => { });
		result.ShouldBeSameAs(services);
	}

	#endregion
}
