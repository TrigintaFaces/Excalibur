// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

/// <summary>
/// Tests for <see cref="AuditLogRole"/> enum values and ordering.
/// </summary>
[Trait("Category", "Unit")]
[UnitTest]
public sealed class AuditLogRoleShould
{
	[Fact]
	public void HaveCorrectValues()
	{
		// Assert - Per ADR-052 role ordering
		((int)AuditLogRole.None).ShouldBe(0);
		((int)AuditLogRole.Developer).ShouldBe(1);
		((int)AuditLogRole.SecurityAnalyst).ShouldBe(2);
		((int)AuditLogRole.ComplianceOfficer).ShouldBe(3);
		((int)AuditLogRole.Administrator).ShouldBe(4);
	}

	[Fact]
	public void HaveCorrectPrivilegeOrdering()
	{
		// Assert - Lower roles should have lower numeric values
		(AuditLogRole.None < AuditLogRole.Developer).ShouldBeTrue();
		(AuditLogRole.Developer < AuditLogRole.SecurityAnalyst).ShouldBeTrue();
		(AuditLogRole.SecurityAnalyst < AuditLogRole.ComplianceOfficer).ShouldBeTrue();
		(AuditLogRole.ComplianceOfficer < AuditLogRole.Administrator).ShouldBeTrue();
	}

	[Fact]
	public void SupportComparisonForAccessControl()
	{
		// Assert - This pattern is used in RbacAuditStore
		var complianceOfficer = AuditLogRole.ComplianceOfficer;
		(AuditLogRole.ComplianceOfficer >= complianceOfficer).ShouldBeTrue();
		(AuditLogRole.Administrator >= complianceOfficer).ShouldBeTrue();
		(AuditLogRole.SecurityAnalyst >= complianceOfficer).ShouldBeFalse();
		(AuditLogRole.Developer >= complianceOfficer).ShouldBeFalse();
		(AuditLogRole.None >= complianceOfficer).ShouldBeFalse();
	}

	[Fact]
	public void HaveFiveDistinctRoles()
	{
		// Assert
		var roles = Enum.GetValues<AuditLogRole>();
		roles.Length.ShouldBe(5);
		roles.Distinct().Count().ShouldBe(5);
	}

	[Theory]
	[InlineData(AuditLogRole.None, "None")]
	[InlineData(AuditLogRole.Developer, "Developer")]
	[InlineData(AuditLogRole.SecurityAnalyst, "SecurityAnalyst")]
	[InlineData(AuditLogRole.ComplianceOfficer, "ComplianceOfficer")]
	[InlineData(AuditLogRole.Administrator, "Administrator")]
	public void HaveCorrectNames(AuditLogRole role, string expectedName)
	{
		// Assert
		role.ToString().ShouldBe(expectedName);
	}

	[Fact]
	public void IdentifyNoAccessRoles()
	{
		// Arrange - Roles that should have no audit log access
		var noAccessRoles = new[] { AuditLogRole.None, AuditLogRole.Developer };

		// Assert
		foreach (var role in noAccessRoles)
		{
			(role < AuditLogRole.SecurityAnalyst).ShouldBeTrue($"{role} should not have audit access");
		}
	}

	[Fact]
	public void IdentifyReadAccessRoles()
	{
		// Arrange - Roles that should have at least read access
		var readAccessRoles = new[]
		{
			AuditLogRole.SecurityAnalyst,
			AuditLogRole.ComplianceOfficer,
			AuditLogRole.Administrator
		};

		// Assert
		foreach (var role in readAccessRoles)
		{
			(role >= AuditLogRole.SecurityAnalyst).ShouldBeTrue($"{role} should have read access");
		}
	}
}
