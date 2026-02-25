// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Transactions;

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Queries;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Tests.Application.Requests.Queries;

/// <summary>
/// Unit tests for <see cref="QueryBase{TResponse}"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
[Trait("Feature", "Queries")]
public sealed class QueryBaseShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithCorrelationId_SetsCorrelationId()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var query = new TestQuery(correlationId);

		// Assert
		((IAmCorrelatable)query).CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void Create_WithTenantId_SetsTenantId()
	{
		// Arrange
		var correlationId = Guid.NewGuid();
		var tenantId = "tenant-456";

		// Act
		var query = new TestQuery(correlationId, tenantId);

		// Assert
		query.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public void Create_WithoutTenantId_SetsDefaultTenantId()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var query = new TestQuery(correlationId);

		// Assert
		query.TenantId.ShouldBe(TenantDefaults.DefaultTenantId);
	}

	[Fact]
	public void Create_WithDefaultConstructor_SetsEmptyCorrelationId()
	{
		// Act
		var query = new TestQueryWithDefaultConstructor();

		// Assert
		((IAmCorrelatable)query).CorrelationId.ShouldBe(Guid.Empty);
	}

	#endregion

	#region Id Property Tests

	[Fact]
	public void Id_GeneratesUniqueGuid()
	{
		// Arrange & Act
		var query1 = new TestQuery(Guid.NewGuid());
		var query2 = new TestQuery(Guid.NewGuid());

		// Assert
		query1.Id.ShouldNotBe(Guid.Empty);
		query2.Id.ShouldNotBe(Guid.Empty);
		query1.Id.ShouldNotBe(query2.Id);
	}

	#endregion

	#region MessageId Property Tests

	[Fact]
	public void MessageId_ReturnsIdAsString()
	{
		// Arrange
		var query = new TestQuery(Guid.NewGuid());

		// Act & Assert
		query.MessageId.ShouldBe(query.Id.ToString());
	}

	#endregion

	#region MessageType Property Tests

	[Fact]
	public void MessageType_ReturnsFullTypeName()
	{
		// Arrange
		var query = new TestQuery(Guid.NewGuid());

		// Act
		var messageType = query.MessageType;

		// Assert
		messageType.ShouldContain("TestQuery");
	}

	#endregion

	#region Kind Property Tests

	[Fact]
	public void Kind_ReturnsAction()
	{
		// Arrange
		var query = new TestQuery(Guid.NewGuid());

		// Act & Assert
		query.Kind.ShouldBe(MessageKinds.Action);
	}

	#endregion

	#region Headers Property Tests

	[Fact]
	public void Headers_ReturnsReadOnlyDictionary()
	{
		// Arrange
		var query = new TestQuery(Guid.NewGuid());

		// Act
		var headers = query.Headers;

		// Assert
		headers.ShouldNotBeNull();
		headers.Count.ShouldBe(0);
	}

	#endregion

	#region ActivityType Property Tests

	[Fact]
	public void ActivityType_ReturnsQuery()
	{
		// Arrange
		var query = new TestQuery(Guid.NewGuid());

		// Act
		var activityType = ((IActivity)query).ActivityType;

		// Assert
		activityType.ShouldBe(ActivityType.Query);
	}

	#endregion

	#region ActivityName Property Tests

	[Fact]
	public void ActivityName_ReturnsNamespaceAndTypeName()
	{
		// Arrange
		var query = new TestQuery(Guid.NewGuid());

		// Act
		var activityName = query.ActivityName;

		// Assert
		activityName.ShouldContain(":");
		activityName.ShouldContain("TestQuery");
	}

	#endregion

	#region Transaction Properties Tests

	[Fact]
	public void TransactionBehavior_DefaultsToRequired()
	{
		// Arrange
		var query = new TestQuery(Guid.NewGuid());

		// Act & Assert
		query.TransactionBehavior.ShouldBe(TransactionScopeOption.Required);
	}

	[Fact]
	public void TransactionIsolation_DefaultsToReadCommitted()
	{
		// Arrange
		var query = new TestQuery(Guid.NewGuid());

		// Act & Assert
		query.TransactionIsolation.ShouldBe(IsolationLevel.ReadCommitted);
	}

	[Fact]
	public void TransactionTimeout_DefaultsToTwoMinutes()
	{
		// Arrange
		var query = new TestQuery(Guid.NewGuid());

		// Act & Assert
		query.TransactionTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	#endregion

	#region IQuery Interface Tests

	[Fact]
	public void ImplementsIQuery()
	{
		// Arrange & Act
		var query = new TestQuery(Guid.NewGuid());

		// Assert
		query.ShouldBeAssignableTo<IQuery<string>>();
	}

	[Fact]
	public void ImplementsIActivity()
	{
		// Arrange & Act
		var query = new TestQuery(Guid.NewGuid());

		// Assert
		query.ShouldBeAssignableTo<IActivity>();
	}

	[Fact]
	public void ImplementsIAmCorrelatable()
	{
		// Arrange & Act
		var query = new TestQuery(Guid.NewGuid());

		// Assert
		query.ShouldBeAssignableTo<IAmCorrelatable>();
	}

	[Fact]
	public void ImplementsIAmMultiTenant()
	{
		// Arrange & Act
		var query = new TestQuery(Guid.NewGuid());

		// Assert
		query.ShouldBeAssignableTo<IAmMultiTenant>();
	}

	#endregion

	#region Complex Response Type Tests

	[Fact]
	public void Create_WithComplexResponseType_Succeeds()
	{
		// Arrange & Act
		var query = new TestQueryWithComplexResponse(Guid.NewGuid());

		// Assert
		query.ShouldBeAssignableTo<IQuery<TestQueryResponse>>();
	}

	#endregion

	#region Test Implementations

	private sealed class TestQuery : QueryBase<string>
	{
		public TestQuery(Guid correlationId, string? tenantId = null)
			: base(correlationId, tenantId)
		{
		}

		public override string ActivityDisplayName => "Test Query";
		public override string ActivityDescription => "A test query for unit testing";
	}

	private sealed class TestQueryWithDefaultConstructor : QueryBase<string>
	{
		public override string ActivityDisplayName => "Test Query";
		public override string ActivityDescription => "A test query for unit testing";
	}

	private sealed class TestQueryWithComplexResponse : QueryBase<TestQueryResponse>
	{
		public TestQueryWithComplexResponse(Guid correlationId)
			: base(correlationId)
		{
		}

		public override string ActivityDisplayName => "Complex Query";
		public override string ActivityDescription => "A query with complex response";
	}

	private sealed class TestQueryResponse
	{
		public string? Data { get; init; }
		public int Count { get; init; }
	}

	#endregion
}
