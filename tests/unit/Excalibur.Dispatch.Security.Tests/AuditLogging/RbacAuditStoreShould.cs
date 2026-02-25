// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

/// <summary>
/// Tests for <see cref="RbacAuditStore"/> role-based access control decorator.
/// </summary>
[Trait("Category", "Unit")]
[UnitTest]
public sealed class RbacAuditStoreShould
{
	private readonly IAuditStore _innerStore;
	private readonly IAuditRoleProvider _roleProvider;
	private readonly IAuditLogger _metaAuditLogger;
	private readonly ILogger<RbacAuditStore> _logger;
	private readonly RbacAuditStore _sut;

	public RbacAuditStoreShould()
	{
		_innerStore = A.Fake<IAuditStore>();
		_roleProvider = A.Fake<IAuditRoleProvider>();
		_metaAuditLogger = A.Fake<IAuditLogger>();
		_logger = new NullLogger<RbacAuditStore>();
		_sut = new RbacAuditStore(_innerStore, _roleProvider, _logger, null, _metaAuditLogger);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenInnerStoreIsNull()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new RbacAuditStore(null!, _roleProvider, _logger, null, _metaAuditLogger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenRoleProviderIsNull()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new RbacAuditStore(_innerStore, null!, _logger, null, _metaAuditLogger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new RbacAuditStore(_innerStore, _roleProvider, null!, null, _metaAuditLogger));
	}

	[Fact]
	public void NotThrow_WhenMetaAuditLoggerIsNull()
	{
		// Arrange & Act & Assert
		_ = Should.NotThrow(() =>
			new RbacAuditStore(_innerStore, _roleProvider, _logger, null));
	}

	#endregion Constructor Tests

	#region StoreAsync Tests - Always Allowed

	[Theory]
	[InlineData(AuditLogRole.None)]
	[InlineData(AuditLogRole.Developer)]
	[InlineData(AuditLogRole.SecurityAnalyst)]
	[InlineData(AuditLogRole.ComplianceOfficer)]
	[InlineData(AuditLogRole.Administrator)]
	public async Task StoreAsync_AllowsAnyRole(AuditLogRole role)
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(role);

		var auditEvent = CreateAuditEvent(AuditEventType.Security);
		var expectedId = new AuditEventId
		{
			EventId = "test-id",
			EventHash = "test-hash",
			SequenceNumber = 1,
			RecordedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _innerStore.StoreAsync(auditEvent, A<CancellationToken>._))
			.Returns(expectedId);

		// Act
		var result = await _sut.StoreAsync(auditEvent, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedId);
		_ = A.CallTo(() => _innerStore.StoreAsync(auditEvent, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StoreAsync_DelegatesDirectlyWithoutRoleCheck()
	{
		// Arrange
		var auditEvent = CreateAuditEvent(AuditEventType.Security);
		var expectedId = new AuditEventId
		{
			EventId = "test-id",
			EventHash = "hash",
			SequenceNumber = 1,
			RecordedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _innerStore.StoreAsync(auditEvent, A<CancellationToken>._))
			.Returns(expectedId);

		// Act
		_ = await _sut.StoreAsync(auditEvent, CancellationToken.None);

		// Assert - role provider should NOT be called for store operations
		A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion StoreAsync Tests - Always Allowed

	#region GetByIdAsync Tests - Role-Based Access

	[Fact]
	public async Task GetByIdAsync_DenyAccessForNoneRole()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.None);

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
			await _sut.GetByIdAsync("event-123", CancellationToken.None));
	}

	[Fact]
	public async Task GetByIdAsync_DenyAccessForDeveloperRole()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Developer);

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
			await _sut.GetByIdAsync("event-123", CancellationToken.None));
	}

	[Theory]
	[InlineData(AuditEventType.Authentication)]
	[InlineData(AuditEventType.Authorization)]
	[InlineData(AuditEventType.Security)]
	public async Task GetByIdAsync_AllowSecurityAnalystToAccessSecurityEvents(AuditEventType eventType)
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.SecurityAnalyst);

		var auditEvent = CreateAuditEvent(eventType);
		_ = A.CallTo(() => _innerStore.GetByIdAsync("event-123", A<CancellationToken>._))
			.Returns(auditEvent);

		// Act
		var result = await _sut.GetByIdAsync("event-123", CancellationToken.None);

		// Assert
		result.ShouldBe(auditEvent);
	}

	[Theory]
	[InlineData(AuditEventType.DataAccess)]
	[InlineData(AuditEventType.DataModification)]
	[InlineData(AuditEventType.Compliance)]
	[InlineData(AuditEventType.System)]
	public async Task GetByIdAsync_DenySecurityAnalystAccessToNonSecurityEvents(AuditEventType eventType)
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.SecurityAnalyst);

		var auditEvent = CreateAuditEvent(eventType);
		_ = A.CallTo(() => _innerStore.GetByIdAsync("event-123", A<CancellationToken>._))
			.Returns(auditEvent);

		// Act
		var result = await _sut.GetByIdAsync("event-123", CancellationToken.None);

		// Assert
		result.ShouldBeNull(); // Filtered out, not exception
	}

	[Theory]
	[InlineData(AuditEventType.Authentication)]
	[InlineData(AuditEventType.DataAccess)]
	[InlineData(AuditEventType.DataModification)]
	[InlineData(AuditEventType.Compliance)]
	[InlineData(AuditEventType.System)]
	public async Task GetByIdAsync_AllowComplianceOfficerToAccessAllEvents(AuditEventType eventType)
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.ComplianceOfficer);

		var auditEvent = CreateAuditEvent(eventType);
		_ = A.CallTo(() => _innerStore.GetByIdAsync("event-123", A<CancellationToken>._))
			.Returns(auditEvent);

		// Act
		var result = await _sut.GetByIdAsync("event-123", CancellationToken.None);

		// Assert
		result.ShouldBe(auditEvent);
	}

	[Theory]
	[InlineData(AuditEventType.Authentication)]
	[InlineData(AuditEventType.DataAccess)]
	[InlineData(AuditEventType.DataModification)]
	[InlineData(AuditEventType.Compliance)]
	[InlineData(AuditEventType.System)]
	public async Task GetByIdAsync_AllowAdministratorToAccessAllEvents(AuditEventType eventType)
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var auditEvent = CreateAuditEvent(eventType);
		_ = A.CallTo(() => _innerStore.GetByIdAsync("event-123", A<CancellationToken>._))
			.Returns(auditEvent);

		// Act
		var result = await _sut.GetByIdAsync("event-123", CancellationToken.None);

		// Assert
		result.ShouldBe(auditEvent);
	}

	[Fact]
	public async Task GetByIdAsync_ReturnsNullWhenInnerStoreReturnsNull()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		_ = A.CallTo(() => _innerStore.GetByIdAsync("non-existent", A<CancellationToken>._))
			.Returns((AuditEvent?)null);

		// Act
		var result = await _sut.GetByIdAsync("non-existent", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	#endregion GetByIdAsync Tests - Role-Based Access

	#region QueryAsync Tests - Role-Based Filtering

	[Fact]
	public async Task QueryAsync_DenyAccessForNoneRole()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.None);

		var query = new AuditQuery();

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
			await _sut.QueryAsync(query, CancellationToken.None));
	}

	[Fact]
	public async Task QueryAsync_DenyAccessForDeveloperRole()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Developer);

		var query = new AuditQuery();

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
			await _sut.QueryAsync(query, CancellationToken.None));
	}

	[Fact]
	public async Task QueryAsync_FilterToSecurityEventsForSecurityAnalyst()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.SecurityAnalyst);

		var query = new AuditQuery();
		AuditQuery? capturedQuery = null;

		_ = A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
			.Returns(Array.Empty<AuditEvent>());

		// Act
		_ = await _sut.QueryAsync(query, CancellationToken.None);

		// Assert
		_ = capturedQuery.ShouldNotBeNull();
		_ = capturedQuery.EventTypes.ShouldNotBeNull();
		capturedQuery.EventTypes.ShouldContain(AuditEventType.Authentication);
		capturedQuery.EventTypes.ShouldContain(AuditEventType.Authorization);
		capturedQuery.EventTypes.ShouldContain(AuditEventType.Security);
		capturedQuery.EventTypes.Count.ShouldBe(3);
	}

	[Fact]
	public async Task QueryAsync_IntersectWithRequestedTypesForSecurityAnalyst()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.SecurityAnalyst);

		// User requests Security and DataAccess, but should only get Security
		var query = new AuditQuery
		{
			EventTypes = [AuditEventType.Security, AuditEventType.DataAccess]
		};

		AuditQuery? capturedQuery = null;

		_ = A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
			.Returns(Array.Empty<AuditEvent>());

		// Act
		_ = await _sut.QueryAsync(query, CancellationToken.None);

		// Assert
		_ = capturedQuery.ShouldNotBeNull();
		_ = capturedQuery.EventTypes.ShouldNotBeNull();
		capturedQuery.EventTypes.ShouldContain(AuditEventType.Security);
		capturedQuery.EventTypes.ShouldNotContain(AuditEventType.DataAccess);
		capturedQuery.EventTypes.Count.ShouldBe(1);
	}

	[Fact]
	public async Task QueryAsync_PassUnmodifiedQueryForComplianceOfficer()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.ComplianceOfficer);

		var query = new AuditQuery
		{
			EventTypes = [AuditEventType.DataAccess, AuditEventType.Compliance]
		};

		AuditQuery? capturedQuery = null;

		_ = A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
			.Returns(Array.Empty<AuditEvent>());

		// Act
		_ = await _sut.QueryAsync(query, CancellationToken.None);

		// Assert
		capturedQuery.ShouldBe(query); // Same reference - unmodified
	}

	[Fact]
	public async Task QueryAsync_PassUnmodifiedQueryForAdministrator()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var query = new AuditQuery
		{
			EventTypes = [AuditEventType.DataAccess, AuditEventType.Compliance]
		};

		AuditQuery? capturedQuery = null;

		_ = A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
			.Returns(Array.Empty<AuditEvent>());

		// Act
		_ = await _sut.QueryAsync(query, CancellationToken.None);

		// Assert
		capturedQuery.ShouldBe(query); // Same reference - unmodified
	}

	[Fact]
	public async Task QueryAsync_ThrowsOnNullQuery()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.QueryAsync(null!, CancellationToken.None));
	}

	#endregion QueryAsync Tests - Role-Based Filtering

	#region CountAsync Tests

	[Fact]
	public async Task CountAsync_DenyAccessForNoneRole()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.None);

		var query = new AuditQuery();

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
			await _sut.CountAsync(query, CancellationToken.None));
	}

	[Fact]
	public async Task CountAsync_DenyAccessForDeveloperRole()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Developer);

		var query = new AuditQuery();

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
			await _sut.CountAsync(query, CancellationToken.None));
	}

	[Fact]
	public async Task CountAsync_ApplyRoleFiltersForSecurityAnalyst()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.SecurityAnalyst);

		var query = new AuditQuery();
		AuditQuery? capturedQuery = null;

		_ = A.CallTo(() => _innerStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Invokes((AuditQuery q, CancellationToken _) => capturedQuery = q)
			.Returns(10L);

		// Act
		var result = await _sut.CountAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBe(10L);
		_ = capturedQuery.ShouldNotBeNull();
		_ = capturedQuery.EventTypes.ShouldNotBeNull();
		capturedQuery.EventTypes.Count.ShouldBe(3); // Security event types only
	}

	[Fact]
	public async Task CountAsync_AllowComplianceOfficerAccess()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.ComplianceOfficer);

		var query = new AuditQuery();
		_ = A.CallTo(() => _innerStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns(42L);

		// Act
		var result = await _sut.CountAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBe(42L);
	}

	[Fact]
	public async Task CountAsync_AllowAdministratorAccess()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var query = new AuditQuery();
		_ = A.CallTo(() => _innerStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns(99L);

		// Act
		var result = await _sut.CountAsync(query, CancellationToken.None);

		// Assert
		result.ShouldBe(99L);
	}

	[Fact]
	public async Task CountAsync_ThrowsOnNullQuery()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.CountAsync(null!, CancellationToken.None));
	}

	#endregion CountAsync Tests

	#region VerifyChainIntegrityAsync Tests

	[Theory]
	[InlineData(AuditLogRole.None)]
	[InlineData(AuditLogRole.Developer)]
	[InlineData(AuditLogRole.SecurityAnalyst)]
	public async Task VerifyChainIntegrityAsync_DenyAccessForLowerRoles(AuditLogRole role)
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(role);

		var startDate = DateTimeOffset.UtcNow.AddDays(-1);
		var endDate = DateTimeOffset.UtcNow;

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
			await _sut.VerifyChainIntegrityAsync(startDate, endDate, CancellationToken.None));
	}

	[Theory]
	[InlineData(AuditLogRole.ComplianceOfficer)]
	[InlineData(AuditLogRole.Administrator)]
	public async Task VerifyChainIntegrityAsync_AllowAccessForHigherRoles(AuditLogRole role)
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(role);

		var startDate = DateTimeOffset.UtcNow.AddDays(-1);
		var endDate = DateTimeOffset.UtcNow;
		var expectedResult = AuditIntegrityResult.Valid(100, startDate, endDate);

		_ = A.CallTo(() => _innerStore.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
		var result = await _sut.VerifyChainIntegrityAsync(startDate, endDate, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBe(expectedResult.IsValid);
		result.EventsVerified.ShouldBe(expectedResult.EventsVerified);
	}

	#endregion VerifyChainIntegrityAsync Tests

	#region GetLastEventAsync Tests

	[Fact]
	public async Task GetLastEventAsync_DenyAccessForNoneRole()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.None);

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
			await _sut.GetLastEventAsync(null, CancellationToken.None));
	}

	[Fact]
	public async Task GetLastEventAsync_DenyAccessForDeveloperRole()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Developer);

		// Act & Assert
		_ = await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
			await _sut.GetLastEventAsync(null, CancellationToken.None));
	}

	[Fact]
	public async Task GetLastEventAsync_AllowAccessForSecurityAnalyst()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.SecurityAnalyst);

		var expectedEvent = CreateAuditEvent(AuditEventType.Security);
		_ = A.CallTo(() => _innerStore.GetLastEventAsync(A<string?>._, A<CancellationToken>._))
			.Returns(expectedEvent);

		// Act
		var result = await _sut.GetLastEventAsync(null, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedEvent);
	}

	[Fact]
	public async Task GetLastEventAsync_AllowAccessForComplianceOfficer()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.ComplianceOfficer);

		var expectedEvent = CreateAuditEvent(AuditEventType.DataAccess);
		_ = A.CallTo(() => _innerStore.GetLastEventAsync(A<string?>._, A<CancellationToken>._))
			.Returns(expectedEvent);

		// Act
		var result = await _sut.GetLastEventAsync(null, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedEvent);
	}

	[Fact]
	public async Task GetLastEventAsync_AllowAccessForAdministrator()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var expectedEvent = CreateAuditEvent(AuditEventType.System);
		_ = A.CallTo(() => _innerStore.GetLastEventAsync(A<string?>._, A<CancellationToken>._))
			.Returns(expectedEvent);

		// Act
		var result = await _sut.GetLastEventAsync(null, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedEvent);
	}

	[Fact]
	public async Task GetLastEventAsync_PassesTenantIdToInnerStore()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		_ = A.CallTo(() => _innerStore.GetLastEventAsync("tenant-x", A<CancellationToken>._))
			.Returns((AuditEvent?)null);

		// Act
		var result = await _sut.GetLastEventAsync("tenant-x", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
		_ = A.CallTo(() => _innerStore.GetLastEventAsync("tenant-x", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion GetLastEventAsync Tests

	#region Meta-Audit Logging Tests

	[Fact]
	public async Task GetByIdAsync_LogsMetaAuditEvent()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var auditEvent = CreateAuditEvent(AuditEventType.Security);
		_ = A.CallTo(() => _innerStore.GetByIdAsync("event-123", A<CancellationToken>._))
			.Returns(auditEvent);

		// Act
		_ = await _sut.GetByIdAsync("event-123", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _metaAuditLogger.LogAsync(
				A<AuditEvent>.That.Matches(e =>
					e.Action == "AuditLog.GetById" &&
					e.EventType == AuditEventType.DataAccess &&
					e.Reason.Contains("Administrator")),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task QueryAsync_LogsMetaAuditEvent()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.ComplianceOfficer);

		var query = new AuditQuery();
		_ = A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns(new List<AuditEvent> { CreateAuditEvent(AuditEventType.Security) });

		// Act
		_ = await _sut.QueryAsync(query, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _metaAuditLogger.LogAsync(
				A<AuditEvent>.That.Matches(e =>
					e.Action == "AuditLog.Query" &&
					e.Reason.Contains("ResultCount=1")),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task VerifyChainIntegrityAsync_LogsMetaAuditEvent()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var startDate = DateTimeOffset.UtcNow.AddDays(-1);
		var endDate = DateTimeOffset.UtcNow;
		var expectedResult = AuditIntegrityResult.Valid(100, startDate, endDate);

		_ = A.CallTo(() => _innerStore.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
			.Returns(expectedResult);

		// Act
		_ = await _sut.VerifyChainIntegrityAsync(startDate, endDate, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _metaAuditLogger.LogAsync(
				A<AuditEvent>.That.Matches(e =>
					e.Action == "AuditLog.VerifyIntegrity"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MetaAuditFailure_DoesNotBlockMainOperation()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var auditEvent = CreateAuditEvent(AuditEventType.Security);
		_ = A.CallTo(() => _innerStore.GetByIdAsync("event-123", A<CancellationToken>._))
			.Returns(auditEvent);

		_ = A.CallTo(() => _metaAuditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Meta-audit failure"));

		// Act
		var result = await _sut.GetByIdAsync("event-123", CancellationToken.None);

		// Assert - Main operation should still succeed
		result.ShouldBe(auditEvent);
	}

	[Fact]
	public async Task GetByIdAsync_NoMetaAuditWhenLoggerIsNull()
	{
		// Arrange
		var sutWithoutMetaLogger = new RbacAuditStore(_innerStore, _roleProvider, _logger, null);

		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var auditEvent = CreateAuditEvent(AuditEventType.Security);
		_ = A.CallTo(() => _innerStore.GetByIdAsync("event-123", A<CancellationToken>._))
			.Returns(auditEvent);

		// Act - should not throw even without meta logger
		var result = await sutWithoutMetaLogger.GetByIdAsync("event-123", CancellationToken.None);

		// Assert
		result.ShouldBe(auditEvent);
	}

	[Fact]
	public async Task GetByIdAsync_MetaAuditEventHasCorrectFields()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.SecurityAnalyst);

		var auditEvent = CreateAuditEvent(AuditEventType.Authentication);
		_ = A.CallTo(() => _innerStore.GetByIdAsync("event-123", A<CancellationToken>._))
			.Returns(auditEvent);

		// Act
		_ = await _sut.GetByIdAsync("event-123", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _metaAuditLogger.LogAsync(
				A<AuditEvent>.That.Matches(e =>
					e.EventId.StartsWith("meta-") &&
					e.EventType == AuditEventType.DataAccess &&
					e.Action == "AuditLog.GetById" &&
					e.Outcome == AuditOutcome.Success &&
					e.ActorId == "role:SecurityAnalyst" &&
					e.ActorType == "AuditLogAccess" &&
					e.ResourceType == "AuditLog" &&
					e.Reason.Contains("SecurityAnalyst") &&
					e.Reason.Contains("event-123")),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion Meta-Audit Logging Tests

	#region Cancellation Token Tests

	[Fact]
	public async Task QueryAsync_PropagatesCancellationToken()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var query = new AuditQuery();
		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		_ = A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, token))
			.Returns(Array.Empty<AuditEvent>());

		// Act
		_ = await _sut.QueryAsync(query, token);

		// Assert
		_ = A.CallTo(() => _innerStore.QueryAsync(A<AuditQuery>._, token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetByIdAsync_PropagatesCancellationToken()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		_ = A.CallTo(() => _innerStore.GetByIdAsync("event-123", token))
			.Returns((AuditEvent?)null);

		// Act
		_ = await _sut.GetByIdAsync("event-123", token);

		// Assert
		_ = A.CallTo(() => _innerStore.GetByIdAsync("event-123", token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CountAsync_PropagatesCancellationToken()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var query = new AuditQuery();
		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		_ = A.CallTo(() => _innerStore.CountAsync(A<AuditQuery>._, token))
			.Returns(5L);

		// Act
		_ = await _sut.CountAsync(query, token);

		// Assert
		_ = A.CallTo(() => _innerStore.CountAsync(A<AuditQuery>._, token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task VerifyChainIntegrityAsync_PropagatesCancellationToken()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		var startDate = DateTimeOffset.UtcNow.AddDays(-1);
		var endDate = DateTimeOffset.UtcNow;
		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		_ = A.CallTo(() => _innerStore.VerifyChainIntegrityAsync(startDate, endDate, token))
			.Returns(AuditIntegrityResult.Valid(0, startDate, endDate));

		// Act
		_ = await _sut.VerifyChainIntegrityAsync(startDate, endDate, token);

		// Assert
		_ = A.CallTo(() => _innerStore.VerifyChainIntegrityAsync(startDate, endDate, token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetLastEventAsync_PropagatesCancellationToken()
	{
		// Arrange
		_ = A.CallTo(() => _roleProvider.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.Administrator);

		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		_ = A.CallTo(() => _innerStore.GetLastEventAsync(A<string?>._, token))
			.Returns((AuditEvent?)null);

		// Act
		_ = await _sut.GetLastEventAsync(null, token);

		// Assert
		_ = A.CallTo(() => _innerStore.GetLastEventAsync(A<string?>._, token))
			.MustHaveHappenedOnceExactly();
	}

	#endregion Cancellation Token Tests

	#region Helper Methods

	private static AuditEvent CreateAuditEvent(AuditEventType eventType)
	{
		return new AuditEvent
		{
			EventId = $"evt-{Guid.NewGuid():N}",
			EventType = eventType,
			Action = "TestAction",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "test-actor",
			ActorType = "User"
		};
	}

	#endregion Helper Methods
}
