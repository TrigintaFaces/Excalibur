// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Delivery;

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
