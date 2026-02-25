// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Patterns;

namespace Excalibur.Dispatch.Patterns.Tests.Hosting.Json;

/// <summary>
/// Depth coverage tests for <see cref="DispatchPatternsJsonOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchPatternsJsonOptionsDepthShould
{
	[Fact]
	public void Constructor_SetsDefaultSerializerOptions()
	{
		var options = new DispatchPatternsJsonOptions();
		options.SerializerOptions.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_UsesWebDefaults()
	{
		var options = new DispatchPatternsJsonOptions();
		// Web defaults = camelCase property naming + case-insensitive
		options.SerializerOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
		options.SerializerOptions.PropertyNameCaseInsensitive.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_DisablesWriteIndented()
	{
		var options = new DispatchPatternsJsonOptions();
		options.SerializerOptions.WriteIndented.ShouldBeFalse();
	}

	[Fact]
	public void SerializerContext_DefaultsToNull()
	{
		var options = new DispatchPatternsJsonOptions();
		options.SerializerContext.ShouldBeNull();
	}

	[Fact]
	public void SerializerContext_CanBeSet()
	{
		var options = new DispatchPatternsJsonOptions();
		// We just verify the property is settable (null assignment)
		options.SerializerContext = null;
		options.SerializerContext.ShouldBeNull();
	}

	[Fact]
	public void SerializerOptions_IsNotShared_BetweenInstances()
	{
		var options1 = new DispatchPatternsJsonOptions();
		var options2 = new DispatchPatternsJsonOptions();
		options1.SerializerOptions.ShouldNotBeSameAs(options2.SerializerOptions);
	}

	[Fact]
	public void SerializerOptions_CanBeCustomized()
	{
		var options = new DispatchPatternsJsonOptions();
		options.SerializerOptions.WriteIndented = true;
		options.SerializerOptions.WriteIndented.ShouldBeTrue();
	}
}
