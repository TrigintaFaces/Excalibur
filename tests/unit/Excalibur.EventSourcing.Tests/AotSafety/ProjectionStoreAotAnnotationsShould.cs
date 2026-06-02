// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing.Tests.AotSafety;

/// <summary>
/// Verifies AOT trimmer annotations on projection store type parameters (bd-yd29oo).
/// Ensures <c>[DynamicallyAccessedMembers]</c> attributes preserve required members
/// for JSON serialization in AOT/trimmed scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionStoreAotAnnotationsShould
{
	// ═══════════════════════════════════════════════════
	// CosmosDB — DynamicallyAccessedMembers on TProjection
	// ═══════════════════════════════════════════════════

	[Fact]
	public void CosmosDbProjectionStore_HasDynamicallyAccessedMembersOnTProjection()
	{
		// Arrange
		var storeType = typeof(Excalibur.Data.CosmosDb.Projections.CosmosDbProjectionStore<>);
		var typeParam = storeType.GetGenericArguments()[0];

		// Act
		var attrs = typeParam.GetCustomAttributes(typeof(DynamicallyAccessedMembersAttribute), false);

		// Assert
		attrs.ShouldNotBeEmpty(
			"CosmosDbProjectionStore<TProjection> must have [DynamicallyAccessedMembers] on TProjection");

		var attr = (DynamicallyAccessedMembersAttribute)attrs[0];
		(attr.MemberTypes & DynamicallyAccessedMemberTypes.PublicProperties).ShouldNotBe(
			(DynamicallyAccessedMemberTypes)0,
			"Must preserve PublicProperties for JSON serialization");
	}

	// ═══════════════════════════════════════════════════
	// DynamoDB — DynamicallyAccessedMembers on TProjection
	// ═══════════════════════════════════════════════════

	[Fact]
	public void DynamoDbProjectionStore_HasDynamicallyAccessedMembersOnTProjection()
	{
		// Arrange
		var storeType = typeof(Excalibur.Data.DynamoDb.Projections.DynamoDbProjectionStore<>);
		var typeParam = storeType.GetGenericArguments()[0];

		// Act
		var attrs = typeParam.GetCustomAttributes(typeof(DynamicallyAccessedMembersAttribute), false);

		// Assert
		attrs.ShouldNotBeEmpty(
			"DynamoDbProjectionStore<TProjection> must have [DynamicallyAccessedMembers] on TProjection");

		var attr = (DynamicallyAccessedMembersAttribute)attrs[0];
		(attr.MemberTypes & DynamicallyAccessedMemberTypes.PublicProperties).ShouldNotBe(
			(DynamicallyAccessedMemberTypes)0,
			"Must preserve PublicProperties for JSON serialization");
	}

	// ═══════════════════════════════════════════════════
	// SqlServer — No DynamicallyAccessedMembers needed (Dapper is reflection-based)
	// ═══════════════════════════════════════════════════

	[Fact]
	public void SqlServerProjectionStore_DoesNotRequireDynamicallyAccessedMembers()
	{
		// Arrange — SqlServer uses Dapper (inherently reflection-based), file-level pragma is sufficient
		var storeType = typeof(Excalibur.EventSourcing.SqlServer.SqlServerProjectionStore<>);
		var typeParam = storeType.GetGenericArguments()[0];

		// Act
		var attrs = typeParam.GetCustomAttributes(typeof(DynamicallyAccessedMembersAttribute), false);

		// Assert — no attribute needed (Dapper handles its own reflection)
		attrs.ShouldBeEmpty(
			"SqlServerProjectionStore uses Dapper (reflection-based) — no [DynamicallyAccessedMembers] needed");
	}

	// ═══════════════════════════════════════════════════
	// ElasticSearch builder extensions — DynamicallyAccessedMembers on TProjection
	// ═══════════════════════════════════════════════════

	[Fact]
	public void ElasticSearchProjectionStore_HasDynamicallyAccessedMembersOnTProjection()
	{
		// Arrange
		var storeType = typeof(Excalibur.Data.ElasticSearch.Projections.ElasticSearchProjectionStore<>);
		var typeParam = storeType.GetGenericArguments()[0];

		// Act
		var attrs = typeParam.GetCustomAttributes(typeof(DynamicallyAccessedMembersAttribute), false);

		// Assert — ES uses reflection for property mapping during index creation
		attrs.ShouldNotBeEmpty(
			"ElasticSearchProjectionStore<TProjection> must have [DynamicallyAccessedMembers] on TProjection");
	}
}
