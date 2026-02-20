// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Diagnostics;

/// <summary>
/// Event IDs for leader election components (180000-184999).
/// </summary>
/// <remarks>
/// Range allocation:
/// - 180000-180999: Core (InMemory)
/// - 181000-181999: Consul
/// - 182000-182099: Kubernetes
/// - 182100-182199: Kubernetes Hosted Service
/// - 183000-183999: Redis
/// - 184000-184999: SqlServer
/// </remarks>
public static class LeaderElectionEventId
{
	// ========================================
	// 180000-180999: Core (InMemory)
	// ========================================

	/// <summary>Started leader election for resource.</summary>
	public const int InMemoryStarted = 180000;

	/// <summary>Stopped leader election for resource.</summary>
	public const int InMemoryStopped = 180001;

	/// <summary>Updated health status for candidate.</summary>
	public const int InMemoryHealthUpdated = 180002;

	/// <summary>Stepped down from leadership due to unhealthy status.</summary>
	public const int InMemorySteppedDownUnhealthy = 180003;

	/// <summary>Acquired leadership for resource.</summary>
	public const int InMemoryAcquiredLeadership = 180004;

	/// <summary>Renewed leadership lease for resource.</summary>
	public const int InMemoryRenewedLease = 180005;

	/// <summary>Error during lease renewal for resource.</summary>
	public const int InMemoryRenewalError = 180006;

	// ========================================
	// 181000-181999: Consul
	// ========================================

	/// <summary>Retry attempt for Consul operation.</summary>
	public const int ConsulRetryAttempt = 181000;

	/// <summary>Leader election is already running.</summary>
	public const int ConsulAlreadyRunning = 181001;

	/// <summary>Starting Consul leader election.</summary>
	public const int ConsulStartingElection = 181002;

	/// <summary>Stopping Consul leader election.</summary>
	public const int ConsulStoppingElection = 181003;

	/// <summary>Error getting current leader.</summary>
	public const int ConsulErrorGettingCurrentLeader = 181004;

	/// <summary>Failed to update health status in Consul.</summary>
	public const int ConsulFailedToUpdateHealth = 181005;

	/// <summary>Leader is unhealthy, stepping down.</summary>
	public const int ConsulSteppingDownUnhealthy = 181006;

	/// <summary>Failed to deserialize health data.</summary>
	public const int ConsulFailedToDeserializeHealth = 181007;

	/// <summary>Error getting candidate health from Consul.</summary>
	public const int ConsulErrorGettingCandidateHealth = 181008;

	/// <summary>Created Consul session.</summary>
	public const int ConsulCreatedSession = 181009;

	/// <summary>Failed to create Consul session.</summary>
	public const int ConsulFailedToCreateSession = 181010;

	/// <summary>Destroyed Consul session.</summary>
	public const int ConsulDestroyedSession = 181011;

	/// <summary>Failed to destroy Consul session.</summary>
	public const int ConsulFailedToDestroySession = 181012;

	/// <summary>Cannot acquire leadership without a valid session.</summary>
	public const int ConsulCannotAcquireWithoutSession = 181013;

	/// <summary>Acquired leadership.</summary>
	public const int ConsulAcquiredLeadership = 181014;

	/// <summary>Failed to acquire leadership - another leader exists.</summary>
	public const int ConsulFailedToAcquireLeadership = 181015;

	/// <summary>Error trying to acquire leadership.</summary>
	public const int ConsulErrorAcquiringLeadership = 181016;

	/// <summary>Released leadership.</summary>
	public const int ConsulReleasedLeadership = 181017;

	/// <summary>Error releasing leadership.</summary>
	public const int ConsulErrorReleasingLeadership = 181018;

	/// <summary>Session renewal failed, recreating session.</summary>
	public const int ConsulSessionRenewalFailed = 181019;

	/// <summary>Renewed Consul session.</summary>
	public const int ConsulRenewedSession = 181020;

	/// <summary>Error during session renewal.</summary>
	public const int ConsulErrorDuringRenewal = 181021;

	/// <summary>Error during leadership monitoring.</summary>
	public const int ConsulErrorDuringMonitoring = 181022;

	// ========================================
	// 182000-182099: Kubernetes
	// ========================================

	/// <summary>Retry warning for Kubernetes operation.</summary>
	public const int KubernetesRetryWarning = 182000;

	/// <summary>Initialized Kubernetes leader election.</summary>
	public const int KubernetesInitialized = 182001;

	/// <summary>Leader election is already running.</summary>
	public const int KubernetesAlreadyRunning = 182002;

	/// <summary>Starting leader election.</summary>
	public const int KubernetesStarting = 182003;

	/// <summary>Stopping leader election.</summary>
	public const int KubernetesStopping = 182004;

	/// <summary>Stopped but candidate was not the leader.</summary>
	public const int KubernetesStoppedNotLeader = 182005;

	/// <summary>Leader is unhealthy, stepping down.</summary>
	public const int KubernetesSteppingDownUnhealthy = 182006;

	/// <summary>Failed to parse health annotation.</summary>
	public const int KubernetesHealthAnnotationParseFailed = 182007;

	/// <summary>Failed to get candidate health.</summary>
	public const int KubernetesGetHealthFailed = 182008;

	/// <summary>Failed to read namespace from file.</summary>
	public const int KubernetesNamespaceReadFailed = 182009;

	/// <summary>Lease already exists.</summary>
	public const int KubernetesLeaseExists = 182010;

	/// <summary>Creating lease.</summary>
	public const int KubernetesCreatingLease = 182011;

	/// <summary>Error in election loop.</summary>
	public const int KubernetesElectionLoopError = 182012;

	/// <summary>Lease has no holder, attempting to acquire.</summary>
	public const int KubernetesAttemptingAcquire = 182013;

	/// <summary>Renewing lease as current holder.</summary>
	public const int KubernetesRenewingLease = 182014;

	/// <summary>Lease has expired.</summary>
	public const int KubernetesLeaseExpired = 182015;

	/// <summary>Successfully renewed lease as leader.</summary>
	public const int KubernetesRenewedLease = 182016;

	/// <summary>Acquired leadership.</summary>
	public const int KubernetesAcquiredLeadership = 182017;

	/// <summary>Lost race to acquire lease.</summary>
	public const int KubernetesLostRace = 182018;

	/// <summary>Lost leadership.</summary>
	public const int KubernetesLostLeadership = 182019;

	/// <summary>Leader changed.</summary>
	public const int KubernetesLeaderChanged = 182020;

	/// <summary>Failed to renew lease, will retry.</summary>
	public const int KubernetesRenewalFailed = 182021;

	/// <summary>Error renewing leadership.</summary>
	public const int KubernetesRenewalError = 182022;

	/// <summary>Releasing leadership.</summary>
	public const int KubernetesReleasingLeadership = 182023;

	/// <summary>Failed to release leadership.</summary>
	public const int KubernetesReleaseError = 182024;

	// ========================================
	// 182100-182199: Kubernetes Hosted Service
	// ========================================

	/// <summary>Starting Kubernetes leader election hosted service.</summary>
	public const int KubernetesServiceStarting = 182100;

	/// <summary>Stopping Kubernetes leader election hosted service.</summary>
	public const int KubernetesServiceStopping = 182101;

	/// <summary>This instance became the leader.</summary>
	public const int KubernetesServiceBecameLeader = 182102;

	/// <summary>This instance lost leadership.</summary>
	public const int KubernetesServiceLostLeadership = 182103;

	/// <summary>Leader changed event.</summary>
	public const int KubernetesServiceLeaderChanged = 182104;

	// ========================================
	// 183000-183999: Redis
	// ========================================

	/// <summary>Starting leader election.</summary>
	public const int RedisStarting = 183000;

	/// <summary>Stopping leader election.</summary>
	public const int RedisStopping = 183001;

	/// <summary>Failed to acquire lock.</summary>
	public const int RedisLockAcquisitionFailed = 183002;

	/// <summary>Error acquiring lock.</summary>
	public const int RedisLockAcquisitionError = 183003;

	/// <summary>Released lock.</summary>
	public const int RedisLockReleased = 183004;

	/// <summary>Error releasing lock.</summary>
	public const int RedisLockReleaseError = 183005;

	/// <summary>Error in renewal loop.</summary>
	public const int RedisRenewalError = 183006;

	/// <summary>Error renewing lease.</summary>
	public const int RedisRenewalWarning = 183007;

	/// <summary>Candidate became leader.</summary>
	public const int RedisBecameLeader = 183008;

	/// <summary>Candidate lost leadership.</summary>
	public const int RedisLostLeadership = 183009;

	// ========================================
	// 184000-184999: SqlServer
	// ========================================

	/// <summary>Starting leader election.</summary>
	public const int SqlServerStarting = 184000;

	/// <summary>Stopping leader election.</summary>
	public const int SqlServerStopping = 184001;

	/// <summary>Failed to acquire lock.</summary>
	public const int SqlServerLockAcquisitionFailed = 184002;

	/// <summary>Error acquiring lock.</summary>
	public const int SqlServerLockAcquisitionError = 184003;

	/// <summary>Released lock.</summary>
	public const int SqlServerLockReleased = 184004;

	/// <summary>Error releasing lock.</summary>
	public const int SqlServerLockReleaseError = 184005;

	/// <summary>Error in renewal loop.</summary>
	public const int SqlServerRenewalError = 184006;

	/// <summary>Candidate became leader.</summary>
	public const int SqlServerBecameLeader = 184007;

	/// <summary>Candidate lost leadership.</summary>
	public const int SqlServerLostLeadership = 184008;
}
