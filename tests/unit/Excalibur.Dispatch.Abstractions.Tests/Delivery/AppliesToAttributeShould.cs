// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Excalibur.Dispatch.Abstractions.Tests.Delivery;

/// <summary>
/// Unit tests for the <see cref="AppliesToAttribute"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class AppliesToAttributeShould
{
	[Fact]
	public void Constructor_Should_StoreMessageKinds()
	{
		// Act
		var attr = new AppliesToAttribute(MessageKinds.Action);

		// Assert
		attr.MessageKinds.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public void Constructor_Should_SupportCombinedFlags()
	{
		// Act
		var attr = new AppliesToAttribute(MessageKinds.Action | MessageKinds.Event);

		// Assert
		attr.MessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
	}

	[Fact]
	public void Should_BeApplicableOnlyToClasses()
	{
		// Act
		var usage = typeof(AppliesToAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		// Assert
		usage.ValidOn.ShouldBe(AttributeTargets.Class);
	}
}
