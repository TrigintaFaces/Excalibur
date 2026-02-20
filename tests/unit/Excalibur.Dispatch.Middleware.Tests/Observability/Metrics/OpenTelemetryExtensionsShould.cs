// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Metrics;

/// <summary>
/// Unit tests for <see cref="OpenTelemetryExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class OpenTelemetryExtensionsShould : UnitTestBase
{
	#region AddDispatchMetrics (IOpenTelemetryBuilder) Tests

	[Fact]
	public void AddDispatchMetrics_IOpenTelemetryBuilder_ThrowOnNullBuilder()
	{
		// Arrange
		IOpenTelemetryBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddDispatchMetrics());
	}

	#endregion

	#region AddDispatchMetrics (MeterProviderBuilder) Tests

	[Fact]
	public void AddDispatchMetrics_MeterProviderBuilder_ThrowOnNullBuilder()
	{
		// Arrange
		MeterProviderBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddDispatchMetrics());
	}

	#endregion

	#region AddTransportMetrics (IOpenTelemetryBuilder) Tests

	[Fact]
	public void AddTransportMetrics_IOpenTelemetryBuilder_ThrowOnNullBuilder()
	{
		// Arrange
		IOpenTelemetryBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddTransportMetrics());
	}

	#endregion

	#region AddTransportMetrics (MeterProviderBuilder) Tests

	[Fact]
	public void AddTransportMetrics_MeterProviderBuilder_ThrowOnNullBuilder()
	{
		// Arrange
		MeterProviderBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddTransportMetrics());
	}

	#endregion

	#region AddAllDispatchMetrics (IOpenTelemetryBuilder) Tests

	[Fact]
	public void AddAllDispatchMetrics_IOpenTelemetryBuilder_ThrowOnNullBuilder()
	{
		// Arrange
		IOpenTelemetryBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddAllDispatchMetrics());
	}

	#endregion

	#region AddAllDispatchMetrics (MeterProviderBuilder) Tests

	[Fact]
	public void AddAllDispatchMetrics_MeterProviderBuilder_ThrowOnNullBuilder()
	{
		// Arrange
		MeterProviderBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddAllDispatchMetrics());
	}

	#endregion
}
