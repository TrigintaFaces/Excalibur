// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Reflection;

using Excalibur.Dispatch.AuditLogging;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

/// <summary>
/// Additional coverage tests to push Excalibur.Dispatch.AuditLogging from 93.9% to 95%+.
/// Targets specific uncovered branches:
/// - Resources.Designer.cs (Culture setter, constructor, ResourceManager initialization)
/// - RbacAuditStore.CanAccessEvent fallback return false (when role is below SecurityAnalyst)
/// - InMemoryAuditStore edge cases
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[UnitTest]
public sealed class AuditLoggingAdditionalCoverageShould
{
	#region Resources.Designer.cs Coverage

	[Fact]
	public void Resources_Culture_SetterSetsValue()
	{
		// Arrange - get the Resources type via reflection (internal class)
		var resourcesType = typeof(DefaultAuditLogger).Assembly
			.GetType("Excalibur.Dispatch.AuditLogging.Resources", throwOnError: true);

		// Get the Culture property
		var cultureProperty = resourcesType.GetProperty(
			"Culture",
			BindingFlags.Static | BindingFlags.NonPublic);
		cultureProperty.ShouldNotBeNull();

		// Store original value
		var originalCulture = (CultureInfo?)cultureProperty.GetValue(null);

		try
		{
			// Act - set Culture to a specific value
			var testCulture = new CultureInfo("fr-FR");
			cultureProperty.SetValue(null, testCulture);

			// Assert - getter should return the set value
			var retrievedCulture = (CultureInfo?)cultureProperty.GetValue(null);
			retrievedCulture.ShouldBe(testCulture);

			// Set to null
			cultureProperty.SetValue(null, null);
			var nullCulture = (CultureInfo?)cultureProperty.GetValue(null);
			nullCulture.ShouldBeNull();
		}
		finally
		{
			// Restore original value
			cultureProperty.SetValue(null, originalCulture);
		}
	}

	[Fact]
	public void Resources_Constructor_CanBeInvoked()
	{
		// Arrange - get the Resources type via reflection
		var resourcesType = typeof(DefaultAuditLogger).Assembly
			.GetType("Excalibur.Dispatch.AuditLogging.Resources", throwOnError: true);

		// Act - invoke the internal constructor
		var constructor = resourcesType.GetConstructor(
			BindingFlags.Instance | BindingFlags.NonPublic,
			Type.EmptyTypes);
		constructor.ShouldNotBeNull();

		var instance = constructor.Invoke(null);

		// Assert - instance should be created
		instance.ShouldNotBeNull();
	}

	[Fact]
	public void Resources_ResourceManager_ReturnsNonNull()
	{
		// Arrange - get the Resources type via reflection
		var resourcesType = typeof(DefaultAuditLogger).Assembly
			.GetType("Excalibur.Dispatch.AuditLogging.Resources", throwOnError: true);

		// Get the ResourceManager property
		var resourceManagerProperty = resourcesType.GetProperty(
			"ResourceManager",
			BindingFlags.Static | BindingFlags.NonPublic);
		resourceManagerProperty.ShouldNotBeNull();

		// Act
		var resourceManager = resourceManagerProperty.GetValue(null);

		// Assert
		resourceManager.ShouldNotBeNull();
		resourceManager.ShouldBeOfType<System.Resources.ResourceManager>();
	}

	[Fact]
	public void Resources_AllResourceStrings_CanBeRetrieved()
	{
		// Arrange - get the Resources type via reflection
		var resourcesType = typeof(DefaultAuditLogger).Assembly
			.GetType("Excalibur.Dispatch.AuditLogging.Resources", throwOnError: true);

		// Get all string properties
		var stringProperties = resourcesType.GetProperties(BindingFlags.Static | BindingFlags.NonPublic)
			.Where(p => p.PropertyType == typeof(string) && p.Name != "ResourceManager")
			.ToList();

		stringProperties.Count.ShouldBeGreaterThan(0);

		// Act & Assert - each property should return a non-null string
		foreach (var prop in stringProperties)
		{
			var value = (string?)prop.GetValue(null);
			value.ShouldNotBeNullOrWhiteSpace($"Resource property {prop.Name} should have a value");
		}
	}

	#endregion

	#region RbacAuditStore - CanAccessEvent Returns False Edge Cases

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_ReturnsNull_WhenNoneRoleTriesToAccessEvent_AfterEnsureReadAccessThrows()
	{
		// Note: This test verifies the EnsureReadAccess path that throws for None role.
		// The CanAccessEvent fallback "return false" is unreachable when role is None/Developer
		// because EnsureReadAccess throws first. This is by design.
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.None));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		// Act & Assert - None role should throw UnauthorizedAccessException via EnsureReadAccess
		await Should.ThrowAsync<UnauthorizedAccessException>(
			() => sut.GetByIdAsync("any-event", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_ReturnsNull_WhenSecurityAnalystAccessesConfigurationChangeEvent()
	{
		// Arrange - SecurityAnalyst cannot access ConfigurationChange event type
		// This exercises the CanAccessEvent "return false" path for SecurityAnalyst
		// accessing a non-security event type
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		// A ConfigurationChange event that SecurityAnalyst cannot access
		var configEvent = new AuditEvent
		{
			EventId = "config-evt-1",
			EventType = AuditEventType.ConfigurationChange,
			Action = "SettingsChange",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "admin"
		};

		A.CallTo(() => innerStore.GetByIdAsync("config-evt-1", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(configEvent));

		// Act
		var result = await sut.GetByIdAsync("config-evt-1", CancellationToken.None).ConfigureAwait(false);

		// Assert - filtered out, returns null
		result.ShouldBeNull();
	}

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_AllowsSecurityAnalyst_ForSecurityEvent()
	{
		// Arrange - verify SecurityAnalyst CAN access Security event type
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		var securityEvent = new AuditEvent
		{
			EventId = "sec-evt-1",
			EventType = AuditEventType.Security,
			Action = "ThreatDetected",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "security-system"
		};

		A.CallTo(() => innerStore.GetByIdAsync("sec-evt-1", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(securityEvent));

		// Act
		var result = await sut.GetByIdAsync("sec-evt-1", CancellationToken.None).ConfigureAwait(false);

		// Assert - Security event should be accessible
		result.ShouldNotBeNull();
		result.EventType.ShouldBe(AuditEventType.Security);
	}

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_AllowsSecurityAnalyst_ForAuthenticationEvent()
	{
		// Arrange
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		var authEvent = new AuditEvent
		{
			EventId = "auth-evt-1",
			EventType = AuditEventType.Authentication,
			Action = "Login",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		A.CallTo(() => innerStore.GetByIdAsync("auth-evt-1", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(authEvent));

		// Act
		var result = await sut.GetByIdAsync("auth-evt-1", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.EventType.ShouldBe(AuditEventType.Authentication);
	}

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_ReturnsNull_WhenSecurityAnalystAccessesAdministrativeEvent()
	{
		// Arrange - SecurityAnalyst cannot access Administrative event type
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		var adminEvent = new AuditEvent
		{
			EventId = "admin-evt-1",
			EventType = AuditEventType.Administrative,
			Action = "UserCreation",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "admin"
		};

		A.CallTo(() => innerStore.GetByIdAsync("admin-evt-1", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(adminEvent));

		// Act
		var result = await sut.GetByIdAsync("admin-evt-1", CancellationToken.None).ConfigureAwait(false);

		// Assert - filtered out, returns null
		result.ShouldBeNull();
	}

	[Fact]
	public async Task RbacAuditStore_GetByIdAsync_ReturnsNull_WhenSecurityAnalystAccessesIntegrationEvent()
	{
		// Arrange - SecurityAnalyst cannot access Integration event type
		var innerStore = A.Fake<IAuditStore>();
		var roleProvider = A.Fake<IAuditRoleProvider>();
		A.CallTo(() => roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(AuditLogRole.SecurityAnalyst));

		var logger = new NullLogger<RbacAuditStore>();
		var sut = new RbacAuditStore(innerStore, roleProvider, logger);

		var integrationEvent = new AuditEvent
		{
			EventId = "integ-evt-1",
			EventType = AuditEventType.Integration,
			Action = "ApiCall",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "service"
		};

		A.CallTo(() => innerStore.GetByIdAsync("integ-evt-1", A<CancellationToken>._))
			.Returns(Task.FromResult<AuditEvent?>(integrationEvent));

		// Act
		var result = await sut.GetByIdAsync("integ-evt-1", CancellationToken.None).ConfigureAwait(false);

		// Assert - filtered out, returns null
		result.ShouldBeNull();
	}

	#endregion

	#region InMemoryAuditStore - Additional Edge Cases

	[Fact]
	public async Task InMemoryAuditStore_QueryAsync_WithDescendingOrder_NoTenant_ReturnsCorrectOrder()
	{
		// Arrange - test descending order without tenant filter (uses _eventsById.Values path)
		var store = new InMemoryAuditStore();
		var ts1 = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
		var ts2 = new DateTimeOffset(2025, 6, 20, 0, 0, 0, TimeSpan.Zero);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "desc-no-tenant-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = ts1,
			ActorId = "user-1"
		}, CancellationToken.None).ConfigureAwait(false);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "desc-no-tenant-2",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = ts2,
			ActorId = "user-1"
		}, CancellationToken.None).ConfigureAwait(false);

		// Act - descending order, no tenant
		var results = await store.QueryAsync(new AuditQuery
		{
			OrderByDescending = true
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert - newer first
		results.Count.ShouldBe(2);
		results[0].EventId.ShouldBe("desc-no-tenant-2");
		results[1].EventId.ShouldBe("desc-no-tenant-1");
	}

	[Fact]
	public async Task InMemoryAuditStore_QueryAsync_WithAscendingOrder_NoTenant_ReturnsCorrectOrder()
	{
		// Arrange
		var store = new InMemoryAuditStore();
		var ts1 = new DateTimeOffset(2025, 6, 10, 0, 0, 0, TimeSpan.Zero);
		var ts2 = new DateTimeOffset(2025, 6, 20, 0, 0, 0, TimeSpan.Zero);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "asc-no-tenant-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = ts2,
			ActorId = "user-1"
		}, CancellationToken.None).ConfigureAwait(false);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "asc-no-tenant-2",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = ts1,
			ActorId = "user-1"
		}, CancellationToken.None).ConfigureAwait(false);

		// Act - ascending order, no tenant
		var results = await store.QueryAsync(new AuditQuery
		{
			OrderByDescending = false
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert - older first
		results.Count.ShouldBe(2);
		results[0].EventId.ShouldBe("asc-no-tenant-2"); // ts1 is older
		results[1].EventId.ShouldBe("asc-no-tenant-1"); // ts2 is newer
	}

	[Fact]
	public async Task InMemoryAuditStore_CountAsync_NoTenantFilter_CountsAllEvents()
	{
		// Arrange
		var store = new InMemoryAuditStore();

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "cnt-all-1",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
			TenantId = "tenant-a"
		}, CancellationToken.None).ConfigureAwait(false);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "cnt-all-2",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
			TenantId = "tenant-b"
		}, CancellationToken.None).ConfigureAwait(false);

		_ = await store.StoreAsync(new AuditEvent
		{
			EventId = "cnt-all-3",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
			// No TenantId = default tenant
		}, CancellationToken.None).ConfigureAwait(false);

		// Act - no tenant filter, should count all
		var count = await store.CountAsync(new AuditQuery(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		count.ShouldBe(3L);
	}

	#endregion

	#region AuditHasher - Edge Cases

	[Fact]
	public void AuditHasher_ComputeHash_WithAllNullOptionalFields_ProducesValidHash()
	{
		// Arrange - minimal event with all optional fields null
		var auditEvent = new AuditEvent
		{
			EventId = "minimal-evt",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero),
			ActorId = "user-1"
			// All optional fields null: ActorType, ResourceId, ResourceType, etc.
		};

		// Act
		var hash = AuditHasher.ComputeHash(auditEvent, null);

		// Assert
		hash.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void AuditHasher_VerifyHash_ReturnsFalse_WhenHashMismatch()
	{
		// Arrange
		var auditEvent = new AuditEvent
		{
			EventId = "mismatch-evt",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero),
			ActorId = "user-1",
			EventHash = "WRONG_HASH_VALUE"
		};

		// Act
		var isValid = AuditHasher.VerifyHash(auditEvent, null);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public void AuditHasher_ComputeGenesisHash_WithNullTenant_UsesDefault()
	{
		// Arrange
		var initTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var hash = AuditHasher.ComputeGenesisHash(null, initTime);

		// Assert
		hash.ShouldNotBeNullOrEmpty();
	}

	#endregion

	#region DefaultAuditLogger - Edge Cases

	[Fact]
	public async Task DefaultAuditLogger_LogAsync_ReturnsFailureIndicator_WhenStoreThrows()
	{
		// Arrange
		var store = A.Fake<IAuditStore>();
		var sut = new DefaultAuditLogger(store, NullLogger<DefaultAuditLogger>.Instance);

		var auditEvent = new AuditEvent
		{
			EventId = "store-fail-test",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1"
		};

		A.CallTo(() => store.StoreAsync(auditEvent, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Database unavailable"));

		// Act
		var result = await sut.LogAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);

		// Assert - should return failure indicator, not throw
		result.EventId.ShouldBe("store-fail-test");
		result.EventHash.ShouldBeEmpty();
		result.SequenceNumber.ShouldBe(-1);
	}

	#endregion

	#region AuditLoggingServiceCollectionExtensions - Edge Cases

	[Fact]
	public void AddAuditLogging_WithFactory_ThenAddRbacAuditStore_WithScopedLifetime_PreservesLifetime()
	{
		// Arrange - verify scoped lifetime is preserved through factory + RBAC decoration
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Use a custom scoped factory registration
		var scopedDescriptor = new ServiceDescriptor(
			typeof(IAuditStore),
			_ => new InMemoryAuditStore(),
			ServiceLifetime.Scoped);
		((ICollection<ServiceDescriptor>)services).Add(scopedDescriptor);

		_ = services.AddScoped<IAuditRoleProvider, TestRoleProvider>();

		// Act
		_ = services.AddRbacAuditStore();

		// Assert - scoped lifetime should be preserved
		var descriptor = services.First(d => d.ServiceType == typeof(IAuditStore));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
	}

	#endregion

	#region Helper Types

	private sealed class TestRoleProvider : IAuditRoleProvider
	{
		public Task<AuditLogRole> GetCurrentRoleAsync(CancellationToken cancellationToken = default)
			=> Task.FromResult(AuditLogRole.Administrator);
	}

	#endregion
}
