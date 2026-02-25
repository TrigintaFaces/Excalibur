// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.AwsLambda;

/// <summary>
/// Event IDs for AWS Lambda hosting (50100-50199).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>50100-50149: Lambda Host Provider</item>
/// <item>50150-50199: Lambda Cold Start Optimization</item>
/// </list>
/// </remarks>
public static class AwsLambdaEventId
{
	// ========================================
	// 50100-50149: Lambda Host Provider
	// ========================================

	/// <summary>Configuring services for AWS Lambda.</summary>
	public const int ConfiguringServices = 50100;

	/// <summary>AWS Lambda services configured successfully.</summary>
	public const int ServicesConfigured = 50101;

	/// <summary>Configuring host for AWS Lambda.</summary>
	public const int ConfiguringHost = 50102;

	/// <summary>AWS Lambda host configured successfully.</summary>
	public const int HostConfigured = 50103;

	/// <summary>Executing AWS Lambda handler.</summary>
	public const int ExecutingHandler = 50104;

	/// <summary>AWS Lambda handler executed successfully.</summary>
	public const int HandlerExecuted = 50105;

	/// <summary>AWS Lambda execution cancelled by external token.</summary>
	public const int ExecutionCancelled = 50106;

	/// <summary>AWS Lambda execution timed out.</summary>
	public const int ExecutionTimedOut = 50107;

	/// <summary>AWS Lambda handler execution failed.</summary>
	public const int HandlerFailed = 50108;

	/// <summary>Configuring AWS X-Ray tracing for Lambda.</summary>
	public const int ConfiguringXRayTracing = 50109;

	/// <summary>Configuring AWS Lambda metrics.</summary>
	public const int ConfiguringMetrics = 50110;

	// ========================================
	// 50150-50199: Lambda Cold Start Optimization
	// ========================================

	/// <summary>Lambda cold start optimization started.</summary>
	public const int ColdStartOptimizationStarted = 50150;

	/// <summary>Lambda cold start optimization completed.</summary>
	public const int ColdStartOptimizationCompleted = 50151;

	/// <summary>Lambda warm instance detected.</summary>
	public const int WarmInstanceDetected = 50152;

	/// <summary>Lambda cold instance detected.</summary>
	public const int ColdInstanceDetected = 50153;

	/// <summary>Lambda provisioned concurrency active.</summary>
	public const int ProvisionedConcurrencyActive = 50154;

	/// <summary>Cold start optimization disabled.</summary>
	public const int ColdStartOptimizationDisabled = 50155;

	/// <summary>Warming up services.</summary>
	public const int WarmingUpServices = 50156;

	/// <summary>Services warmed up.</summary>
	public const int ServicesWarmedUp = 50157;

	/// <summary>DI singleton service warmup starting.</summary>
	public const int SingletonWarmupStarting = 50158;

	/// <summary>DI singleton service warmup completed.</summary>
	public const int SingletonWarmupCompleted = 50159;

	/// <summary>DI singleton service warmup failed.</summary>
	public const int SingletonWarmupFailed = 50160;

	/// <summary>AWS SDK client warmup starting.</summary>
	public const int AwsSdkWarmupStarting = 50161;

	/// <summary>AWS SDK client warmup completed.</summary>
	public const int AwsSdkWarmupCompleted = 50162;

	/// <summary>AWS SDK client warmup failed.</summary>
	public const int AwsSdkWarmupFailed = 50163;

	/// <summary>X-Ray tracing is enabled.</summary>
	public const int XRayTracingEnabled = 50164;

	/// <summary>JIT compilation warmup starting.</summary>
	public const int JitWarmupStarting = 50165;

	/// <summary>JIT compilation warmup completed.</summary>
	public const int JitWarmupCompleted = 50166;

	/// <summary>JIT compilation warmup failed.</summary>
	public const int JitWarmupFailed = 50167;
}

