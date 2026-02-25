// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Diagnostics;

/// <summary>
/// Event IDs for Excalibur hosting infrastructure (160000-162999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>160000-160499: Hosting Core</item>
/// <item>160500-160999: Worker Services</item>
/// <item>161000-161499: Health Checks</item>
/// <item>161500-161999: Configuration</item>
/// <item>162000-162499: Leader Election</item>
/// <item>162500-162999: Web Hosting</item>
/// </list>
/// </remarks>
public static class ExcaliburHostingEventId
{
	// ========================================
	// 160000-160099: Hosting Core
	// ========================================

	/// <summary>Host builder created.</summary>
	public const int HostBuilderCreated = 160000;

	/// <summary>Host starting.</summary>
	public const int HostStarting = 160001;

	/// <summary>Host started.</summary>
	public const int HostStarted = 160002;

	/// <summary>Host stopping.</summary>
	public const int HostStopping = 160003;

	/// <summary>Host stopped.</summary>
	public const int HostStopped = 160004;

	/// <summary>Host error occurred.</summary>
	public const int HostError = 160005;

	// ========================================
	// 160100-160199: Service Registration
	// ========================================

	/// <summary>Services configured.</summary>
	public const int ServicesConfigured = 160100;

	/// <summary>Service registered.</summary>
	public const int ServiceRegistered = 160101;

	/// <summary>Service resolved.</summary>
	public const int ServiceResolved = 160102;

	/// <summary>Service disposed.</summary>
	public const int ServiceDisposed = 160103;

	// ========================================
	// 160500-160599: Worker Services Core
	// ========================================

	/// <summary>Worker service created.</summary>
	public const int WorkerServiceCreated = 160500;

	/// <summary>Worker service started.</summary>
	public const int WorkerServiceStarted = 160501;

	/// <summary>Worker service stopped.</summary>
	public const int WorkerServiceStopped = 160502;

	/// <summary>Worker iteration completed.</summary>
	public const int WorkerIterationCompleted = 160503;

	/// <summary>Worker error occurred.</summary>
	public const int WorkerError = 160504;

	// ========================================
	// 160600-160699: Background Tasks
	// ========================================

	/// <summary>Background task started.</summary>
	public const int BackgroundTaskStarted = 160600;

	/// <summary>Background task completed.</summary>
	public const int BackgroundTaskCompleted = 160601;

	/// <summary>Background task failed.</summary>
	public const int BackgroundTaskFailed = 160602;

	/// <summary>Background task cancelled.</summary>
	public const int BackgroundTaskCancelled = 160603;

	// ========================================
	// 161000-161099: Health Checks Core
	// ========================================

	/// <summary>Health check service created.</summary>
	public const int HealthCheckServiceCreated = 161000;

	/// <summary>Health check executed.</summary>
	public const int HealthCheckExecuted = 161001;

	/// <summary>Health check passed.</summary>
	public const int HealthCheckPassed = 161002;

	/// <summary>Health check failed.</summary>
	public const int HealthCheckFailed = 161003;

	/// <summary>Health status changed.</summary>
	public const int HealthStatusChanged = 161004;

	// ========================================
	// 161100-161199: Health Check Types
	// ========================================

	/// <summary>Liveness check executed.</summary>
	public const int LivenessCheckExecuted = 161100;

	/// <summary>Readiness check executed.</summary>
	public const int ReadinessCheckExecuted = 161101;

	/// <summary>Startup check executed.</summary>
	public const int StartupCheckExecuted = 161102;

	/// <summary>Dependency check executed.</summary>
	public const int DependencyCheckExecuted = 161103;

	// ========================================
	// 161500-161599: Configuration Core
	// ========================================

	/// <summary>Configuration loaded.</summary>
	public const int ConfigurationLoaded = 161500;

	/// <summary>Configuration validated.</summary>
	public const int ConfigurationValidated = 161501;

	/// <summary>Configuration changed.</summary>
	public const int ConfigurationChanged = 161502;

	/// <summary>Configuration error.</summary>
	public const int ConfigurationError = 161503;

	/// <summary>Environment detected.</summary>
	public const int EnvironmentDetected = 161504;

	// ========================================
	// 162000-162099: Leader Election Core
	// ========================================

	/// <summary>Leader election service created.</summary>
	public const int LeaderElectionServiceCreated = 162000;

	/// <summary>Leader election started.</summary>
	public const int LeaderElectionStarted = 162001;

	/// <summary>Leadership acquired.</summary>
	public const int LeadershipAcquired = 162002;

	/// <summary>Leadership lost.</summary>
	public const int LeadershipLost = 162003;

	/// <summary>Leadership renewed.</summary>
	public const int LeadershipRenewed = 162004;

	// ========================================
	// 162100-162199: Leader Election Providers
	// ========================================

	/// <summary>Redis leader election created.</summary>
	public const int RedisLeaderElectionCreated = 162100;

	/// <summary>SQL Server leader election created.</summary>
	public const int SqlServerLeaderElectionCreated = 162101;

	/// <summary>Consul leader election created.</summary>
	public const int ConsulLeaderElectionCreated = 162102;

	/// <summary>Kubernetes leader election created.</summary>
	public const int KubernetesLeaderElectionCreated = 162103;

	/// <summary>In-memory leader election created.</summary>
	public const int InMemoryLeaderElectionCreated = 162104;

	// ========================================
	// 162500-162599: Web Hosting
	// ========================================

	/// <summary>Web host created.</summary>
	public const int WebHostCreated = 162500;

	/// <summary>Web host started.</summary>
	public const int WebHostStarted = 162501;

	/// <summary>Web host stopped.</summary>
	public const int WebHostStopped = 162502;

	/// <summary>Request pipeline configured.</summary>
	public const int RequestPipelineConfigured = 162503;

	/// <summary>Endpoints mapped.</summary>
	public const int EndpointsMapped = 162504;

	// ========================================
	// 162600-162699: Serverless Hosting
	// ========================================

	/// <summary>Serverless hosting service starting.</summary>
	public const int ServerlessServiceStarting = 162600;

	/// <summary>Serverless provider unavailable.</summary>
	public const int ServerlessProviderUnavailable = 162601;

	/// <summary>Serverless provider ready.</summary>
	public const int ServerlessProviderReady = 162602;

	/// <summary>Serverless hosting service stopping.</summary>
	public const int ServerlessServiceStopping = 162603;

	/// <summary>Serverless platform provider selected.</summary>
	public const int ServerlessPlatformSelected = 162604;

	/// <summary>Serverless platform fallback occurred.</summary>
	public const int ServerlessPlatformFallback = 162605;

	/// <summary>Unable to detect serverless platform from environment.</summary>
	public const int ServerlessPlatformUndetected = 162606;

	/// <summary>Serverless provider registered.</summary>
	public const int ServerlessProviderRegistered = 162607;

	// ========================================
	// 162700-162799: AWS Lambda Provider
	// ========================================

	/// <summary>Configuring services for AWS Lambda.</summary>
	public const int LambdaConfiguringServices = 162700;

	/// <summary>AWS Lambda services configured successfully.</summary>
	public const int LambdaServicesConfigured = 162701;

	/// <summary>Configuring host for AWS Lambda.</summary>
	public const int LambdaConfiguringHost = 162702;

	/// <summary>AWS Lambda host configured successfully.</summary>
	public const int LambdaHostConfigured = 162703;

	/// <summary>Executing AWS Lambda handler.</summary>
	public const int LambdaExecutingHandler = 162704;

	/// <summary>AWS Lambda handler executed successfully.</summary>
	public const int LambdaHandlerExecuted = 162705;

	/// <summary>AWS Lambda execution cancelled by external token.</summary>
	public const int LambdaExecutionCancelled = 162706;

	/// <summary>AWS Lambda execution timed out.</summary>
	public const int LambdaExecutionTimedOut = 162707;

	/// <summary>AWS Lambda handler execution failed.</summary>
	public const int LambdaHandlerFailed = 162708;

	/// <summary>Configuring AWS X-Ray tracing for Lambda.</summary>
	public const int LambdaConfiguringXRay = 162709;

	/// <summary>Configuring AWS Lambda metrics.</summary>
	public const int LambdaConfiguringMetrics = 162710;

	// ========================================
	// 162800-162899: Azure Functions Provider
	// ========================================

	/// <summary>Configuring services for Azure Functions.</summary>
	public const int AzFuncConfiguringServices = 162800;

	/// <summary>Azure Functions services configured successfully.</summary>
	public const int AzFuncServicesConfigured = 162801;

	/// <summary>Azure Functions support not available.</summary>
	public const int AzFuncNotAvailable = 162802;

	/// <summary>Configuring host for Azure Functions.</summary>
	public const int AzFuncConfiguringHost = 162803;

	/// <summary>Azure Functions host configured successfully.</summary>
	public const int AzFuncHostConfigured = 162804;

	/// <summary>Executing Azure Functions handler.</summary>
	public const int AzFuncExecutingHandler = 162805;

	/// <summary>Azure Functions handler executed successfully.</summary>
	public const int AzFuncHandlerExecuted = 162806;

	/// <summary>Azure Functions execution cancelled by external token.</summary>
	public const int AzFuncExecutionCancelled = 162807;

	/// <summary>Azure Functions execution timed out.</summary>
	public const int AzFuncExecutionTimedOut = 162808;

	/// <summary>Azure Functions handler execution failed.</summary>
	public const int AzFuncHandlerFailed = 162809;

	/// <summary>Configuring Application Insights for Azure Functions.</summary>
	public const int AzFuncConfiguringAppInsights = 162810;

	/// <summary>Configuring Azure Functions metrics.</summary>
	public const int AzFuncConfiguringMetrics = 162811;

	/// <summary>Configuring Durable Functions.</summary>
	public const int AzFuncConfiguringDurable = 162812;

	// ========================================
	// 162830-162849: Azure Functions Saga Orchestration
	// ========================================

	/// <summary>Saga orchestration started.</summary>
	public const int SagaOrchestrationStarted = 162830;

	/// <summary>Saga orchestration completed.</summary>
	public const int SagaOrchestrationCompleted = 162831;

	/// <summary>Saga orchestration failed.</summary>
	public const int SagaOrchestrationFailed = 162832;

	/// <summary>Saga compensation failed.</summary>
	public const int SagaCompensationFailed = 162833;

	/// <summary>Saga step skipping.</summary>
	public const int SagaStepSkipping = 162834;

	/// <summary>Saga step executing.</summary>
	public const int SagaStepExecuting = 162835;

	/// <summary>Saga compensation started.</summary>
	public const int SagaCompensationStarted = 162836;

	/// <summary>Saga compensation step executing.</summary>
	public const int SagaCompensationStepExecuting = 162837;

	/// <summary>Saga compensation step failed.</summary>
	public const int SagaCompensationStepFailed = 162838;

	/// <summary>Saga compensation completed.</summary>
	public const int SagaCompensationCompleted = 162839;

	/// <summary>Saga step retry starting.</summary>
	public const int SagaStepRetryStarting = 162840;

	/// <summary>Saga step retry succeeded.</summary>
	public const int SagaStepRetrySucceeded = 162841;

	/// <summary>Saga step retry failed.</summary>
	public const int SagaStepRetryFailed = 162842;

	/// <summary>Saga retry exhausted.</summary>
	public const int SagaRetryExhausted = 162843;

	/// <summary>Saga retry delay applied.</summary>
	public const int SagaRetryDelayApplied = 162844;

	/// <summary>Azure Functions cold start optimization disabled.</summary>
	public const int AzFuncColdStartDisabled = 162820;

	/// <summary>Azure Functions cold start optimization starting.</summary>
	public const int AzFuncColdStartStarting = 162821;

	/// <summary>Azure Functions cold start optimization completed.</summary>
	public const int AzFuncColdStartCompleted = 162822;

	/// <summary>Warming up Azure Functions services.</summary>
	public const int AzFuncWarmingUpServices = 162823;

	/// <summary>Azure Functions services warmed up.</summary>
	public const int AzFuncServicesWarmedUp = 162824;

	// ========================================
	// 162900-162999: Google Cloud Functions Provider
	// ========================================

	/// <summary>Configuring services for Google Cloud Functions.</summary>
	public const int GcfConfiguringServices = 162900;

	/// <summary>Google Cloud Functions services configured successfully.</summary>
	public const int GcfServicesConfigured = 162901;

	/// <summary>Configuring host for Google Cloud Functions.</summary>
	public const int GcfConfiguringHost = 162902;

	/// <summary>Google Cloud Functions host configured successfully.</summary>
	public const int GcfHostConfigured = 162903;

	/// <summary>Executing Google Cloud Functions handler.</summary>
	public const int GcfExecutingHandler = 162904;

	/// <summary>Google Cloud Functions handler executed successfully.</summary>
	public const int GcfHandlerExecuted = 162905;

	/// <summary>Google Cloud Functions execution cancelled by external token.</summary>
	public const int GcfExecutionCancelled = 162906;

	/// <summary>Google Cloud Functions execution timed out.</summary>
	public const int GcfExecutionTimedOut = 162907;

	/// <summary>Google Cloud Functions handler execution failed.</summary>
	public const int GcfHandlerFailed = 162908;

	/// <summary>Configuring Google Cloud Trace.</summary>
	public const int GcfConfiguringTrace = 162909;

	/// <summary>Configuring Google Cloud Monitoring.</summary>
	public const int GcfConfiguringMetrics = 162910;

	/// <summary>Google Cloud Functions cold start optimization disabled.</summary>
	public const int GcfColdStartDisabled = 162920;

	/// <summary>Google Cloud Functions cold start optimization starting.</summary>
	public const int GcfColdStartStarting = 162921;

	/// <summary>Google Cloud Functions cold start optimization completed.</summary>
	public const int GcfColdStartCompleted = 162922;

	/// <summary>Warming up Google Cloud Functions services.</summary>
	public const int GcfWarmingUpServices = 162923;

	/// <summary>Google Cloud Functions services warmed up.</summary>
	public const int GcfServicesWarmedUp = 162924;

	// ========================================
	// 162720-162799: AWS Lambda Cold Start
	// ========================================

	/// <summary>AWS Lambda cold start optimization disabled.</summary>
	public const int LambdaColdStartDisabled = 162720;

	/// <summary>AWS Lambda cold start optimization starting.</summary>
	public const int LambdaColdStartStarting = 162721;

	/// <summary>AWS Lambda cold start optimization completed.</summary>
	public const int LambdaColdStartCompleted = 162722;

	/// <summary>Warming up AWS Lambda services.</summary>
	public const int LambdaWarmingUpServices = 162723;

	/// <summary>AWS Lambda services warmed up.</summary>
	public const int LambdaServicesWarmedUp = 162724;

	// ========================================
	// 161510-161549: Configuration Validation
	// ========================================

	/// <summary>Configuration validation is disabled.</summary>
	public const int ConfigValidationDisabled = 161510;

	/// <summary>Configuration validation starting.</summary>
	public const int ConfigValidationStarting = 161511;

	/// <summary>Running configuration validator.</summary>
	public const int ConfigValidatorRunning = 161512;

	/// <summary>Configuration validator reported errors.</summary>
	public const int ConfigValidatorErrors = 161513;

	/// <summary>Configuration validation error detail.</summary>
	public const int ConfigValidationErrorDetail = 161514;

	/// <summary>Configuration validator passed.</summary>
	public const int ConfigValidatorPassed = 161515;

	/// <summary>Configuration validator threw an exception.</summary>
	public const int ConfigValidatorException = 161516;

	/// <summary>Configuration validator exception ignored.</summary>
	public const int ConfigValidatorExceptionIgnored = 161517;

	/// <summary>Configuration validation complete.</summary>
	public const int ConfigValidationComplete = 161518;

	/// <summary>Configuration validation failed, terminating.</summary>
	public const int ConfigValidationFailedTerminating = 161519;

	/// <summary>Configuration validation errors detected.</summary>
	public const int ConfigValidationErrorsDetected = 161520;

	/// <summary>All configuration validators passed.</summary>
	public const int ConfigAllValidatorsPassed = 161521;

	// ========================================
	// 162510-162519: Global Exception Handler
	// ========================================

	/// <summary>Global exception occurred.</summary>
	public const int GlobalExceptionOccurred = 162510;
}
