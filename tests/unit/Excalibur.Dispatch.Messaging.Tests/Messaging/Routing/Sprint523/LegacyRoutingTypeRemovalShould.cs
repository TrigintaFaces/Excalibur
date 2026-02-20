// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Sprint523;

/// <summary>
/// Verifies that legacy routing types from Abstractions have been removed (S523.4/S523.5)
/// and that the new two-tier routing types are intact.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LegacyRoutingTypeRemovalShould
{
	private static readonly Assembly AbstractionsAssembly =
		typeof(Dispatch.Abstractions.Routing.RoutingContext).Assembly;

	private static readonly Assembly DispatchAssembly =
		typeof(Dispatch.Metrics.MetricRegistry).Assembly;

	#region Tier 1 — Deleted Legacy Types (Abstractions)

	[Theory]
	[InlineData("IRoutingConfiguration")]
	[InlineData("IRoutingStrategy")]
	[InlineData("RoutingRuleType")]
	[InlineData("IRoutingRule")]
	[InlineData("RoutingRule")]
	[InlineData("RoutingHints")]
	public void NotContainDeletedTier1Types_InAbstractions(string typeName)
	{
		// Assert - these legacy types should no longer exist in Abstractions assembly
		var types = AbstractionsAssembly.GetTypes()
			.Where(t => t.Name == typeName && t.Namespace?.Contains("Routing") == true)
			.ToList();

		types.ShouldBeEmpty(
			$"Legacy type '{typeName}' should have been deleted from Abstractions in S523.4");
	}

	#endregion

	#region Tier 2 — Deleted Legacy Types (Abstractions)

	[Theory]
	[InlineData("RoutingTable")]
	[InlineData("RoutingEntry")]
	public void NotContainDeletedTier2Types_InAbstractions(string typeName)
	{
		// Assert
		var types = AbstractionsAssembly.GetTypes()
			.Where(t => t.Name == typeName && t.Namespace?.Contains("Routing") == true)
			.ToList();

		types.ShouldBeEmpty(
			$"Legacy type '{typeName}' should have been deleted from Abstractions in S523.4");
	}

	#endregion

	#region Moved Types — Now Internal in Dispatch

	[Theory]
	[InlineData("IRouteResult")]
	[InlineData("RouteResult")]
	[InlineData("RouteDeliveryStatus")]
	[InlineData("RouteFailure")]
	[InlineData("IRouteMetadata")]
	public void NotExposeMovedTypes_AsPublic_InAbstractions(string typeName)
	{
		// Assert - moved types should NOT exist in Abstractions anymore
		var types = AbstractionsAssembly.GetTypes()
			.Where(t => t.Name == typeName && t.Namespace?.Contains("Routing") == true)
			.ToList();

		types.ShouldBeEmpty(
			$"Type '{typeName}' was moved to Dispatch in S523.4 — should not exist in Abstractions");
	}

	[Theory]
	[InlineData("IRouteResult")]
	[InlineData("RouteResult")]
	[InlineData("RouteDeliveryStatus")]
	[InlineData("RouteFailure")]
	[InlineData("IRouteMetadata")]
	public void ContainMovedTypes_InDispatch(string typeName)
	{
		// Assert - moved types should exist in Dispatch assembly
		// Use safe loading to avoid ReflectionTypeLoadException from StructLayout generic types
		var types = SafeGetTypes(DispatchAssembly)
			.Where(t => t.Name == typeName && t.Namespace?.Contains("Routing") == true)
			.ToList();

		types.ShouldNotBeEmpty(
			$"Type '{typeName}' should exist in Dispatch after move from Abstractions in S523.4");
	}

	#endregion

	#region Tier 3 — Kept Types (Abstractions)

	[Theory]
	[InlineData("RoutingContext")]
	[InlineData("RouteDefinition")]
	[InlineData("IRoutingContext")]
	public void StillContainKeptTier3Types_InAbstractions(string typeName)
	{
		// Assert - Tier 3 types should still be present (needed by load balancers)
		var types = AbstractionsAssembly.GetTypes()
			.Where(t => t.Name == typeName && t.Namespace?.Contains("Routing") == true)
			.ToList();

		types.ShouldNotBeEmpty(
			$"Tier 3 type '{typeName}' should be preserved per AD-523.1 decision");
	}

	#endregion

	#region New Two-Tier System — Present

	[Theory]
	[InlineData("IDispatchRouter")]
	[InlineData("ITransportSelector")]
	[InlineData("IEndpointRouter")]
	[InlineData("RoutingDecision")]
	[InlineData("RouteInfo")]
	public void ContainNewTwoTierRoutingTypes_InAbstractions(string typeName)
	{
		// Assert - Sprint 520 two-tier routing types should still be present
		var types = AbstractionsAssembly.GetTypes()
			.Where(t => t.Name == typeName && t.Namespace?.Contains("Routing") == true)
			.ToList();

		types.ShouldNotBeEmpty(
			$"New two-tier routing type '{typeName}' should be present in Abstractions");
	}

	#endregion

	#region RoutingContext — Hints Property Removed

	[Fact]
	public void RoutingContext_ShouldNotHaveHintsProperty()
	{
		// Assert - Hints property was removed in S523.5
		var hintsProperty = typeof(Dispatch.Abstractions.Routing.RoutingContext)
			.GetProperty("Hints");

		hintsProperty.ShouldBeNull(
			"RoutingContext.Hints should have been removed in S523.5 legacy cleanup");
	}

	[Fact]
	public void RoutingContext_ShouldStillHavePropertiesCollection()
	{
		// Assert - Properties collection should still exist as the modern alternative
		var propertiesProperty = typeof(Dispatch.Abstractions.Routing.RoutingContext)
			.GetProperty("Properties");

		propertiesProperty.ShouldNotBeNull(
			"RoutingContext.Properties should still be available as the routing data store");
	}

	#endregion

	private static Type[] SafeGetTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(t => t != null).ToArray()!;
		}
	}
}
