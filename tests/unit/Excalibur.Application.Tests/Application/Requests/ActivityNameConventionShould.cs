// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests.Commands;
using Excalibur.Application.Requests.Queries;
using Excalibur.Application.Requests.Jobs;
using Excalibur.Application.Requests.Notifications;

namespace Excalibur.Tests.Application.Requests;

/// <summary>
/// Tests for convention-based activity naming via <see cref="ActivityAttribute"/>
/// and the convention defaults derived from type names.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
[Trait("Feature", "ActivityNaming")]
public sealed class ActivityNameConventionShould : UnitTestBase
{
	private const string TestNamespace = "Excalibur.Tests.Application.Requests";

	#region ActivityName Tests

	[Fact]
	public void ResolveName_ForCommand_UsesNamespaceColonTypeName()
	{
		var command = new PlaceOrderCommand();
		command.ActivityName.ShouldBe($"{TestNamespace}:PlaceOrderCommand");
	}

	[Fact]
	public void ResolveName_ForQuery_UsesNamespaceColonTypeName()
	{
		var query = new GetOrderSummaryQuery();
		query.ActivityName.ShouldBe($"{TestNamespace}:GetOrderSummaryQuery");
	}

	[Fact]
	public void ResolveName_ForJob_UsesNamespaceColonTypeName()
	{
		var job = new ProcessOrderBatchJob();
		job.ActivityName.ShouldBe($"{TestNamespace}:ProcessOrderBatchJob");
	}

	[Fact]
	public void ResolveName_ForNotification_UsesNamespaceColonTypeName()
	{
		var notification = new OrderShippedNotification();
		notification.ActivityName.ShouldBe($"{TestNamespace}:OrderShippedNotification");
	}

	[Fact]
	public void ResolveName_IsConsistentAcrossAllBaseTypes()
	{
		var command = new PlaceOrderCommand();
		var query = new GetOrderSummaryQuery();
		var job = new ProcessOrderBatchJob();
		var notification = new OrderShippedNotification();

		// All should follow the same Namespace:TypeName pattern
		command.ActivityName.ShouldContain(":");
		query.ActivityName.ShouldContain(":");
		job.ActivityName.ShouldContain(":");
		notification.ActivityName.ShouldContain(":");
	}

	#endregion

	#region Convention Default Tests - Commands

	[Fact]
	public void ResolveDisplayName_ForCommand_IncludesNamespaceAndHumanizedName()
	{
		var command = new PlaceOrderCommand();
		command.ActivityDisplayName.ShouldBe($"{TestNamespace}: Place Order");
	}

	[Fact]
	public void ResolveDescription_ForCommand_MatchesDisplayName()
	{
		var command = new PlaceOrderCommand();
		command.ActivityDescription.ShouldBe(command.ActivityDisplayName);
	}

	[Fact]
	public void ResolveDisplayName_ForSingleWordCommand_IncludesNamespace()
	{
		var command = new PingCommand();
		command.ActivityDisplayName.ShouldBe($"{TestNamespace}: Ping");
	}

	#endregion

	#region Convention Default Tests - Queries

	[Fact]
	public void ResolveDisplayName_ForQuery_StripsQuerySuffix()
	{
		var query = new GetOrderSummaryQuery();
		query.ActivityDisplayName.ShouldBe($"{TestNamespace}: Get Order Summary");
	}

	#endregion

	#region Convention Default Tests - Jobs

	[Fact]
	public void ResolveDisplayName_ForJob_StripsJobSuffix()
	{
		var job = new ProcessOrderBatchJob();
		job.ActivityDisplayName.ShouldBe($"{TestNamespace}: Process Order Batch");
	}

	#endregion

	#region Convention Default Tests - Notifications

	[Fact]
	public void ResolveDisplayName_ForNotification_StripsNotificationSuffix()
	{
		var notification = new OrderShippedNotification();
		notification.ActivityDisplayName.ShouldBe($"{TestNamespace}: Order Shipped");
	}

	#endregion

	#region Attribute Override Tests

	[Fact]
	public void ResolveDisplayName_WithAttribute_UsesAttributeValue()
	{
		var command = new AttributeOnlyDisplayCommand();
		command.ActivityDisplayName.ShouldBe("Custom Display Name");
	}

	[Fact]
	public void ResolveDescription_WithAttributeDisplayOnly_UsesQualifiedConvention()
	{
		var command = new AttributeOnlyDisplayCommand();

		// Description uses convention: "{Namespace}: {AttributeDisplayName}"
		command.ActivityDescription.ShouldBe($"{TestNamespace}: Custom Display Name");
	}

	[Fact]
	public void ResolveDisplayName_WithFullAttribute_UsesAttributeDisplayName()
	{
		var command = new FullAttributeCommand();
		command.ActivityDisplayName.ShouldBe("Full Custom Name");
	}

	[Fact]
	public void ResolveDescription_WithFullAttribute_UsesAttributeDescription()
	{
		var command = new FullAttributeCommand();
		command.ActivityDescription.ShouldBe("A fully custom description.");
	}

	#endregion

	#region Override Still Works Tests

	[Fact]
	public void ResolveDisplayName_WithPropertyOverride_UsesOverrideValue()
	{
		var command = new OverrideCommand();
		command.ActivityDisplayName.ShouldBe("Overridden Display");
	}

	[Fact]
	public void ResolveDescription_WithPropertyOverride_UsesOverrideValue()
	{
		var command = new OverrideCommand();
		command.ActivityDescription.ShouldBe("Overridden description.");
	}

	#endregion

	#region Caching Tests

	[Fact]
	public void Resolve_SameType_ReturnsSameValues()
	{
		var cmd1 = new PlaceOrderCommand();
		var cmd2 = new PlaceOrderCommand();

		cmd1.ActivityDisplayName.ShouldBe(cmd2.ActivityDisplayName);
		cmd1.ActivityDescription.ShouldBe(cmd2.ActivityDescription);
	}

	#endregion

	#region ActivityAttribute Tests

	[Fact]
	public void ActivityAttribute_SingleParam_SetsDisplayNameOnly()
	{
		var attr = new ActivityAttribute("Test Name");
		attr.DisplayName.ShouldBe("Test Name");
		attr.Description.ShouldBeNull();
	}

	[Fact]
	public void ActivityAttribute_TwoParams_SetsBoth()
	{
		var attr = new ActivityAttribute("Test Name", "Test Desc");
		attr.DisplayName.ShouldBe("Test Name");
		attr.Description.ShouldBe("Test Desc");
	}

	[Fact]
	public void ActivityAttribute_IsNotInherited()
	{
		var usage = typeof(ActivityAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		usage.Inherited.ShouldBeFalse();
	}

	[Fact]
	public void ActivityAttribute_DisallowsMultiple()
	{
		var usage = typeof(ActivityAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		usage.AllowMultiple.ShouldBeFalse();
	}

	#endregion

	#region Test Types

	private sealed class PlaceOrderCommand : CommandBase
	{
		public Guid OrderId { get; init; }
	}

	private sealed class PingCommand : CommandBase
	{
	}

	private sealed class GetOrderSummaryQuery : QueryBase<string>
	{
	}

	private sealed class ProcessOrderBatchJob : JobBase
	{
	}

	private sealed class OrderShippedNotification : NotificationBase
	{
	}

	[Activity("Custom Display Name")]
	private sealed class AttributeOnlyDisplayCommand : CommandBase
	{
	}

	[Activity("Full Custom Name", "A fully custom description.")]
	private sealed class FullAttributeCommand : CommandBase
	{
	}

	private sealed class OverrideCommand : CommandBase
	{
		public override string ActivityDisplayName => "Overridden Display";
		public override string ActivityDescription => "Overridden description.";
	}

	#endregion
}
