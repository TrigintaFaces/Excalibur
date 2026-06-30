// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Middleware.Tests.Observability;

/// <summary>
/// Unit tests for <see cref="DispatchBuilderObservabilityExtensions"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class DispatchBuilderObservabilityExtensionsShould : UnitTestBase
{
	private static IDispatchBuilder CreateBuilder()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		return builder;
	}

	#region UseObservability Tests

	[Fact]
	public void UseObservability_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseObservability());
	}

	[Fact]
	public void UseObservability_ReturnsBuilder()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseObservability();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseTracing Tests

	[Fact]
	public void UseTracing_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseTracing());
	}

	#endregion

	#region UseMetrics Tests

	[Fact]
	public void UseMetrics_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseMetrics());
	}

	#endregion

	#region UseOpenTelemetry Tests

	[Fact]
	public void UseOpenTelemetry_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseOpenTelemetry());
	}

	#endregion

	#region UseW3CTraceContext Tests

	[Fact]
	public void UseW3CTraceContext_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseW3CTraceContext());
	}

	#endregion

	#region UseTraceSampling Tests

	[Fact]
	public void UseTraceSampling_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseTraceSampling());
	}

	#endregion
}
