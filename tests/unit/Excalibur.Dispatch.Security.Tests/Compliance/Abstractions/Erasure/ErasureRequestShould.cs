// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Erasure;

/// <summary>
/// Unit tests for <see cref="ErasureRequest"/> record and related enums.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Erasure")]
public sealed class ErasureRequestShould : UnitTestBase
{
	[Fact]
	public void CreateValidRequestWithRequiredProperties()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var request = new ErasureRequest
		{
			DataSubjectId = "user@example.com",
			IdType = DataSubjectIdType.Email,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "support-agent-001"
		};

		var after = DateTimeOffset.UtcNow;

		// Assert
		request.DataSubjectId.ShouldBe("user@example.com");
		request.IdType.ShouldBe(DataSubjectIdType.Email);
		request.LegalBasis.ShouldBe(ErasureLegalBasis.ConsentWithdrawal);
		request.RequestedBy.ShouldBe("support-agent-001");
		request.RequestId.ShouldNotBe(Guid.Empty);
		request.RequestedAt.ShouldBeGreaterThanOrEqualTo(before);
		request.RequestedAt.ShouldBeLessThanOrEqualTo(after);
		request.Scope.ShouldBe(ErasureScope.User); // Default
	}

	[Fact]
	public void CreateFullyPopulatedRequest()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var requestedAt = DateTimeOffset.UtcNow.AddDays(-1);
		var gracePeriod = TimeSpan.FromDays(30);
		var dataCategories = new List<string> { "PII", "Financial", "Health" };
		var metadata = new Dictionary<string, string>
		{
			["ticket"] = "SUP-12345",
			["region"] = "EU"
		};

		// Act
		var request = new ErasureRequest
		{
			RequestId = requestId,
			DataSubjectId = "user-12345",
			IdType = DataSubjectIdType.UserId,
			TenantId = "tenant-abc",
			Scope = ErasureScope.Selective,
			LegalBasis = ErasureLegalBasis.RightToObject,
			ExternalReference = "GDPR-REQ-2026-001",
			RequestedBy = "dpo@company.com",
			GracePeriodOverride = gracePeriod,
			DataCategories = dataCategories,
			RequestedAt = requestedAt,
			Metadata = metadata
		};

		// Assert
		request.RequestId.ShouldBe(requestId);
		request.DataSubjectId.ShouldBe("user-12345");
		request.IdType.ShouldBe(DataSubjectIdType.UserId);
		request.TenantId.ShouldBe("tenant-abc");
		request.Scope.ShouldBe(ErasureScope.Selective);
		request.LegalBasis.ShouldBe(ErasureLegalBasis.RightToObject);
		request.ExternalReference.ShouldBe("GDPR-REQ-2026-001");
		request.RequestedBy.ShouldBe("dpo@company.com");
		request.GracePeriodOverride.ShouldBe(gracePeriod);
		request.DataCategories.ShouldBe(dataCategories);
		request.RequestedAt.ShouldBe(requestedAt);
		request.Metadata.ShouldBe(metadata);
	}

	[Fact]
	public void GenerateUniqueRequestIds()
	{
		// Act
		var request1 = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "admin"
		};

		var request2 = new ErasureRequest
		{
			DataSubjectId = "user-2",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "admin"
		};

		// Assert
		request1.RequestId.ShouldNotBe(request2.RequestId);
	}

	[Theory]
	[InlineData(DataSubjectIdType.UserId)]
	[InlineData(DataSubjectIdType.Email)]
	[InlineData(DataSubjectIdType.ExternalId)]
	[InlineData(DataSubjectIdType.NationalId)]
	[InlineData(DataSubjectIdType.Custom)]
	public void SupportAllDataSubjectIdTypes(DataSubjectIdType idType)
	{
		// Act
		var request = new ErasureRequest
		{
			DataSubjectId = "test-subject",
			IdType = idType,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "admin"
		};

		// Assert
		request.IdType.ShouldBe(idType);
	}

	[Theory]
	[InlineData(ErasureScope.User)]
	[InlineData(ErasureScope.Tenant)]
	[InlineData(ErasureScope.Selective)]
	public void SupportAllErasureScopes(ErasureScope scope)
	{
		// Act
		var request = new ErasureRequest
		{
			DataSubjectId = "test",
			IdType = DataSubjectIdType.UserId,
			Scope = scope,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "admin"
		};

		// Assert
		request.Scope.ShouldBe(scope);
	}

	[Theory]
	[InlineData(ErasureLegalBasis.DataNoLongerNecessary)]
	[InlineData(ErasureLegalBasis.ConsentWithdrawal)]
	[InlineData(ErasureLegalBasis.RightToObject)]
	[InlineData(ErasureLegalBasis.UnlawfulProcessing)]
	[InlineData(ErasureLegalBasis.LegalObligation)]
	[InlineData(ErasureLegalBasis.ChildData)]
	[InlineData(ErasureLegalBasis.DataSubjectRequest)]
	public void SupportAllLegalBases(ErasureLegalBasis legalBasis)
	{
		// Act
		var request = new ErasureRequest
		{
			DataSubjectId = "test",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = legalBasis,
			RequestedBy = "admin"
		};

		// Assert
		request.LegalBasis.ShouldBe(legalBasis);
	}

	[Fact]
	public void HaveUserIdAsDefaultIdType()
	{
		// Arrange
		DataSubjectIdType defaultValue = default;

		// Assert
		defaultValue.ShouldBe(DataSubjectIdType.UserId);
	}

	[Fact]
	public void HaveUserScopeAsDefaultScope()
	{
		// Arrange
		ErasureScope defaultValue = default;

		// Assert
		defaultValue.ShouldBe(ErasureScope.User);
	}

	[Fact]
	public void HaveDataNoLongerNecessaryAsDefaultLegalBasis()
	{
		// Arrange
		ErasureLegalBasis defaultValue = default;

		// Assert
		defaultValue.ShouldBe(ErasureLegalBasis.DataNoLongerNecessary);
	}

	[Theory]
	[InlineData(DataSubjectIdType.UserId, 0)]
	[InlineData(DataSubjectIdType.Email, 1)]
	[InlineData(DataSubjectIdType.ExternalId, 2)]
	[InlineData(DataSubjectIdType.NationalId, 3)]
	[InlineData(DataSubjectIdType.Custom, 99)]
	public void HaveCorrectIdTypeUnderlyingValues(DataSubjectIdType idType, int expectedValue)
	{
		// Assert
		((int)idType).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData(ErasureScope.User, 0)]
	[InlineData(ErasureScope.Tenant, 1)]
	[InlineData(ErasureScope.Selective, 2)]
	public void HaveCorrectScopeUnderlyingValues(ErasureScope scope, int expectedValue)
	{
		// Assert
		((int)scope).ShouldBe(expectedValue);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var requestId = Guid.NewGuid();
		var requestedAt = DateTimeOffset.UtcNow;

		var request1 = new ErasureRequest
		{
			RequestId = requestId,
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			RequestedAt = requestedAt
		};

		var request2 = new ErasureRequest
		{
			RequestId = requestId,
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			RequestedAt = requestedAt
		};

		// Assert
		request1.ShouldBe(request2);
	}
}
