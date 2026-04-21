// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.Projections;

namespace Excalibur.Caching.Tests.Caching.Projections;

/// <summary>
/// Tests for <see cref="ProjectionResolverTypeRegistry"/> covering registration, lookup,
/// freeze behavior, and edge cases.
/// Sprint 739 B.5: Wave 4 AOT-safe dispatch path tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "AOT")]
public sealed class ProjectionResolverTypeRegistryShould : IDisposable
{
	public ProjectionResolverTypeRegistryShould()
	{
		ProjectionResolverTypeRegistry.Clear();
	}

	public void Dispose()
	{
		ProjectionResolverTypeRegistry.Clear();
	}

	#region GetResolverType Tests

	[Fact]
	public void ReturnNullForUnregisteredType()
	{
		var result = ProjectionResolverTypeRegistry.GetResolverType(typeof(TestProjectionMessage));

		result.ShouldBeNull();
	}

	[Fact]
	public void ReturnResolverTypeForRegisteredMessage()
	{
		ProjectionResolverTypeRegistry.Register<TestProjectionMessage>();

		var result = ProjectionResolverTypeRegistry.GetResolverType(typeof(TestProjectionMessage));

		result.ShouldNotBeNull();
		result.ShouldBe(typeof(IProjectionTagResolver<TestProjectionMessage>));
	}

	[Fact]
	public void ReturnCorrectClosedGenericType()
	{
		ProjectionResolverTypeRegistry.Register<AnotherProjectionMessage>();

		var result = ProjectionResolverTypeRegistry.GetResolverType(typeof(AnotherProjectionMessage));

		result.ShouldBe(typeof(IProjectionTagResolver<AnotherProjectionMessage>));
		result!.IsGenericType.ShouldBeTrue();
		result.GetGenericTypeDefinition().ShouldBe(typeof(IProjectionTagResolver<>));
	}

	[Fact]
	public void DistinguishDifferentMessageTypes()
	{
		ProjectionResolverTypeRegistry.Register<TestProjectionMessage>();
		ProjectionResolverTypeRegistry.Register<AnotherProjectionMessage>();

		var result1 = ProjectionResolverTypeRegistry.GetResolverType(typeof(TestProjectionMessage));
		var result2 = ProjectionResolverTypeRegistry.GetResolverType(typeof(AnotherProjectionMessage));

		result1.ShouldBe(typeof(IProjectionTagResolver<TestProjectionMessage>));
		result2.ShouldBe(typeof(IProjectionTagResolver<AnotherProjectionMessage>));
		result1.ShouldNotBe(result2);
	}

	[Fact]
	public void ReturnNullForUnrelatedType()
	{
		ProjectionResolverTypeRegistry.Register<TestProjectionMessage>();

		var result = ProjectionResolverTypeRegistry.GetResolverType(typeof(AnotherProjectionMessage));

		result.ShouldBeNull();
	}

	#endregion

	#region Freeze Tests

	[Fact]
	public void ThrowWhenRegisteringAfterFreeze()
	{
		ProjectionResolverTypeRegistry.Freeze();

		Should.Throw<InvalidOperationException>(
			() => ProjectionResolverTypeRegistry.Register<TestProjectionMessage>());
	}

	[Fact]
	public void AllowLookupAfterFreeze()
	{
		ProjectionResolverTypeRegistry.Register<TestProjectionMessage>();
		ProjectionResolverTypeRegistry.Freeze();

		var result = ProjectionResolverTypeRegistry.GetResolverType(typeof(TestProjectionMessage));

		result.ShouldBe(typeof(IProjectionTagResolver<TestProjectionMessage>));
	}

	[Fact]
	public void AllowDoubleFreezeWithoutThrow()
	{
		ProjectionResolverTypeRegistry.Freeze();
		ProjectionResolverTypeRegistry.Freeze(); // Should not throw
	}

	#endregion

	#region Clear Tests

	[Fact]
	public void ClearRemovesAllRegistrations()
	{
		ProjectionResolverTypeRegistry.Register<TestProjectionMessage>();
		ProjectionResolverTypeRegistry.Clear();

		var result = ProjectionResolverTypeRegistry.GetResolverType(typeof(TestProjectionMessage));

		result.ShouldBeNull();
	}

	[Fact]
	public void ClearResetsFrozenState()
	{
		ProjectionResolverTypeRegistry.Freeze();
		ProjectionResolverTypeRegistry.Clear();

		// Should not throw after clear resets frozen state
		ProjectionResolverTypeRegistry.Register<TestProjectionMessage>();
	}

	#endregion

	#region Overwrite Tests

	[Fact]
	public void AllowOverwriteOfExistingRegistration()
	{
		ProjectionResolverTypeRegistry.Register<TestProjectionMessage>();
		ProjectionResolverTypeRegistry.Register<TestProjectionMessage>(); // Should not throw

		var result = ProjectionResolverTypeRegistry.GetResolverType(typeof(TestProjectionMessage));

		result.ShouldBe(typeof(IProjectionTagResolver<TestProjectionMessage>));
	}

	#endregion

	#region Test Fixtures

	internal sealed class TestProjectionMessage
	{
		public string EntityId { get; set; } = string.Empty;
	}

	internal sealed class AnotherProjectionMessage
	{
		public string Category { get; set; } = string.Empty;
	}

	#endregion
}
