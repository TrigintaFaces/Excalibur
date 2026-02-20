// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json;

namespace Excalibur.Hosting.Tests.HealthChecks;

/// <summary>
/// Tests for the internal HealthCheckJsonSerializerOptions class via reflection.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class HealthCheckJsonSerializerOptionsShould : UnitTestBase
{
	private static readonly Type OptionsType = typeof(Excalibur.Hosting.HealthChecksBuilderExtensions).Assembly
		.GetType("Excalibur.Hosting.HealthChecks.HealthCheckJsonSerializerOptions")!;

	[Fact]
	public void ReturnDefaultOptions()
	{
		// Act
		var defaultProp = OptionsType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
		defaultProp.ShouldNotBeNull();
		var options = defaultProp.GetValue(null) as JsonSerializerOptions;

		// Assert
		options.ShouldNotBeNull();
		options.Converters.ShouldNotBeEmpty();
	}

	[Fact]
	public void ReturnSameInstanceOnSubsequentCalls()
	{
		// Act
		var defaultProp = OptionsType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!;
		var first = defaultProp.GetValue(null);
		var second = defaultProp.GetValue(null);

		// Assert
		first.ShouldBeSameAs(second);
	}
}
