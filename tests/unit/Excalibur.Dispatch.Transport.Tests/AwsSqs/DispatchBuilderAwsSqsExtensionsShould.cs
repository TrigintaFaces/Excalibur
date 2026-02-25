// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DispatchBuilderAwsSqsExtensionsShould
{
	[Fact]
	public void ThrowWhenBuilderIsNull_UseAwsSqs()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DispatchBuilderAwsSqsExtensions.UseAwsSqs(null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull_UseAwsSqs()
	{
		// Arrange
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseAwsSqs((Action<IAwsSqsTransportBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_UseAwsSqs()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseAwsSqs(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ThrowWhenBuilderIsNull_UseAwsSqsNamed()
	{
		Should.Throw<ArgumentNullException>(() =>
			DispatchBuilderAwsSqsExtensions.UseAwsSqs(null!, "orders", _ => { }));
	}

	[Fact]
	public void ThrowWhenNameIsNull_UseAwsSqsNamed()
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		Should.Throw<ArgumentException>(() =>
			builder.UseAwsSqs(null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenNameIsEmpty_UseAwsSqsNamed()
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		Should.Throw<ArgumentException>(() =>
			builder.UseAwsSqs("", _ => { }));
	}

	[Fact]
	public void ThrowWhenNameIsWhitespace_UseAwsSqsNamed()
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		Should.Throw<ArgumentException>(() =>
			builder.UseAwsSqs("  ", _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull_UseAwsSqsNamed()
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());

		Should.Throw<ArgumentNullException>(() =>
			builder.UseAwsSqs("orders", (Action<IAwsSqsTransportBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_UseAwsSqsNamed()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseAwsSqs("orders", _ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}
}
