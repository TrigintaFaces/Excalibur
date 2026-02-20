// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sns;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSnsTransportServiceCollectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsSnsTransportServiceCollectionExtensions.AddAwsSnsTransport(
				null!, "orders", _ => { }));
	}

	[Fact]
	public void ThrowWhenNameIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddAwsSnsTransport(null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenNameIsEmpty()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddAwsSnsTransport("", _ => { }));
	}

	[Fact]
	public void ThrowWhenNameIsWhitespace()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddAwsSnsTransport("  ", _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsSnsTransport("orders", null!));
	}

	[Fact]
	public void HaveDefaultTransportName()
	{
		AwsSnsTransportServiceCollectionExtensions.DefaultTransportName.ShouldBe("aws-sns");
	}

	[Fact]
	public void RegisterSnsServicesWithDefaultName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAwsSnsTransport(sns =>
		{
			sns.TopicArn("arn:aws:sns:us-east-1:123456789:test-topic")
				.Region("us-east-1");
		});

		// Assert
		result.ShouldBeSameAs(services);
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RegisterSnsServicesWithCustomName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsSnsTransport("notifications", sns =>
		{
			sns.TopicArn("arn:aws:sns:us-east-1:123456789:test-topic")
				.Region("us-east-1");
		});

		// Assert
		services.Count.ShouldBeGreaterThan(0);
	}
}
