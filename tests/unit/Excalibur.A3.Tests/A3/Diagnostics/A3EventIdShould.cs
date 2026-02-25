// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Diagnostics;

namespace Excalibur.Tests.A3.Diagnostics;

/// <summary>
/// Unit tests for <see cref="A3EventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Priority", "0")]
public sealed class A3EventIdShould : UnitTestBase
{
	#region Authentication Core Event ID Tests (180000-180099)

	[Fact]
	public void HaveAuthenticationStartedInAuthenticationRange()
	{
		A3EventId.AuthenticationStarted.ShouldBe(180000);
	}

	[Fact]
	public void HaveAllAuthenticationCoreEventIdsInExpectedRange()
	{
		A3EventId.AuthenticationStarted.ShouldBeInRange(180000, 180099);
		A3EventId.AuthenticationSucceeded.ShouldBeInRange(180000, 180099);
		A3EventId.AuthenticationFailed.ShouldBeInRange(180000, 180099);
		A3EventId.TokenValidated.ShouldBeInRange(180000, 180099);
		A3EventId.TokenExpired.ShouldBeInRange(180000, 180099);
	}

	#endregion

	#region Authorization Core Event ID Tests (180500-180599)

	[Fact]
	public void HaveAuthorizationCheckStartedInAuthorizationRange()
	{
		A3EventId.AuthorizationCheckStarted.ShouldBe(180500);
	}

	[Fact]
	public void HaveAllAuthorizationCoreEventIdsInExpectedRange()
	{
		A3EventId.AuthorizationCheckStarted.ShouldBeInRange(180500, 180599);
		A3EventId.AuthorizationGranted.ShouldBeInRange(180500, 180599);
		A3EventId.AuthorizationDenied.ShouldBeInRange(180500, 180599);
		A3EventId.PermissionEvaluated.ShouldBeInRange(180500, 180599);
	}

	#endregion

	#region Activity Groups Event ID Tests (181500-181599)

	[Fact]
	public void HaveActivityGroupsErrorInActivityGroupsRange()
	{
		A3EventId.ActivityGroupsError.ShouldBe(181500);
	}

	[Fact]
	public void HaveAllActivityGroupsEventIdsInExpectedRange()
	{
		A3EventId.ActivityGroupsError.ShouldBeInRange(181500, 181599);
		A3EventId.ActivityGrantsError.ShouldBeInRange(181500, 181599);
		A3EventId.ActivityGroupsRetrieved.ShouldBeInRange(181500, 181599);
		A3EventId.ActivityGrantsRetrieved.ShouldBeInRange(181500, 181599);
	}

	#endregion

	#region Grant Repository Event ID Tests (180600-180699)

	[Fact]
	public void HaveGrantSaveErrorInGrantRepositoryRange()
	{
		A3EventId.GrantSaveError.ShouldBe(180600);
	}

	[Fact]
	public void HaveAllGrantRepositoryEventIdsInExpectedRange()
	{
		A3EventId.GrantSaveError.ShouldBeInRange(180600, 180699);
	}

	#endregion

	#region Audit Middleware Event ID Tests (181100-181199)

	[Fact]
	public void HaveAuditPublishFailureInAuditMiddlewareRange()
	{
		A3EventId.AuditPublishFailure.ShouldBe(181100);
	}

	[Fact]
	public void HaveAllAuditMiddlewareEventIdsInExpectedRange()
	{
		A3EventId.AuditPublishFailure.ShouldBeInRange(181100, 181199);
	}

	#endregion

	#region A3 Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInA3ReservedRange()
	{
		// A3 reserved range is 180000-182999
		var allEventIds = GetAllA3EventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(180000, 182999,
				$"Event ID {eventId} is outside A3 reserved range (180000-182999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllA3EventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllA3EventIds();
		allEventIds.Length.ShouldBeGreaterThan(10);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllA3EventIds()
	{
		return
		[
			// Authentication Core (180000-180099)
			A3EventId.AuthenticationStarted,
			A3EventId.AuthenticationSucceeded,
			A3EventId.AuthenticationFailed,
			A3EventId.TokenValidated,
			A3EventId.TokenExpired,

			// Authorization Core (180500-180599)
			A3EventId.AuthorizationCheckStarted,
			A3EventId.AuthorizationGranted,
			A3EventId.AuthorizationDenied,
			A3EventId.PermissionEvaluated,

			// Grant Repository (180600-180699)
			A3EventId.GrantSaveError,

			// Audit Middleware (181100-181199)
			A3EventId.AuditPublishFailure,

			// Activity Groups (181500-181599)
			A3EventId.ActivityGroupsError,
			A3EventId.ActivityGrantsError,
			A3EventId.ActivityGroupsRetrieved,
			A3EventId.ActivityGrantsRetrieved
		];
	}

	#endregion
}
