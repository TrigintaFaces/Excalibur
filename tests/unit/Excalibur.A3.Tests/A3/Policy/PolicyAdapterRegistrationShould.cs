// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Policy.Cedar;
using Excalibur.A3.Policy.Opa;

using FakeItEasy;

namespace Excalibur.Tests.A3.Policy;

/// <summary>
/// Tests for OPA and Cedar policy adapter DI registration (Sprint 726 T.11 fki006).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class PolicyAdapterRegistrationShould
{
	#region OPA

	[Fact]
	public void UseOpaPolicy_ThrowsOnNullBuilder()
	{
		IA3Builder builder = null!;
		Should.Throw<ArgumentNullException>(() =>
			builder.UseOpaPolicy(_ => { }))
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void UseOpaPolicy_ThrowsOnNullConfigure()
	{
		var builder = A.Fake<IA3Builder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());
		Should.Throw<ArgumentNullException>(() =>
			builder.UseOpaPolicy(null!))
			.ParamName.ShouldBe("configure");
	}

	[Fact]
	public void UseOpaPolicy_ReturnsBuilderForChaining()
	{
		var builder = A.Fake<IA3Builder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		var result = builder.UseOpaPolicy(opts =>
		{
			opts.Endpoint = "http://localhost:8181";
			opts.PolicyPath = "v1/data/authz/allow";
		});

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void OpaOptions_HasCorrectDefaults()
	{
		var opts = new OpaOptions();
		opts.PolicyPath.ShouldBe("v1/data/authz/allow");
		opts.TimeoutMs.ShouldBe(5000);
		opts.FailClosed.ShouldBeTrue();
	}

	#endregion

	#region Cedar

	[Fact]
	public void UseCedarPolicy_ThrowsOnNullBuilder()
	{
		IA3Builder builder = null!;
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCedarPolicy(_ => { }))
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void UseCedarPolicy_ThrowsOnNullConfigure()
	{
		var builder = A.Fake<IA3Builder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCedarPolicy(null!))
			.ParamName.ShouldBe("configure");
	}

	[Fact]
	public void UseCedarPolicy_ReturnsBuilderForChaining()
	{
		var builder = A.Fake<IA3Builder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		var result = builder.UseCedarPolicy(opts =>
		{
			opts.Endpoint = "http://localhost:8180";
		});

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void CedarOptions_HasCorrectDefaults()
	{
		var opts = new CedarOptions();
		opts.TimeoutMs.ShouldBe(5000);
		opts.FailClosed.ShouldBeTrue();
		opts.Mode.ShouldBe(CedarMode.Local);
	}

	#endregion
}
