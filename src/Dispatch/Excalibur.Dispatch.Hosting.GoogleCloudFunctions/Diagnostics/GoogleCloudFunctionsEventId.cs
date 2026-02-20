// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.GoogleCloudFunctions;

/// <summary>
/// Event IDs for Google Cloud Functions hosting (50400-50499).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>50400-50449: Google Cloud Functions Host Provider</item>
/// <item>50450-50499: Google Cloud Functions Cold Start Optimization</item>
/// </list>
/// </remarks>
public static class GoogleCloudFunctionsEventId
{
	// ========================================
	// 50400-50449: Google Cloud Functions Host Provider
	// ========================================

	/// <summary>Configuring services for Google Cloud Functions.</summary>
	public const int ConfiguringServices = 50400;

	/// <summary>Google Cloud Functions services configured successfully.</summary>
	public const int ServicesConfigured = 50401;

	/// <summary>Configuring host for Google Cloud Functions.</summary>
	public const int ConfiguringHost = 50402;

	/// <summary>Google Cloud Functions host configured successfully.</summary>
	public const int HostConfigured = 50403;

	/// <summary>Executing Google Cloud Functions handler.</summary>
	public const int ExecutingHandler = 50404;

	/// <summary>Google Cloud Functions handler executed successfully.</summary>
	public const int HandlerExecuted = 50405;

	/// <summary>Google Cloud Functions execution cancelled by external token.</summary>
	public const int ExecutionCancelled = 50406;

	/// <summary>Google Cloud Functions execution timed out.</summary>
	public const int ExecutionTimedOut = 50407;

	/// <summary>Google Cloud Functions handler execution failed.</summary>
	public const int HandlerFailed = 50408;

	/// <summary>Configuring Cloud Trace for Google Cloud Functions.</summary>
	public const int ConfiguringCloudTrace = 50409;

	/// <summary>Configuring Cloud Monitoring for Google Cloud Functions.</summary>
	public const int ConfiguringCloudMonitoring = 50410;

	// ========================================
	// 50450-50499: Google Cloud Functions Cold Start Optimization
	// ========================================

	/// <summary>Google Cloud Functions cold start optimization started.</summary>
	public const int ColdStartOptimizationStarted = 50450;

	/// <summary>Google Cloud Functions cold start optimization completed.</summary>
	public const int ColdStartOptimizationCompleted = 50451;

	/// <summary>Google Cloud Functions warm instance detected.</summary>
	public const int WarmInstanceDetected = 50452;

	/// <summary>Google Cloud Functions cold instance detected.</summary>
	public const int ColdInstanceDetected = 50453;

	/// <summary>Google Cloud Functions minimum instances configured.</summary>
	public const int MinimumInstancesConfigured = 50454;

	/// <summary>Cold start optimization disabled.</summary>
	public const int ColdStartOptimizationDisabled = 50455;

	/// <summary>Warming up services.</summary>
	public const int WarmingUpServices = 50456;

	/// <summary>Services warmed up.</summary>
	public const int ServicesWarmedUp = 50457;

	/// <summary>DI singleton service warmup starting.</summary>
	public const int SingletonWarmupStarting = 50458;

	/// <summary>DI singleton service warmup completed.</summary>
	public const int SingletonWarmupCompleted = 50459;

	/// <summary>DI singleton service warmup failed.</summary>
	public const int SingletonWarmupFailed = 50460;

	/// <summary>GCP SDK client warmup starting.</summary>
	public const int GcpSdkWarmupStarting = 50461;

	/// <summary>GCP SDK client warmup completed.</summary>
	public const int GcpSdkWarmupCompleted = 50462;

	/// <summary>GCP SDK client warmup failed.</summary>
	public const int GcpSdkWarmupFailed = 50463;

	/// <summary>Cloud Trace is enabled.</summary>
	public const int CloudTraceEnabled = 50464;

	/// <summary>JIT compilation warmup starting.</summary>
	public const int JitWarmupStarting = 50465;

	/// <summary>JIT compilation warmup completed.</summary>
	public const int JitWarmupCompleted = 50466;

	/// <summary>JIT compilation warmup failed.</summary>
	public const int JitWarmupFailed = 50467;
}
