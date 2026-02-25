// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Event IDs for serverless hosting abstractions (50000-50099).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>50000-50049: Serverless Host Provider</item>
/// <item>50050-50099: Serverless Host Factory</item>
/// </list>
/// </remarks>
public static class ServerlessEventId
{
	// ========================================
	// 50000-50049: Serverless Host Provider
	// ========================================

	/// <summary>Serverless hosting service starting.</summary>
	public const int HostingServiceStarting = 50000;

	/// <summary>Serverless hosting service stopping.</summary>
	public const int HostingServiceStopping = 50001;

	/// <summary>Serverless provider unavailable.</summary>
	public const int ProviderUnavailable = 50002;

	/// <summary>Serverless provider ready.</summary>
	public const int ProviderReady = 50003;

	/// <summary>Serverless context created.</summary>
	public const int ContextCreated = 50004;

	/// <summary>Serverless execution started.</summary>
	public const int ExecutionStarted = 50005;

	/// <summary>Serverless execution completed.</summary>
	public const int ExecutionCompleted = 50006;

	/// <summary>Serverless execution failed.</summary>
	public const int ExecutionFailed = 50007;

	/// <summary>Serverless execution timed out.</summary>
	public const int ExecutionTimedOut = 50008;

	/// <summary>Serverless execution cancelled.</summary>
	public const int ExecutionCancelled = 50009;

	// ========================================
	// 50050-50099: Serverless Host Factory
	// ========================================

	/// <summary>Serverless host provider factory created.</summary>
	public const int ProviderFactoryCreated = 50050;

	/// <summary>Serverless host provider registered.</summary>
	public const int ProviderRegistered = 50051;

	/// <summary>Serverless host provider resolved.</summary>
	public const int ProviderResolved = 50052;

	/// <summary>Cold start optimization enabled.</summary>
	public const int ColdStartOptimizationEnabled = 50053;

	/// <summary>Cold start optimization completed.</summary>
	public const int ColdStartOptimizationCompleted = 50054;

	/// <summary>Serverless platform selected.</summary>
	public const int PlatformSelected = 50055;

	/// <summary>Serverless platform fallback.</summary>
	public const int PlatformFallback = 50056;

	/// <summary>Unable to detect serverless platform.</summary>
	public const int UnableToDetectPlatform = 50057;
}
