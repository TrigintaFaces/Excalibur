// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Builders;

/// <summary>
/// Tests for <see cref="TransportReceiverBuilder"/>.
/// Verifies decorator composition ordering and Build() behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public class TransportReceiverBuilderShould
{
	private readonly ITransportReceiver _innerReceiver = A.Fake<ITransportReceiver>();

	[Fact]
	public void Build_Returns_InnerReceiver_When_No_Decorators()
	{
		var builder = new TransportReceiverBuilder(_innerReceiver);
		var result = builder.Build();

		result.ShouldBeSameAs(_innerReceiver);
	}

	[Fact]
	public void Build_Applies_Single_Decorator()
	{
		var decorator = A.Fake<ITransportReceiver>();
		var builder = new TransportReceiverBuilder(_innerReceiver)
			.Use(_ => decorator);

		var result = builder.Build();

		result.ShouldBeSameAs(decorator);
	}

	[Fact]
	public void Build_Applies_Decorators_In_Registration_Order()
	{
		var callOrder = new List<string>();

		var builder = new TransportReceiverBuilder(_innerReceiver)
			.Use(inner =>
			{
				callOrder.Add("first");
				return new TrackingDecorator(inner, "first");
			})
			.Use(inner =>
			{
				callOrder.Add("second");
				return new TrackingDecorator(inner, "second");
			})
			.Use(inner =>
			{
				callOrder.Add("third");
				return new TrackingDecorator(inner, "third");
			});

		var result = builder.Build();

		callOrder.ShouldBe(["first", "second", "third"]);

		// The outermost decorator should be "third" (last registered wraps the previous)
		result.ShouldBeOfType<TrackingDecorator>().Name.ShouldBe("third");
	}

	[Fact]
	public void Build_Chains_Decorators_Correctly()
	{
		var builder = new TransportReceiverBuilder(_innerReceiver)
			.Use(inner => new TrackingDecorator(inner, "outer"))
			.Use(inner => new TrackingDecorator(inner, "outermost"));

		var result = builder.Build();

		// Outermost layer
		var outermost = result.ShouldBeOfType<TrackingDecorator>();
		outermost.Name.ShouldBe("outermost");

		// Inner layer (the inner of outermost should be "outer")
		outermost.GetInner().ShouldBeOfType<TrackingDecorator>().Name.ShouldBe("outer");
	}

	[Fact]
	public void Use_Is_Fluent()
	{
		var builder = new TransportReceiverBuilder(_innerReceiver);
		var returned = builder.Use(inner => inner);

		returned.ShouldBeSameAs(builder);
	}

	[Fact]
	public void Throw_When_InnerReceiver_Is_Null()
	{
		Should.Throw<ArgumentNullException>(() => new TransportReceiverBuilder(null!));
	}

	[Fact]
	public void Throw_When_Decorator_Is_Null()
	{
		var builder = new TransportReceiverBuilder(_innerReceiver);
		Should.Throw<ArgumentNullException>(() => builder.Use(null!));
	}

	/// <summary>Test decorator that tracks its name and exposes the inner receiver.</summary>
	private sealed class TrackingDecorator : DelegatingTransportReceiver
	{
		public string Name { get; }

		public TrackingDecorator(ITransportReceiver inner, string name) : base(inner)
		{
			Name = name;
		}

		public ITransportReceiver GetInner() => InnerReceiver;
	}
}
