// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for the params Assembly[] overload of AddHandlersFromAssembly.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchBuilderExtensionsParamsShould
{
	[Fact]
	public void ThrowOnNullBuilder()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddHandlersFromAssembly(typeof(object).Assembly));
	}

	[Fact]
	public void ThrowOnNullAssembliesArray()
	{
		// Arrange
		var builder = A.Fake<IDispatchBuilder>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddHandlersFromAssembly((Assembly[])null!));
	}

	[Fact]
	public void ThrowOnNullAssemblyInArray()
	{
		// Arrange
		var builder = A.Fake<IDispatchBuilder>();
		var assemblies = new Assembly[] { typeof(object).Assembly, null! };

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() =>
			builder.AddHandlersFromAssembly(assemblies));
		ex.Message.ShouldContain("index 1");
	}

	[Fact]
	public void AcceptEmptyArrayAsNoOp()
	{
		// Arrange
		var builder = A.Fake<IDispatchBuilder>();

		// Act -- should not throw
		builder.AddHandlersFromAssembly(Array.Empty<Assembly>());

		// Assert -- no calls to single-assembly method
		A.CallTo(builder).MustNotHaveHappened();
	}

	[Fact]
	public void AcceptMultipleAssembliesWithoutThrowing()
	{
		// Arrange
		var builder = A.Fake<IDispatchBuilder>();

		// Act -- should not throw when passing multiple assemblies
		Should.NotThrow(() =>
			builder.AddHandlersFromAssembly(
				typeof(DispatchBuilderExtensionsParamsShould).Assembly,
				typeof(object).Assembly));
	}
}
