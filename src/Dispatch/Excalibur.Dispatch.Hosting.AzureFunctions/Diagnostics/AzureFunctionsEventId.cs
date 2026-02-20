// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.AzureFunctions;

/// <summary>
/// Event IDs for Azure Functions hosting (50200-50399).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>50200-50249: Azure Functions Host Provider</item>
/// <item>50250-50299: Azure Functions Cold Start Optimization</item>
/// <item>50300-50305: Durable Functions Orchestration (saga moved to Excalibur.Hosting)</item>
/// </list>
/// </remarks>
public static class AzureFunctionsEventId
{
	// ========================================
	// 50200-50249: Azure Functions Host Provider
	// ========================================

	/// <summary>Configuring services for Azure Functions.</summary>
	public const int ConfiguringServices = 50200;

	/// <summary>Azure Functions services configured successfully.</summary>
	public const int ServicesConfigured = 50201;

	/// <summary>Azure Functions support is not available.</summary>
	public const int SupportNotAvailable = 50202;

	/// <summary>Configuring host for Azure Functions.</summary>
	public const int ConfiguringHost = 50203;

	/// <summary>Azure Functions host configured successfully.</summary>
	public const int HostConfigured = 50204;

	/// <summary>Executing Azure Functions handler.</summary>
	public const int ExecutingHandler = 50205;

	/// <summary>Azure Functions handler executed successfully.</summary>
	public const int HandlerExecuted = 50206;

	/// <summary>Azure Functions execution cancelled by external token.</summary>
	public const int ExecutionCancelled = 50207;

	/// <summary>Azure Functions execution timed out.</summary>
	public const int ExecutionTimedOut = 50208;

	/// <summary>Azure Functions handler execution failed.</summary>
	public const int HandlerFailed = 50209;

	/// <summary>Configuring Application Insights for Azure Functions.</summary>
	public const int ConfiguringAppInsights = 50210;

	/// <summary>Configuring Azure Functions metrics.</summary>
	public const int ConfiguringMetrics = 50211;

	/// <summary>Configuring Durable Functions for Azure Functions.</summary>
	public const int ConfiguringDurableFunctions = 50212;

	// ========================================
	// 50250-50299: Azure Functions Cold Start Optimization
	// ========================================

	/// <summary>Azure Functions cold start optimization started.</summary>
	public const int ColdStartOptimizationStarted = 50250;

	/// <summary>Azure Functions cold start optimization completed.</summary>
	public const int ColdStartOptimizationCompleted = 50251;

	/// <summary>Azure Functions warm instance detected.</summary>
	public const int WarmInstanceDetected = 50252;

	/// <summary>Azure Functions cold instance detected.</summary>
	public const int ColdInstanceDetected = 50253;

	/// <summary>Azure Functions premium plan active.</summary>
	public const int PremiumPlanActive = 50254;

	// ========================================
	// 50300-50399: Durable Functions Orchestration (Reserved)
	// NOTE: Saga orchestration moved to Excalibur.Hosting (ExcaliburHostingEventId 162830-162849) in Sprint 507
	// ========================================

	/// <summary>Durable orchestration started.</summary>
	public const int OrchestrationStarted = 50300;

	/// <summary>Durable orchestration completed.</summary>
	public const int OrchestrationCompleted = 50301;

	/// <summary>Durable orchestration failed.</summary>
	public const int OrchestrationFailed = 50302;

	/// <summary>Durable activity started.</summary>
	public const int ActivityStarted = 50303;

	/// <summary>Durable activity completed.</summary>
	public const int ActivityCompleted = 50304;

	/// <summary>Durable activity failed.</summary>
	public const int ActivityFailed = 50305;

	// ========================================
	// 50255-50280: Additional Cold Start
	// ========================================

	/// <summary>Cold start optimization disabled.</summary>
	public const int ColdStartOptimizationDisabled = 50255;

	/// <summary>Warming up services.</summary>
	public const int WarmingUpServices = 50256;

	/// <summary>Services warmed up.</summary>
	public const int ServicesWarmedUp = 50257;

	/// <summary>DI singleton service warmup starting.</summary>
	public const int SingletonWarmupStarting = 50258;

	/// <summary>DI singleton service warmup completed.</summary>
	public const int SingletonWarmupCompleted = 50259;

	/// <summary>DI singleton service warmup failed.</summary>
	public const int SingletonWarmupFailed = 50260;

	/// <summary>Azure SDK client warmup starting.</summary>
	public const int AzureSdkWarmupStarting = 50261;

	/// <summary>Azure SDK client warmup completed.</summary>
	public const int AzureSdkWarmupCompleted = 50262;

	/// <summary>Azure SDK client warmup failed.</summary>
	public const int AzureSdkWarmupFailed = 50263;

	/// <summary>Application Insights is enabled.</summary>
	public const int AppInsightsEnabled = 50264;

	/// <summary>JIT compilation warmup starting.</summary>
	public const int JitWarmupStarting = 50265;

	/// <summary>JIT compilation warmup completed.</summary>
	public const int JitWarmupCompleted = 50266;

	/// <summary>JIT compilation warmup failed.</summary>
	public const int JitWarmupFailed = 50267;
}
