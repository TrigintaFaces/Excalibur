// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Middleware.Tests.Configuration;

/// <summary>
/// Unit tests for MiddlewarePresetExtensions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MiddlewarePresetExtensionsShould : UnitTestBase
{
	[Fact]
	public void UseDevelopmentMiddleware_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseDevelopmentMiddleware());
	}

	[Fact]
	public void UseProductionMiddleware_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseProductionMiddleware());
	}

	[Fact]
	public void UseFullMiddleware_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseFullMiddleware());
	}
}
