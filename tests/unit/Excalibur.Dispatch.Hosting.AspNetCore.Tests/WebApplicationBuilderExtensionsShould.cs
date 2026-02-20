// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.AspNetCore.Builder;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Tests for <see cref="WebApplicationBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class WebApplicationBuilderExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddDispatch_ThrowWhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((WebApplicationBuilder)null!).AddDispatch());
	}

	[Fact]
	public void AddDispatch_ReturnSameBuilder()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();

		// Act
		var result = builder.AddDispatch();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddDispatch_AcceptNullConfigure()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();

		// Act & Assert — should not throw
		var result = builder.AddDispatch(null);

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddDispatch_InvokeConfigureAction()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		var configureCalled = false;

		// Act
		builder.AddDispatch(b =>
		{
			configureCalled = true;
			b.ShouldNotBeNull();
		});

		// Assert
		configureCalled.ShouldBeTrue();
	}
}
