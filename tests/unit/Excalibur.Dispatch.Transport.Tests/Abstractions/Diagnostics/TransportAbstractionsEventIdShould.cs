// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Diagnostics;

/// <summary>
/// Unit tests for <see cref="TransportAbstractionsEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport.Abstractions")]
[Trait("Priority", "0")]
public sealed class TransportAbstractionsEventIdShould : UnitTestBase
{
	#region Core Abstractions Event ID Tests (20000-20099)

	[Fact]
	public void HaveBrokerFactoryCreatedInCoreRange()
	{
		TransportAbstractionsEventId.BrokerFactoryCreated.ShouldBe(20000);
	}

	[Fact]
	public void HaveCloudProviderRegisteredInCoreRange()
	{
		TransportAbstractionsEventId.CloudProviderRegistered.ShouldBe(20001);
	}

	[Fact]
	public void HaveCloudProviderUnregisteredInCoreRange()
	{
		TransportAbstractionsEventId.CloudProviderUnregistered.ShouldBe(20002);
	}

	[Fact]
	public void HaveTransportAdapterInitializedInCoreRange()
	{
		TransportAbstractionsEventId.TransportAdapterInitialized.ShouldBe(20003);
	}

	[Fact]
	public void HaveTransportAdapterDisposedInCoreRange()
	{
		TransportAbstractionsEventId.TransportAdapterDisposed.ShouldBe(20004);
	}

	[Fact]
	public void HaveReturningCachedBrokerInCoreRange()
	{
		TransportAbstractionsEventId.ReturningCachedBroker.ShouldBe(20005);
	}

	[Fact]
	public void HaveCreatingNewBrokerInCoreRange()
	{
		TransportAbstractionsEventId.CreatingNewBroker.ShouldBe(20006);
	}

	[Fact]
	public void HaveBrokerCreatedSuccessfullyInCoreRange()
	{
		TransportAbstractionsEventId.BrokerCreatedSuccessfully.ShouldBe(20007);
	}

	[Fact]
	public void HaveBrokerDisposeErrorInCoreRange()
	{
		TransportAbstractionsEventId.BrokerDisposeError.ShouldBe(20008);
	}

	[Fact]
	public void HaveAllCoreEventIdsInExpectedRange()
	{
		TransportAbstractionsEventId.BrokerFactoryCreated.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.CloudProviderRegistered.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.CloudProviderUnregistered.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.TransportAdapterInitialized.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.TransportAdapterDisposed.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.ReturningCachedBroker.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.CreatingNewBroker.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.BrokerCreatedSuccessfully.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.BrokerDisposeError.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.ProviderAlreadyRegistered.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.ProviderUnregistered.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.ProviderUnregisterFailed.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.GrpcSendingAction.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.GrpcPublishingEvent.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.GrpcSendingDocument.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.FlushMetricsError.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.FlushTimerError.ShouldBeInRange(20000, 20099);
		TransportAbstractionsEventId.LocalMetricEntry.ShouldBeInRange(20000, 20099);
	}

	#endregion

	#region Kubernetes Event ID Tests (20100-20199)

	[Fact]
	public void HavePodLifecycleStartedInKubernetesRange()
	{
		TransportAbstractionsEventId.PodLifecycleStarted.ShouldBe(20100);
	}

	[Fact]
	public void HaveRunningOutsideKubernetesInKubernetesRange()
	{
		TransportAbstractionsEventId.RunningOutsideKubernetes.ShouldBe(20101);
	}

	[Fact]
	public void HaveLifecycleHookRegisteredInKubernetesRange()
	{
		TransportAbstractionsEventId.LifecycleHookRegistered.ShouldBe(20102);
	}

	[Fact]
	public void HaveGracefulShutdownStartedInKubernetesRange()
	{
		TransportAbstractionsEventId.GracefulShutdownStarted.ShouldBe(20111);
	}

	[Fact]
	public void HaveAllKubernetesEventIdsInExpectedRange()
	{
		// PodLifecycleManager (20100-20110)
		TransportAbstractionsEventId.PodLifecycleStarted.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.RunningOutsideKubernetes.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.LifecycleHookRegistered.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.PodReadinessUpdated.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.PodReadinessUpdateFailed.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.ExecutingHooks.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.ExecutingHook.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.HookExecutionError.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.PodMarkedForDeletion.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.PodMonitoringError.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.PodEvent.ShouldBeInRange(20100, 20199);

		// GracefulShutdownHandler (20111-20119)
		TransportAbstractionsEventId.GracefulShutdownStarted.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.BeginningShutdownSequence.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.GracefulShutdownCompleted.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.OperationRegistered.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.OperationCompleted.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.WaitingForInFlightOperations.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.ShutdownTimeoutExceeded.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.ExecutingShutdownHook.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.ShutdownHookError.ShouldBeInRange(20100, 20199);

		// HpaMetricsProvider (20120-20126)
		TransportAbstractionsEventId.HpaMetricsProviderStarted.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.HpaMetricRegistered.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.HpaMetricsUpdateError.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.CgroupV1MemoryLimitReadError.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.CgroupV1MemoryLimitAccessDenied.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.CgroupV2MemoryLimitReadError.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.CgroupV2MemoryLimitAccessDenied.ShouldBeInRange(20100, 20199);

		// ConfigMapReloader (20127-20131)
		TransportAbstractionsEventId.ConfigMapEvent.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.SecretEvent.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.ConfigurationChangeDetected.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.ReloadCallbackError.ShouldBeInRange(20100, 20199);
		TransportAbstractionsEventId.WatchError.ShouldBeInRange(20100, 20199);
	}

	#endregion

	#region ServiceMesh Event ID Tests (20200-20299)

	[Fact]
	public void HaveServiceMeshMiddlewareErrorInServiceMeshRange()
	{
		TransportAbstractionsEventId.ServiceMeshMiddlewareError.ShouldBe(20200);
	}

	[Fact]
	public void HaveServiceTopologyLoggedInServiceMeshRange()
	{
		TransportAbstractionsEventId.ServiceTopologyLogged.ShouldBe(20201);
	}

	[Fact]
	public void HaveCircuitManuallyOpenedInServiceMeshRange()
	{
		TransportAbstractionsEventId.CircuitManuallyOpened.ShouldBe(20212);
	}

	[Fact]
	public void HaveAllServiceMeshEventIdsInExpectedRange()
	{
		// ServiceMeshMiddleware (20200-20203)
		TransportAbstractionsEventId.ServiceMeshMiddlewareError.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.ServiceTopologyLogged.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.TopologyRecordingFailed.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.ServiceMeshMessageProcessed.ShouldBeInRange(20200, 20299);

		// TrafficManager (20204-20206)
		TransportAbstractionsEventId.TrafficRequestCompleted.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.TrafficDefaultPolicySet.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.TrafficSplitRouting.ShouldBeInRange(20200, 20299);

		// EnvoySidecarIntegration (20207-20211)
		TransportAbstractionsEventId.EnvoyGetClusterInfoFailed.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.EnvoyRuntimeConfigUpdated.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.EnvoyUpdateRuntimeConfigFailed.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.EnvoyFlushStatsFailed.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.EnvoyGetLoadBalancingStatsFailed.ShouldBeInRange(20200, 20299);

		// CircuitBreakerProvider (20212-20219)
		TransportAbstractionsEventId.CircuitManuallyOpened.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.CircuitReset.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.CircuitRequestSucceeded.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.CircuitBreakerOpen.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.CircuitRequestFailed.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.CircuitBreakerOpened.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.CircuitBreakerReset.ShouldBeInRange(20200, 20299);
		TransportAbstractionsEventId.CircuitBreakerHalfOpen.ShouldBeInRange(20200, 20299);
	}

	#endregion

	#region Service Discovery Event ID Tests (20300-20399)

	[Fact]
	public void HaveNoEndpointsFoundInServiceDiscoveryRange()
	{
		TransportAbstractionsEventId.NoEndpointsFound.ShouldBe(20300);
	}

	[Fact]
	public void HaveNoHealthyEndpointsFoundInServiceDiscoveryRange()
	{
		TransportAbstractionsEventId.NoHealthyEndpointsFound.ShouldBe(20301);
	}

	[Fact]
	public void HaveDiscoveredEndpointsInServiceDiscoveryRange()
	{
		TransportAbstractionsEventId.DiscoveredEndpoints.ShouldBe(20302);
	}

	[Fact]
	public void HaveAllServiceDiscoveryEventIdsInExpectedRange()
	{
		// ServiceDiscoveryProvider (20300-20308)
		TransportAbstractionsEventId.NoEndpointsFound.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.NoHealthyEndpointsFound.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.DiscoveredEndpoints.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ServiceRegistered.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ServiceDeregistered.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.HealthStatusUpdated.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.HealthCheckError.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.EndpointHealthChanged.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.HealthCheckFailed.ShouldBeInRange(20300, 20399);

		// ConsulServiceRegistry (20309-20322)
		TransportAbstractionsEventId.ConsulRetrievedServiceEndpoints.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulFailedToGetServiceEndpoints.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulRetrievedAllEndpoints.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulFailedToGetAllEndpoints.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulServiceRegistrationNotFound.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulFailedToGetServiceRegistration.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulServiceRegistered.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulFailedToRegisterService.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulServiceDeregistered.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulFailedToDeregisterService.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulHealthStatusUpdated.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulFailedToUpdateHealthStatus.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulServiceChanged.ShouldBeInRange(20300, 20399);
		TransportAbstractionsEventId.ConsulWatchingServiceError.ShouldBeInRange(20300, 20399);
	}

	#endregion

	#region Traffic Management Event ID Tests (20400-20499)

	[Fact]
	public void HaveTrafficManagerStartedInTrafficRange()
	{
		TransportAbstractionsEventId.TrafficManagerStarted.ShouldBe(20400);
	}

	[Fact]
	public void HaveTrafficRoutedInTrafficRange()
	{
		TransportAbstractionsEventId.TrafficRouted.ShouldBe(20401);
	}

	[Fact]
	public void HaveAllTrafficManagementEventIdsInExpectedRange()
	{
		TransportAbstractionsEventId.TrafficManagerStarted.ShouldBeInRange(20400, 20499);
		TransportAbstractionsEventId.TrafficRouted.ShouldBeInRange(20400, 20499);
		TransportAbstractionsEventId.CircuitBreakerProviderCreated.ShouldBeInRange(20400, 20499);
		TransportAbstractionsEventId.TrafficThrottled.ShouldBeInRange(20400, 20499);
	}

	#endregion

	#region TLS Security Event ID Tests (20500-20599)

	[Fact]
	public void HaveMtlsHttpClientConfiguredInTlsRange()
	{
		TransportAbstractionsEventId.MtlsHttpClientConfigured.ShouldBe(20500);
	}

	[Fact]
	public void HaveMtlsClientCertificateAddedInTlsRange()
	{
		TransportAbstractionsEventId.MtlsClientCertificateAdded.ShouldBe(20501);
	}

	[Fact]
	public void HaveAllTlsSecurityEventIdsInExpectedRange()
	{
		TransportAbstractionsEventId.MtlsHttpClientConfigured.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsClientCertificateAdded.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsValidatingPeerCertificate.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsSslPolicyErrors.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsChainValidationFailed.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsAttributeValidationFailed.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsCustomValidationFailed.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsValidationSuccessful.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsRefreshingCertificate.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsCertificateRefreshed.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsRefreshFailed.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsNoCertificatePresented.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsChainValidationError.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsCertificateExpired.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsCertificateNotYetValid.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsValidatingSan.ShouldBeInRange(20500, 20599);
		TransportAbstractionsEventId.MtlsMissingDigitalSignature.ShouldBeInRange(20500, 20599);
	}

	#endregion

	#region Connection Pooling Event ID Tests (20600-20699)

	[Fact]
	public void HaveConnectionPoolInitializedInPoolingRange()
	{
		TransportAbstractionsEventId.ConnectionPoolInitialized.ShouldBe(20600);
	}

	[Fact]
	public void HaveConnectionAcquiredInPoolingRange()
	{
		TransportAbstractionsEventId.ConnectionAcquired.ShouldBe(20601);
	}

	[Fact]
	public void HaveConnectionReturnedInPoolingRange()
	{
		TransportAbstractionsEventId.ConnectionReturned.ShouldBe(20602);
	}

	[Fact]
	public void HaveAllConnectionPoolingEventIdsInExpectedRange()
	{
		TransportAbstractionsEventId.ConnectionPoolInitialized.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionAcquired.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionReturned.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionPoolWarmupStart.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionPoolWarmupComplete.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionPoolDisposing.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionPoolDisposed.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionCreated.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionCreationError.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionDestroyed.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionDestroyError.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionIdleTimeout.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.ConnectionHealthCheckError.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.HealthCheckConnectionCreationError.ShouldBeInRange(20600, 20699);
		TransportAbstractionsEventId.PoolHealthCheckError.ShouldBeInRange(20600, 20699);
	}

	#endregion

	#region Session Management Event ID Tests (20700-20799)

	[Fact]
	public void HaveSessionLockAcquiredInSessionRange()
	{
		TransportAbstractionsEventId.SessionLockAcquired.ShouldBe(20700);
	}

	[Fact]
	public void HaveSessionLockExtendedInSessionRange()
	{
		TransportAbstractionsEventId.SessionLockExtended.ShouldBe(20701);
	}

	[Fact]
	public void HaveSessionLockReleasedInSessionRange()
	{
		TransportAbstractionsEventId.SessionLockReleased.ShouldBe(20702);
	}

	[Fact]
	public void HaveAllSessionManagementEventIdsInExpectedRange()
	{
		TransportAbstractionsEventId.SessionLockAcquired.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionLockExtended.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionLockReleased.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionCreated.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionOpened.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionClosed.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionRenewed.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionAbandoned.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionsCleanedUp.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionStateSet.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionStateDeleted.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionCheckpointCreated.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionCheckpointRestored.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionLockUpgraded.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionLockDowngraded.ShouldBeInRange(20700, 20799);
		TransportAbstractionsEventId.SessionLockBroken.ShouldBeInRange(20700, 20799);
	}

	#endregion

	#region Batch Processing Event ID Tests (20800-20899)

	[Fact]
	public void HaveBatchProcessingFailedInBatchRange()
	{
		TransportAbstractionsEventId.BatchProcessingFailed.ShouldBe(20800);
	}

	[Fact]
	public void HaveBatchProcessedInBatchRange()
	{
		TransportAbstractionsEventId.BatchProcessed.ShouldBe(20801);
	}

	[Fact]
	public void HaveAllBatchProcessingEventIdsInExpectedRange()
	{
		TransportAbstractionsEventId.BatchProcessingFailed.ShouldBeInRange(20800, 20899);
		TransportAbstractionsEventId.BatchProcessed.ShouldBeInRange(20800, 20899);
		TransportAbstractionsEventId.BatchProcessorConfigured.ShouldBeInRange(20800, 20899);
		TransportAbstractionsEventId.BatchChannelClosed.ShouldBeInRange(20800, 20899);
		TransportAbstractionsEventId.BatchStoppingOnError.ShouldBeInRange(20800, 20899);
		TransportAbstractionsEventId.BatchMessageFailedAfterRetries.ShouldBeInRange(20800, 20899);
		TransportAbstractionsEventId.BatchMessageRetrying.ShouldBeInRange(20800, 20899);
	}

	#endregion

	#region Dead Letter Queue Event ID Tests (20900-20999)

	[Fact]
	public void HaveMessageMovedToDeadLetterInDlqRange()
	{
		TransportAbstractionsEventId.MessageMovedToDeadLetter.ShouldBe(20900);
	}

	[Fact]
	public void HaveDeadLetterQueueProcessedInDlqRange()
	{
		TransportAbstractionsEventId.DeadLetterQueueProcessed.ShouldBe(20901);
	}

	[Fact]
	public void HaveDeadLetterReprocessingStartedInDlqRange()
	{
		TransportAbstractionsEventId.DeadLetterReprocessingStarted.ShouldBe(20902);
	}

	[Fact]
	public void HaveAllDeadLetterQueueEventIdsInExpectedRange()
	{
		TransportAbstractionsEventId.MessageMovedToDeadLetter.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterQueueProcessed.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterReprocessingStarted.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterQueueCleared.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterException.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterParseFailed.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterMessagesRetrieved.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterReprocessFailed.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterReprocessSummary.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterPurgeFailed.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterPurgeCompleted.ShouldBeInRange(20900, 20999);
		TransportAbstractionsEventId.DeadLetterMessageReprocessed.ShouldBeInRange(20900, 20999);
	}

	#endregion

	#region Transport Abstractions Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInTransportAbstractionsReservedRange()
	{
		// Transport Abstractions reserved range is 20000-20999
		var allEventIds = GetAllTransportAbstractionsEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(20000, 20999,
				$"Event ID {eventId} is outside Transport Abstractions reserved range (20000-20999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllTransportAbstractionsEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllTransportAbstractionsEventIds();
		allEventIds.Length.ShouldBeGreaterThan(100);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllTransportAbstractionsEventIds()
	{
		return
		[
			// Core Abstractions (20000-20099)
			TransportAbstractionsEventId.BrokerFactoryCreated,
			TransportAbstractionsEventId.CloudProviderRegistered,
			TransportAbstractionsEventId.CloudProviderUnregistered,
			TransportAbstractionsEventId.TransportAdapterInitialized,
			TransportAbstractionsEventId.TransportAdapterDisposed,
			TransportAbstractionsEventId.ReturningCachedBroker,
			TransportAbstractionsEventId.CreatingNewBroker,
			TransportAbstractionsEventId.BrokerCreatedSuccessfully,
			TransportAbstractionsEventId.BrokerDisposeError,
			TransportAbstractionsEventId.ProviderAlreadyRegistered,
			TransportAbstractionsEventId.ProviderUnregistered,
			TransportAbstractionsEventId.ProviderUnregisterFailed,
			TransportAbstractionsEventId.GrpcSendingAction,
			TransportAbstractionsEventId.GrpcPublishingEvent,
			TransportAbstractionsEventId.GrpcSendingDocument,
			TransportAbstractionsEventId.FlushMetricsError,
			TransportAbstractionsEventId.FlushTimerError,
			TransportAbstractionsEventId.LocalMetricEntry,

			// Kubernetes (20100-20199)
			TransportAbstractionsEventId.PodLifecycleStarted,
			TransportAbstractionsEventId.RunningOutsideKubernetes,
			TransportAbstractionsEventId.LifecycleHookRegistered,
			TransportAbstractionsEventId.PodReadinessUpdated,
			TransportAbstractionsEventId.PodReadinessUpdateFailed,
			TransportAbstractionsEventId.ExecutingHooks,
			TransportAbstractionsEventId.ExecutingHook,
			TransportAbstractionsEventId.HookExecutionError,
			TransportAbstractionsEventId.PodMarkedForDeletion,
			TransportAbstractionsEventId.PodMonitoringError,
			TransportAbstractionsEventId.PodEvent,
			TransportAbstractionsEventId.GracefulShutdownStarted,
			TransportAbstractionsEventId.BeginningShutdownSequence,
			TransportAbstractionsEventId.GracefulShutdownCompleted,
			TransportAbstractionsEventId.OperationRegistered,
			TransportAbstractionsEventId.OperationCompleted,
			TransportAbstractionsEventId.WaitingForInFlightOperations,
			TransportAbstractionsEventId.ShutdownTimeoutExceeded,
			TransportAbstractionsEventId.ExecutingShutdownHook,
			TransportAbstractionsEventId.ShutdownHookError,
			TransportAbstractionsEventId.HpaMetricsProviderStarted,
			TransportAbstractionsEventId.HpaMetricRegistered,
			TransportAbstractionsEventId.HpaMetricsUpdateError,
			TransportAbstractionsEventId.CgroupV1MemoryLimitReadError,
			TransportAbstractionsEventId.CgroupV1MemoryLimitAccessDenied,
			TransportAbstractionsEventId.CgroupV2MemoryLimitReadError,
			TransportAbstractionsEventId.CgroupV2MemoryLimitAccessDenied,
			TransportAbstractionsEventId.ConfigMapEvent,
			TransportAbstractionsEventId.SecretEvent,
			TransportAbstractionsEventId.ConfigurationChangeDetected,
			TransportAbstractionsEventId.ReloadCallbackError,
			TransportAbstractionsEventId.WatchError,

			// ServiceMesh (20200-20299)
			TransportAbstractionsEventId.ServiceMeshMiddlewareError,
			TransportAbstractionsEventId.ServiceTopologyLogged,
			TransportAbstractionsEventId.TopologyRecordingFailed,
			TransportAbstractionsEventId.ServiceMeshMessageProcessed,
			TransportAbstractionsEventId.TrafficRequestCompleted,
			TransportAbstractionsEventId.TrafficDefaultPolicySet,
			TransportAbstractionsEventId.TrafficSplitRouting,
			TransportAbstractionsEventId.EnvoyGetClusterInfoFailed,
			TransportAbstractionsEventId.EnvoyRuntimeConfigUpdated,
			TransportAbstractionsEventId.EnvoyUpdateRuntimeConfigFailed,
			TransportAbstractionsEventId.EnvoyFlushStatsFailed,
			TransportAbstractionsEventId.EnvoyGetLoadBalancingStatsFailed,
			TransportAbstractionsEventId.CircuitManuallyOpened,
			TransportAbstractionsEventId.CircuitReset,
			TransportAbstractionsEventId.CircuitRequestSucceeded,
			TransportAbstractionsEventId.CircuitBreakerOpen,
			TransportAbstractionsEventId.CircuitRequestFailed,
			TransportAbstractionsEventId.CircuitBreakerOpened,
			TransportAbstractionsEventId.CircuitBreakerReset,
			TransportAbstractionsEventId.CircuitBreakerHalfOpen,

			// Service Discovery (20300-20399)
			TransportAbstractionsEventId.NoEndpointsFound,
			TransportAbstractionsEventId.NoHealthyEndpointsFound,
			TransportAbstractionsEventId.DiscoveredEndpoints,
			TransportAbstractionsEventId.ServiceRegistered,
			TransportAbstractionsEventId.ServiceDeregistered,
			TransportAbstractionsEventId.HealthStatusUpdated,
			TransportAbstractionsEventId.HealthCheckError,
			TransportAbstractionsEventId.EndpointHealthChanged,
			TransportAbstractionsEventId.HealthCheckFailed,
			TransportAbstractionsEventId.ConsulRetrievedServiceEndpoints,
			TransportAbstractionsEventId.ConsulFailedToGetServiceEndpoints,
			TransportAbstractionsEventId.ConsulRetrievedAllEndpoints,
			TransportAbstractionsEventId.ConsulFailedToGetAllEndpoints,
			TransportAbstractionsEventId.ConsulServiceRegistrationNotFound,
			TransportAbstractionsEventId.ConsulFailedToGetServiceRegistration,
			TransportAbstractionsEventId.ConsulServiceRegistered,
			TransportAbstractionsEventId.ConsulFailedToRegisterService,
			TransportAbstractionsEventId.ConsulServiceDeregistered,
			TransportAbstractionsEventId.ConsulFailedToDeregisterService,
			TransportAbstractionsEventId.ConsulHealthStatusUpdated,
			TransportAbstractionsEventId.ConsulFailedToUpdateHealthStatus,
			TransportAbstractionsEventId.ConsulServiceChanged,
			TransportAbstractionsEventId.ConsulWatchingServiceError,

			// Traffic Management (20400-20499)
			TransportAbstractionsEventId.TrafficManagerStarted,
			TransportAbstractionsEventId.TrafficRouted,
			TransportAbstractionsEventId.CircuitBreakerProviderCreated,
			TransportAbstractionsEventId.TrafficThrottled,

			// TLS Security (20500-20599)
			TransportAbstractionsEventId.MtlsHttpClientConfigured,
			TransportAbstractionsEventId.MtlsClientCertificateAdded,
			TransportAbstractionsEventId.MtlsValidatingPeerCertificate,
			TransportAbstractionsEventId.MtlsSslPolicyErrors,
			TransportAbstractionsEventId.MtlsChainValidationFailed,
			TransportAbstractionsEventId.MtlsAttributeValidationFailed,
			TransportAbstractionsEventId.MtlsCustomValidationFailed,
			TransportAbstractionsEventId.MtlsValidationSuccessful,
			TransportAbstractionsEventId.MtlsRefreshingCertificate,
			TransportAbstractionsEventId.MtlsCertificateRefreshed,
			TransportAbstractionsEventId.MtlsRefreshFailed,
			TransportAbstractionsEventId.MtlsNoCertificatePresented,
			TransportAbstractionsEventId.MtlsChainValidationError,
			TransportAbstractionsEventId.MtlsCertificateExpired,
			TransportAbstractionsEventId.MtlsCertificateNotYetValid,
			TransportAbstractionsEventId.MtlsValidatingSan,
			TransportAbstractionsEventId.MtlsMissingDigitalSignature,

			// Connection Pooling (20600-20699)
			TransportAbstractionsEventId.ConnectionPoolInitialized,
			TransportAbstractionsEventId.ConnectionAcquired,
			TransportAbstractionsEventId.ConnectionReturned,
			TransportAbstractionsEventId.ConnectionPoolWarmupStart,
			TransportAbstractionsEventId.ConnectionPoolWarmupComplete,
			TransportAbstractionsEventId.ConnectionPoolDisposing,
			TransportAbstractionsEventId.ConnectionPoolDisposed,
			TransportAbstractionsEventId.ConnectionCreated,
			TransportAbstractionsEventId.ConnectionCreationError,
			TransportAbstractionsEventId.ConnectionDestroyed,
			TransportAbstractionsEventId.ConnectionDestroyError,
			TransportAbstractionsEventId.ConnectionIdleTimeout,
			TransportAbstractionsEventId.ConnectionHealthCheckError,
			TransportAbstractionsEventId.HealthCheckConnectionCreationError,
			TransportAbstractionsEventId.PoolHealthCheckError,

			// Session Management (20700-20799)
			TransportAbstractionsEventId.SessionLockAcquired,
			TransportAbstractionsEventId.SessionLockExtended,
			TransportAbstractionsEventId.SessionLockReleased,
			TransportAbstractionsEventId.SessionCreated,
			TransportAbstractionsEventId.SessionOpened,
			TransportAbstractionsEventId.SessionClosed,
			TransportAbstractionsEventId.SessionRenewed,
			TransportAbstractionsEventId.SessionAbandoned,
			TransportAbstractionsEventId.SessionsCleanedUp,
			TransportAbstractionsEventId.SessionStateSet,
			TransportAbstractionsEventId.SessionStateDeleted,
			TransportAbstractionsEventId.SessionCheckpointCreated,
			TransportAbstractionsEventId.SessionCheckpointRestored,
			TransportAbstractionsEventId.SessionLockUpgraded,
			TransportAbstractionsEventId.SessionLockDowngraded,
			TransportAbstractionsEventId.SessionLockBroken,

			// Batch Processing (20800-20899)
			TransportAbstractionsEventId.BatchProcessingFailed,
			TransportAbstractionsEventId.BatchProcessed,
			TransportAbstractionsEventId.BatchProcessorConfigured,
			TransportAbstractionsEventId.BatchChannelClosed,
			TransportAbstractionsEventId.BatchStoppingOnError,
			TransportAbstractionsEventId.BatchMessageFailedAfterRetries,
			TransportAbstractionsEventId.BatchMessageRetrying,

			// Dead Letter Queue (20900-20999)
			TransportAbstractionsEventId.MessageMovedToDeadLetter,
			TransportAbstractionsEventId.DeadLetterQueueProcessed,
			TransportAbstractionsEventId.DeadLetterReprocessingStarted,
			TransportAbstractionsEventId.DeadLetterQueueCleared,
			TransportAbstractionsEventId.DeadLetterException,
			TransportAbstractionsEventId.DeadLetterParseFailed,
			TransportAbstractionsEventId.DeadLetterMessagesRetrieved,
			TransportAbstractionsEventId.DeadLetterReprocessFailed,
			TransportAbstractionsEventId.DeadLetterReprocessSummary,
			TransportAbstractionsEventId.DeadLetterPurgeFailed,
			TransportAbstractionsEventId.DeadLetterPurgeCompleted,
			TransportAbstractionsEventId.DeadLetterMessageReprocessed
		];
	}

	#endregion
}
