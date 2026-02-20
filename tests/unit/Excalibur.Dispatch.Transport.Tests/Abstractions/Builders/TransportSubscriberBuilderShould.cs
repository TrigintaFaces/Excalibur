// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Builders;

/// <summary>
/// Tests for <see cref="TransportSubscriberBuilder"/>.
/// Verifies decorator composition ordering and Build() behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public class TransportSubscriberBuilderShould
{
	private readonly ITransportSubscriber _innerSubscriber = A.Fake<ITransportSubscriber>();

	[Fact]
	public void Build_Returns_InnerSubscriber_When_No_Decorators()
	{
		var builder = new TransportSubscriberBuilder(_innerSubscriber);
		var result = builder.Build();

		result.ShouldBeSameAs(_innerSubscriber);
	}

	[Fact]
	public void Build_Applies_Single_Decorator()
	{
		var decorator = A.Fake<ITransportSubscriber>();
		var builder = new TransportSubscriberBuilder(_innerSubscriber)
			.Use(_ => decorator);

		var result = builder.Build();

		result.ShouldBeSameAs(decorator);
	}

	[Fact]
	public void Build_Applies_Decorators_In_Registration_Order()
	{
		var callOrder = new List<string>();

		var builder = new TransportSubscriberBuilder(_innerSubscriber)
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
		var builder = new TransportSubscriberBuilder(_innerSubscriber)
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
		var builder = new TransportSubscriberBuilder(_innerSubscriber);
		var returned = builder.Use(inner => inner);

		returned.ShouldBeSameAs(builder);
	}

	[Fact]
	public void Throw_When_InnerSubscriber_Is_Null()
	{
		Should.Throw<ArgumentNullException>(() => new TransportSubscriberBuilder(null!));
	}

	[Fact]
	public void Throw_When_Decorator_Is_Null()
	{
		var builder = new TransportSubscriberBuilder(_innerSubscriber);
		Should.Throw<ArgumentNullException>(() => builder.Use(null!));
	}

	/// <summary>Test decorator that tracks its name and exposes the inner subscriber.</summary>
	private sealed class TrackingDecorator : DelegatingTransportSubscriber
	{
		public string Name { get; }

		public TrackingDecorator(ITransportSubscriber inner, string name) : base(inner)
		{
			Name = name;
		}

		public ITransportSubscriber GetInner() => InnerSubscriber;
	}
}
