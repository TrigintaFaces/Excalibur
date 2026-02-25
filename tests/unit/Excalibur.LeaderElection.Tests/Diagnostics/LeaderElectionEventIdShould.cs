// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="LeaderElectionEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Priority", "0")]
public sealed class LeaderElectionEventIdShould
{
	#region InMemory Event IDs (180000-180999)

	[Fact]
	public void HaveInMemoryStartedInInMemoryRange()
	{
		LeaderElectionEventId.InMemoryStarted.ShouldBe(180000);
	}

	[Fact]
	public void HaveInMemoryStoppedInInMemoryRange()
	{
		LeaderElectionEventId.InMemoryStopped.ShouldBe(180001);
	}

	[Fact]
	public void HaveInMemoryHealthUpdatedInInMemoryRange()
	{
		LeaderElectionEventId.InMemoryHealthUpdated.ShouldBe(180002);
	}

	[Fact]
	public void HaveInMemorySteppedDownUnhealthyInInMemoryRange()
	{
		LeaderElectionEventId.InMemorySteppedDownUnhealthy.ShouldBe(180003);
	}

	[Fact]
	public void HaveInMemoryAcquiredLeadershipInInMemoryRange()
	{
		LeaderElectionEventId.InMemoryAcquiredLeadership.ShouldBe(180004);
	}

	[Fact]
	public void HaveInMemoryRenewedLeaseInInMemoryRange()
	{
		LeaderElectionEventId.InMemoryRenewedLease.ShouldBe(180005);
	}

	[Fact]
	public void HaveInMemoryRenewalErrorInInMemoryRange()
	{
		LeaderElectionEventId.InMemoryRenewalError.ShouldBe(180006);
	}

	#endregion

	#region Consul Event IDs (181000-181999)

	[Fact]
	public void HaveConsulRetryAttemptInConsulRange()
	{
		LeaderElectionEventId.ConsulRetryAttempt.ShouldBe(181000);
	}

	[Fact]
	public void HaveConsulAlreadyRunningInConsulRange()
	{
		LeaderElectionEventId.ConsulAlreadyRunning.ShouldBe(181001);
	}

	[Fact]
	public void HaveConsulStartingElectionInConsulRange()
	{
		LeaderElectionEventId.ConsulStartingElection.ShouldBe(181002);
	}

	[Fact]
	public void HaveConsulStoppingElectionInConsulRange()
	{
		LeaderElectionEventId.ConsulStoppingElection.ShouldBe(181003);
	}

	[Fact]
	public void HaveConsulErrorGettingCurrentLeaderInConsulRange()
	{
		LeaderElectionEventId.ConsulErrorGettingCurrentLeader.ShouldBe(181004);
	}

	[Fact]
	public void HaveConsulFailedToUpdateHealthInConsulRange()
	{
		LeaderElectionEventId.ConsulFailedToUpdateHealth.ShouldBe(181005);
	}

	[Fact]
	public void HaveConsulSteppingDownUnhealthyInConsulRange()
	{
		LeaderElectionEventId.ConsulSteppingDownUnhealthy.ShouldBe(181006);
	}

	[Fact]
	public void HaveConsulFailedToDeserializeHealthInConsulRange()
	{
		LeaderElectionEventId.ConsulFailedToDeserializeHealth.ShouldBe(181007);
	}

	[Fact]
	public void HaveConsulErrorGettingCandidateHealthInConsulRange()
	{
		LeaderElectionEventId.ConsulErrorGettingCandidateHealth.ShouldBe(181008);
	}

	[Fact]
	public void HaveConsulCreatedSessionInConsulRange()
	{
		LeaderElectionEventId.ConsulCreatedSession.ShouldBe(181009);
	}

	[Fact]
	public void HaveConsulFailedToCreateSessionInConsulRange()
	{
		LeaderElectionEventId.ConsulFailedToCreateSession.ShouldBe(181010);
	}

	[Fact]
	public void HaveConsulDestroyedSessionInConsulRange()
	{
		LeaderElectionEventId.ConsulDestroyedSession.ShouldBe(181011);
	}

	[Fact]
	public void HaveConsulFailedToDestroySessionInConsulRange()
	{
		LeaderElectionEventId.ConsulFailedToDestroySession.ShouldBe(181012);
	}

	[Fact]
	public void HaveConsulCannotAcquireWithoutSessionInConsulRange()
	{
		LeaderElectionEventId.ConsulCannotAcquireWithoutSession.ShouldBe(181013);
	}

	[Fact]
	public void HaveConsulAcquiredLeadershipInConsulRange()
	{
		LeaderElectionEventId.ConsulAcquiredLeadership.ShouldBe(181014);
	}

	[Fact]
	public void HaveConsulFailedToAcquireLeadershipInConsulRange()
	{
		LeaderElectionEventId.ConsulFailedToAcquireLeadership.ShouldBe(181015);
	}

	[Fact]
	public void HaveConsulErrorAcquiringLeadershipInConsulRange()
	{
		LeaderElectionEventId.ConsulErrorAcquiringLeadership.ShouldBe(181016);
	}

	[Fact]
	public void HaveConsulReleasedLeadershipInConsulRange()
	{
		LeaderElectionEventId.ConsulReleasedLeadership.ShouldBe(181017);
	}

	[Fact]
	public void HaveConsulErrorReleasingLeadershipInConsulRange()
	{
		LeaderElectionEventId.ConsulErrorReleasingLeadership.ShouldBe(181018);
	}

	[Fact]
	public void HaveConsulSessionRenewalFailedInConsulRange()
	{
		LeaderElectionEventId.ConsulSessionRenewalFailed.ShouldBe(181019);
	}

	[Fact]
	public void HaveConsulRenewedSessionInConsulRange()
	{
		LeaderElectionEventId.ConsulRenewedSession.ShouldBe(181020);
	}

	[Fact]
	public void HaveConsulErrorDuringRenewalInConsulRange()
	{
		LeaderElectionEventId.ConsulErrorDuringRenewal.ShouldBe(181021);
	}

	[Fact]
	public void HaveConsulErrorDuringMonitoringInConsulRange()
	{
		LeaderElectionEventId.ConsulErrorDuringMonitoring.ShouldBe(181022);
	}

	#endregion

	#region Kubernetes Event IDs (182000-182099)

	[Fact]
	public void HaveKubernetesRetryWarningInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesRetryWarning.ShouldBe(182000);
	}

	[Fact]
	public void HaveKubernetesInitializedInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesInitialized.ShouldBe(182001);
	}

	[Fact]
	public void HaveKubernetesAlreadyRunningInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesAlreadyRunning.ShouldBe(182002);
	}

	[Fact]
	public void HaveKubernetesStartingInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesStarting.ShouldBe(182003);
	}

	[Fact]
	public void HaveKubernetesStoppingInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesStopping.ShouldBe(182004);
	}

	[Fact]
	public void HaveKubernetesStoppedNotLeaderInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesStoppedNotLeader.ShouldBe(182005);
	}

	[Fact]
	public void HaveKubernetesSteppingDownUnhealthyInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesSteppingDownUnhealthy.ShouldBe(182006);
	}

	[Fact]
	public void HaveKubernetesHealthAnnotationParseFailedInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesHealthAnnotationParseFailed.ShouldBe(182007);
	}

	[Fact]
	public void HaveKubernetesGetHealthFailedInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesGetHealthFailed.ShouldBe(182008);
	}

	[Fact]
	public void HaveKubernetesNamespaceReadFailedInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesNamespaceReadFailed.ShouldBe(182009);
	}

	[Fact]
	public void HaveKubernetesLeaseExistsInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesLeaseExists.ShouldBe(182010);
	}

	[Fact]
	public void HaveKubernetesCreatingLeaseInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesCreatingLease.ShouldBe(182011);
	}

	[Fact]
	public void HaveKubernetesElectionLoopErrorInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesElectionLoopError.ShouldBe(182012);
	}

	[Fact]
	public void HaveKubernetesAttemptingAcquireInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesAttemptingAcquire.ShouldBe(182013);
	}

	[Fact]
	public void HaveKubernetesRenewingLeaseInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesRenewingLease.ShouldBe(182014);
	}

	[Fact]
	public void HaveKubernetesLeaseExpiredInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesLeaseExpired.ShouldBe(182015);
	}

	[Fact]
	public void HaveKubernetesRenewedLeaseInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesRenewedLease.ShouldBe(182016);
	}

	[Fact]
	public void HaveKubernetesAcquiredLeadershipInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesAcquiredLeadership.ShouldBe(182017);
	}

	[Fact]
	public void HaveKubernetesLostRaceInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesLostRace.ShouldBe(182018);
	}

	[Fact]
	public void HaveKubernetesLostLeadershipInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesLostLeadership.ShouldBe(182019);
	}

	[Fact]
	public void HaveKubernetesLeaderChangedInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesLeaderChanged.ShouldBe(182020);
	}

	[Fact]
	public void HaveKubernetesRenewalFailedInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesRenewalFailed.ShouldBe(182021);
	}

	[Fact]
	public void HaveKubernetesRenewalErrorInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesRenewalError.ShouldBe(182022);
	}

	[Fact]
	public void HaveKubernetesReleasingLeadershipInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesReleasingLeadership.ShouldBe(182023);
	}

	[Fact]
	public void HaveKubernetesReleaseErrorInKubernetesRange()
	{
		LeaderElectionEventId.KubernetesReleaseError.ShouldBe(182024);
	}

	#endregion

	#region Kubernetes Hosted Service Event IDs (182100-182199)

	[Fact]
	public void HaveKubernetesServiceStartingInServiceRange()
	{
		LeaderElectionEventId.KubernetesServiceStarting.ShouldBe(182100);
	}

	[Fact]
	public void HaveKubernetesServiceStoppingInServiceRange()
	{
		LeaderElectionEventId.KubernetesServiceStopping.ShouldBe(182101);
	}

	[Fact]
	public void HaveKubernetesServiceBecameLeaderInServiceRange()
	{
		LeaderElectionEventId.KubernetesServiceBecameLeader.ShouldBe(182102);
	}

	[Fact]
	public void HaveKubernetesServiceLostLeadershipInServiceRange()
	{
		LeaderElectionEventId.KubernetesServiceLostLeadership.ShouldBe(182103);
	}

	[Fact]
	public void HaveKubernetesServiceLeaderChangedInServiceRange()
	{
		LeaderElectionEventId.KubernetesServiceLeaderChanged.ShouldBe(182104);
	}

	#endregion

	#region Redis Event IDs (183000-183999)

	[Fact]
	public void HaveRedisStartingInRedisRange()
	{
		LeaderElectionEventId.RedisStarting.ShouldBe(183000);
	}

	[Fact]
	public void HaveRedisStoppingInRedisRange()
	{
		LeaderElectionEventId.RedisStopping.ShouldBe(183001);
	}

	[Fact]
	public void HaveRedisLockAcquisitionFailedInRedisRange()
	{
		LeaderElectionEventId.RedisLockAcquisitionFailed.ShouldBe(183002);
	}

	[Fact]
	public void HaveRedisLockAcquisitionErrorInRedisRange()
	{
		LeaderElectionEventId.RedisLockAcquisitionError.ShouldBe(183003);
	}

	[Fact]
	public void HaveRedisLockReleasedInRedisRange()
	{
		LeaderElectionEventId.RedisLockReleased.ShouldBe(183004);
	}

	[Fact]
	public void HaveRedisLockReleaseErrorInRedisRange()
	{
		LeaderElectionEventId.RedisLockReleaseError.ShouldBe(183005);
	}

	[Fact]
	public void HaveRedisRenewalErrorInRedisRange()
	{
		LeaderElectionEventId.RedisRenewalError.ShouldBe(183006);
	}

	[Fact]
	public void HaveRedisRenewalWarningInRedisRange()
	{
		LeaderElectionEventId.RedisRenewalWarning.ShouldBe(183007);
	}

	[Fact]
	public void HaveRedisBecameLeaderInRedisRange()
	{
		LeaderElectionEventId.RedisBecameLeader.ShouldBe(183008);
	}

	[Fact]
	public void HaveRedisLostLeadershipInRedisRange()
	{
		LeaderElectionEventId.RedisLostLeadership.ShouldBe(183009);
	}

	#endregion

	#region SqlServer Event IDs (184000-184999)

	[Fact]
	public void HaveSqlServerStartingInSqlServerRange()
	{
		LeaderElectionEventId.SqlServerStarting.ShouldBe(184000);
	}

	[Fact]
	public void HaveSqlServerStoppingInSqlServerRange()
	{
		LeaderElectionEventId.SqlServerStopping.ShouldBe(184001);
	}

	[Fact]
	public void HaveSqlServerLockAcquisitionFailedInSqlServerRange()
	{
		LeaderElectionEventId.SqlServerLockAcquisitionFailed.ShouldBe(184002);
	}

	[Fact]
	public void HaveSqlServerLockAcquisitionErrorInSqlServerRange()
	{
		LeaderElectionEventId.SqlServerLockAcquisitionError.ShouldBe(184003);
	}

	[Fact]
	public void HaveSqlServerLockReleasedInSqlServerRange()
	{
		LeaderElectionEventId.SqlServerLockReleased.ShouldBe(184004);
	}

	[Fact]
	public void HaveSqlServerLockReleaseErrorInSqlServerRange()
	{
		LeaderElectionEventId.SqlServerLockReleaseError.ShouldBe(184005);
	}

	[Fact]
	public void HaveSqlServerRenewalErrorInSqlServerRange()
	{
		LeaderElectionEventId.SqlServerRenewalError.ShouldBe(184006);
	}

	[Fact]
	public void HaveSqlServerBecameLeaderInSqlServerRange()
	{
		LeaderElectionEventId.SqlServerBecameLeader.ShouldBe(184007);
	}

	[Fact]
	public void HaveSqlServerLostLeadershipInSqlServerRange()
	{
		LeaderElectionEventId.SqlServerLostLeadership.ShouldBe(184008);
	}

	#endregion

	#region Event ID Range Validation Tests

	[Fact]
	public void HaveAllInMemoryEventIdsInExpectedRange()
	{
		LeaderElectionEventId.InMemoryStarted.ShouldBeInRange(180000, 180999);
		LeaderElectionEventId.InMemoryStopped.ShouldBeInRange(180000, 180999);
		LeaderElectionEventId.InMemoryHealthUpdated.ShouldBeInRange(180000, 180999);
		LeaderElectionEventId.InMemorySteppedDownUnhealthy.ShouldBeInRange(180000, 180999);
		LeaderElectionEventId.InMemoryAcquiredLeadership.ShouldBeInRange(180000, 180999);
		LeaderElectionEventId.InMemoryRenewedLease.ShouldBeInRange(180000, 180999);
		LeaderElectionEventId.InMemoryRenewalError.ShouldBeInRange(180000, 180999);
	}

	[Fact]
	public void HaveAllConsulEventIdsInExpectedRange()
	{
		LeaderElectionEventId.ConsulRetryAttempt.ShouldBeInRange(181000, 181999);
		LeaderElectionEventId.ConsulAlreadyRunning.ShouldBeInRange(181000, 181999);
		LeaderElectionEventId.ConsulStartingElection.ShouldBeInRange(181000, 181999);
		LeaderElectionEventId.ConsulStoppingElection.ShouldBeInRange(181000, 181999);
		LeaderElectionEventId.ConsulErrorGettingCurrentLeader.ShouldBeInRange(181000, 181999);
		LeaderElectionEventId.ConsulFailedToUpdateHealth.ShouldBeInRange(181000, 181999);
		LeaderElectionEventId.ConsulSteppingDownUnhealthy.ShouldBeInRange(181000, 181999);
		LeaderElectionEventId.ConsulErrorDuringMonitoring.ShouldBeInRange(181000, 181999);
	}

	[Fact]
	public void HaveAllKubernetesEventIdsInExpectedRange()
	{
		LeaderElectionEventId.KubernetesRetryWarning.ShouldBeInRange(182000, 182099);
		LeaderElectionEventId.KubernetesInitialized.ShouldBeInRange(182000, 182099);
		LeaderElectionEventId.KubernetesAlreadyRunning.ShouldBeInRange(182000, 182099);
		LeaderElectionEventId.KubernetesStarting.ShouldBeInRange(182000, 182099);
		LeaderElectionEventId.KubernetesStopping.ShouldBeInRange(182000, 182099);
		LeaderElectionEventId.KubernetesStoppedNotLeader.ShouldBeInRange(182000, 182099);
		LeaderElectionEventId.KubernetesReleaseError.ShouldBeInRange(182000, 182099);
	}

	[Fact]
	public void HaveAllKubernetesServiceEventIdsInExpectedRange()
	{
		LeaderElectionEventId.KubernetesServiceStarting.ShouldBeInRange(182100, 182199);
		LeaderElectionEventId.KubernetesServiceStopping.ShouldBeInRange(182100, 182199);
		LeaderElectionEventId.KubernetesServiceBecameLeader.ShouldBeInRange(182100, 182199);
		LeaderElectionEventId.KubernetesServiceLostLeadership.ShouldBeInRange(182100, 182199);
		LeaderElectionEventId.KubernetesServiceLeaderChanged.ShouldBeInRange(182100, 182199);
	}

	[Fact]
	public void HaveAllRedisEventIdsInExpectedRange()
	{
		LeaderElectionEventId.RedisStarting.ShouldBeInRange(183000, 183999);
		LeaderElectionEventId.RedisStopping.ShouldBeInRange(183000, 183999);
		LeaderElectionEventId.RedisLockAcquisitionFailed.ShouldBeInRange(183000, 183999);
		LeaderElectionEventId.RedisLockAcquisitionError.ShouldBeInRange(183000, 183999);
		LeaderElectionEventId.RedisLockReleased.ShouldBeInRange(183000, 183999);
		LeaderElectionEventId.RedisLostLeadership.ShouldBeInRange(183000, 183999);
	}

	[Fact]
	public void HaveAllSqlServerEventIdsInExpectedRange()
	{
		LeaderElectionEventId.SqlServerStarting.ShouldBeInRange(184000, 184999);
		LeaderElectionEventId.SqlServerStopping.ShouldBeInRange(184000, 184999);
		LeaderElectionEventId.SqlServerLockAcquisitionFailed.ShouldBeInRange(184000, 184999);
		LeaderElectionEventId.SqlServerLockAcquisitionError.ShouldBeInRange(184000, 184999);
		LeaderElectionEventId.SqlServerLockReleased.ShouldBeInRange(184000, 184999);
		LeaderElectionEventId.SqlServerLostLeadership.ShouldBeInRange(184000, 184999);
	}

	#endregion

	#region Overall Range Validation

	[Fact]
	public void HaveAllEventIdsWithinLeaderElectionPackageRange()
	{
		// LeaderElection package owns range 180000-184999
		var allEventIds = GetAllEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(180000, 184999,
				$"Event ID {eventId} is outside LeaderElection package range (180000-184999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIdsForAllDefinedEventIds()
	{
		var allEventIds = GetAllEventIds();

		allEventIds.Distinct().Count().ShouldBe(
			allEventIds.Length,
			"All event IDs must be unique");
	}

	[Fact]
	public void HaveNoDuplicateInMemoryEventIds()
	{
		var inmemoryEventIds = new[]
		{
			LeaderElectionEventId.InMemoryStarted,
			LeaderElectionEventId.InMemoryStopped,
			LeaderElectionEventId.InMemoryHealthUpdated,
			LeaderElectionEventId.InMemorySteppedDownUnhealthy,
			LeaderElectionEventId.InMemoryAcquiredLeadership,
			LeaderElectionEventId.InMemoryRenewedLease,
			LeaderElectionEventId.InMemoryRenewalError
		};

		inmemoryEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateConsulEventIds()
	{
		var consulEventIds = new[]
		{
			LeaderElectionEventId.ConsulRetryAttempt,
			LeaderElectionEventId.ConsulAlreadyRunning,
			LeaderElectionEventId.ConsulStartingElection,
			LeaderElectionEventId.ConsulStoppingElection,
			LeaderElectionEventId.ConsulErrorGettingCurrentLeader,
			LeaderElectionEventId.ConsulFailedToUpdateHealth,
			LeaderElectionEventId.ConsulSteppingDownUnhealthy,
			LeaderElectionEventId.ConsulFailedToDeserializeHealth,
			LeaderElectionEventId.ConsulErrorGettingCandidateHealth,
			LeaderElectionEventId.ConsulCreatedSession,
			LeaderElectionEventId.ConsulFailedToCreateSession,
			LeaderElectionEventId.ConsulDestroyedSession,
			LeaderElectionEventId.ConsulFailedToDestroySession,
			LeaderElectionEventId.ConsulCannotAcquireWithoutSession,
			LeaderElectionEventId.ConsulAcquiredLeadership,
			LeaderElectionEventId.ConsulFailedToAcquireLeadership,
			LeaderElectionEventId.ConsulErrorAcquiringLeadership,
			LeaderElectionEventId.ConsulReleasedLeadership,
			LeaderElectionEventId.ConsulErrorReleasingLeadership,
			LeaderElectionEventId.ConsulSessionRenewalFailed,
			LeaderElectionEventId.ConsulRenewedSession,
			LeaderElectionEventId.ConsulErrorDuringRenewal,
			LeaderElectionEventId.ConsulErrorDuringMonitoring
		};

		consulEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateKubernetesEventIds()
	{
		var kubernetesEventIds = new[]
		{
			LeaderElectionEventId.KubernetesRetryWarning,
			LeaderElectionEventId.KubernetesInitialized,
			LeaderElectionEventId.KubernetesAlreadyRunning,
			LeaderElectionEventId.KubernetesStarting,
			LeaderElectionEventId.KubernetesStopping,
			LeaderElectionEventId.KubernetesStoppedNotLeader,
			LeaderElectionEventId.KubernetesSteppingDownUnhealthy,
			LeaderElectionEventId.KubernetesHealthAnnotationParseFailed,
			LeaderElectionEventId.KubernetesGetHealthFailed,
			LeaderElectionEventId.KubernetesNamespaceReadFailed,
			LeaderElectionEventId.KubernetesLeaseExists,
			LeaderElectionEventId.KubernetesCreatingLease,
			LeaderElectionEventId.KubernetesElectionLoopError,
			LeaderElectionEventId.KubernetesAttemptingAcquire,
			LeaderElectionEventId.KubernetesRenewingLease,
			LeaderElectionEventId.KubernetesLeaseExpired,
			LeaderElectionEventId.KubernetesRenewedLease,
			LeaderElectionEventId.KubernetesAcquiredLeadership,
			LeaderElectionEventId.KubernetesLostRace,
			LeaderElectionEventId.KubernetesLostLeadership,
			LeaderElectionEventId.KubernetesLeaderChanged,
			LeaderElectionEventId.KubernetesRenewalFailed,
			LeaderElectionEventId.KubernetesRenewalError,
			LeaderElectionEventId.KubernetesReleasingLeadership,
			LeaderElectionEventId.KubernetesReleaseError
		};

		kubernetesEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateRedisEventIds()
	{
		var redisEventIds = new[]
		{
			LeaderElectionEventId.RedisStarting,
			LeaderElectionEventId.RedisStopping,
			LeaderElectionEventId.RedisLockAcquisitionFailed,
			LeaderElectionEventId.RedisLockAcquisitionError,
			LeaderElectionEventId.RedisLockReleased,
			LeaderElectionEventId.RedisLockReleaseError,
			LeaderElectionEventId.RedisRenewalError,
			LeaderElectionEventId.RedisRenewalWarning,
			LeaderElectionEventId.RedisBecameLeader,
			LeaderElectionEventId.RedisLostLeadership
		};

		redisEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveNoDuplicateSqlServerEventIds()
	{
		var sqlServerEventIds = new[]
		{
			LeaderElectionEventId.SqlServerStarting,
			LeaderElectionEventId.SqlServerStopping,
			LeaderElectionEventId.SqlServerLockAcquisitionFailed,
			LeaderElectionEventId.SqlServerLockAcquisitionError,
			LeaderElectionEventId.SqlServerLockReleased,
			LeaderElectionEventId.SqlServerLockReleaseError,
			LeaderElectionEventId.SqlServerRenewalError,
			LeaderElectionEventId.SqlServerBecameLeader,
			LeaderElectionEventId.SqlServerLostLeadership
		};

		sqlServerEventIds.ShouldBeUnique();
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllEventIds()
	{
		return new[]
		{
			// InMemory (180000-180999)
			LeaderElectionEventId.InMemoryStarted,
			LeaderElectionEventId.InMemoryStopped,
			LeaderElectionEventId.InMemoryHealthUpdated,
			LeaderElectionEventId.InMemorySteppedDownUnhealthy,
			LeaderElectionEventId.InMemoryAcquiredLeadership,
			LeaderElectionEventId.InMemoryRenewedLease,
			LeaderElectionEventId.InMemoryRenewalError,

			// Consul (181000-181999)
			LeaderElectionEventId.ConsulRetryAttempt,
			LeaderElectionEventId.ConsulAlreadyRunning,
			LeaderElectionEventId.ConsulStartingElection,
			LeaderElectionEventId.ConsulStoppingElection,
			LeaderElectionEventId.ConsulErrorGettingCurrentLeader,
			LeaderElectionEventId.ConsulFailedToUpdateHealth,
			LeaderElectionEventId.ConsulSteppingDownUnhealthy,
			LeaderElectionEventId.ConsulFailedToDeserializeHealth,
			LeaderElectionEventId.ConsulErrorGettingCandidateHealth,
			LeaderElectionEventId.ConsulCreatedSession,
			LeaderElectionEventId.ConsulFailedToCreateSession,
			LeaderElectionEventId.ConsulDestroyedSession,
			LeaderElectionEventId.ConsulFailedToDestroySession,
			LeaderElectionEventId.ConsulCannotAcquireWithoutSession,
			LeaderElectionEventId.ConsulAcquiredLeadership,
			LeaderElectionEventId.ConsulFailedToAcquireLeadership,
			LeaderElectionEventId.ConsulErrorAcquiringLeadership,
			LeaderElectionEventId.ConsulReleasedLeadership,
			LeaderElectionEventId.ConsulErrorReleasingLeadership,
			LeaderElectionEventId.ConsulSessionRenewalFailed,
			LeaderElectionEventId.ConsulRenewedSession,
			LeaderElectionEventId.ConsulErrorDuringRenewal,
			LeaderElectionEventId.ConsulErrorDuringMonitoring,

			// Kubernetes (182000-182099)
			LeaderElectionEventId.KubernetesRetryWarning,
			LeaderElectionEventId.KubernetesInitialized,
			LeaderElectionEventId.KubernetesAlreadyRunning,
			LeaderElectionEventId.KubernetesStarting,
			LeaderElectionEventId.KubernetesStopping,
			LeaderElectionEventId.KubernetesStoppedNotLeader,
			LeaderElectionEventId.KubernetesSteppingDownUnhealthy,
			LeaderElectionEventId.KubernetesHealthAnnotationParseFailed,
			LeaderElectionEventId.KubernetesGetHealthFailed,
			LeaderElectionEventId.KubernetesNamespaceReadFailed,
			LeaderElectionEventId.KubernetesLeaseExists,
			LeaderElectionEventId.KubernetesCreatingLease,
			LeaderElectionEventId.KubernetesElectionLoopError,
			LeaderElectionEventId.KubernetesAttemptingAcquire,
			LeaderElectionEventId.KubernetesRenewingLease,
			LeaderElectionEventId.KubernetesLeaseExpired,
			LeaderElectionEventId.KubernetesRenewedLease,
			LeaderElectionEventId.KubernetesAcquiredLeadership,
			LeaderElectionEventId.KubernetesLostRace,
			LeaderElectionEventId.KubernetesLostLeadership,
			LeaderElectionEventId.KubernetesLeaderChanged,
			LeaderElectionEventId.KubernetesRenewalFailed,
			LeaderElectionEventId.KubernetesRenewalError,
			LeaderElectionEventId.KubernetesReleasingLeadership,
			LeaderElectionEventId.KubernetesReleaseError,

			// Kubernetes Hosted Service (182100-182199)
			LeaderElectionEventId.KubernetesServiceStarting,
			LeaderElectionEventId.KubernetesServiceStopping,
			LeaderElectionEventId.KubernetesServiceBecameLeader,
			LeaderElectionEventId.KubernetesServiceLostLeadership,
			LeaderElectionEventId.KubernetesServiceLeaderChanged,

			// Redis (183000-183999)
			LeaderElectionEventId.RedisStarting,
			LeaderElectionEventId.RedisStopping,
			LeaderElectionEventId.RedisLockAcquisitionFailed,
			LeaderElectionEventId.RedisLockAcquisitionError,
			LeaderElectionEventId.RedisLockReleased,
			LeaderElectionEventId.RedisLockReleaseError,
			LeaderElectionEventId.RedisRenewalError,
			LeaderElectionEventId.RedisRenewalWarning,
			LeaderElectionEventId.RedisBecameLeader,
			LeaderElectionEventId.RedisLostLeadership,

			// SqlServer (184000-184999)
			LeaderElectionEventId.SqlServerStarting,
			LeaderElectionEventId.SqlServerStopping,
			LeaderElectionEventId.SqlServerLockAcquisitionFailed,
			LeaderElectionEventId.SqlServerLockAcquisitionError,
			LeaderElectionEventId.SqlServerLockReleased,
			LeaderElectionEventId.SqlServerLockReleaseError,
			LeaderElectionEventId.SqlServerRenewalError,
			LeaderElectionEventId.SqlServerBecameLeader,
			LeaderElectionEventId.SqlServerLostLeadership
		};
	}

	#endregion
}
