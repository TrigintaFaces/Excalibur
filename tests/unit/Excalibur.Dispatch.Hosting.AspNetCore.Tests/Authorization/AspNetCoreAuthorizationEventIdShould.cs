// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.Authorization;

/// <summary>
/// Tests for AspNetCoreAuthorizationEventId constants.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AspNetCoreAuthorizationEventIdShould : UnitTestBase
{
	[Fact]
	public void HaveAllEventIdsInHostingRange()
	{
		// Arrange — Event IDs should be in the Excalibur.Dispatch.Hosting.* range (2600-2699)
		var eventIdType = typeof(Excalibur.Dispatch.Hosting.AspNetCore.AspNetCoreAuthorizationMiddleware)
			.Assembly
			.GetType("Excalibur.Dispatch.Hosting.AspNetCore.AspNetCoreAuthorizationEventId");

		eventIdType.ShouldNotBeNull("AspNetCoreAuthorizationEventId type should exist");

		// Act
		var fields = eventIdType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			.Where(f => f.FieldType == typeof(int))
			.ToList();

		// Assert
		fields.ShouldNotBeEmpty();
		foreach (var field in fields)
		{
			var value = (int)field.GetValue(null)!;
			value.ShouldBeGreaterThanOrEqualTo(2600, $"{field.Name} should be >= 2600");
			value.ShouldBeLessThanOrEqualTo(2699, $"{field.Name} should be <= 2699");
		}
	}

	[Fact]
	public void HaveUniqueEventIds()
	{
		// Arrange
		var eventIdType = typeof(Excalibur.Dispatch.Hosting.AspNetCore.AspNetCoreAuthorizationMiddleware)
			.Assembly
			.GetType("Excalibur.Dispatch.Hosting.AspNetCore.AspNetCoreAuthorizationEventId");

		eventIdType.ShouldNotBeNull();

		// Act
		var fields = eventIdType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			.Where(f => f.FieldType == typeof(int))
			.ToList();

		var values = fields.Select(f => (int)f.GetValue(null)!).ToList();

		// Assert — all event IDs must be unique
		values.Distinct().Count().ShouldBe(values.Count, "All event IDs must be unique");
	}

	[Fact]
	public void HaveExpectedEventIdConstants()
	{
		// Arrange
		var eventIdType = typeof(Excalibur.Dispatch.Hosting.AspNetCore.AspNetCoreAuthorizationMiddleware)
			.Assembly
			.GetType("Excalibur.Dispatch.Hosting.AspNetCore.AspNetCoreAuthorizationEventId");

		eventIdType.ShouldNotBeNull();

		// Act
		var fieldNames = eventIdType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			.Where(f => f.FieldType == typeof(int))
			.Select(f => f.Name)
			.ToHashSet();

		// Assert — verify expected fields exist
		fieldNames.ShouldContain("AuthorizationExecuting");
		fieldNames.ShouldContain("AuthorizationGranted");
		fieldNames.ShouldContain("AuthorizationDenied");
		fieldNames.ShouldContain("AuthorizationSkipped");
		fieldNames.ShouldContain("AllowAnonymousApplied");
		fieldNames.ShouldContain("AttributeCacheHit");
		fieldNames.ShouldContain("AuthorizationError");
	}
}
