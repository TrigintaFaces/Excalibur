// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Analyzers;

/// <summary>
/// Central diagnostic descriptors for Dispatch analyzers.
/// </summary>
/// <remarks>
/// <para>
/// Diagnostic ID range: DISP001-DISP099 (reserved for analyzer diagnostics).
/// These do NOT overlap with LoggerMessage Event IDs (1-4999) or generator diagnostics (HND001+).
/// </para>
/// </remarks>
internal static class DiagnosticDescriptors
{
	/// <summary>
	/// Category for handler-related diagnostics.
	/// </summary>
	private const string HandlersCategory = "Excalibur.Dispatch.Messaging.Handlers";

	/// <summary>
	/// Category for AOT compatibility diagnostics.
	/// </summary>
	private const string CompatibilityCategory = "Excalibur.Dispatch.Compatibility";

	/// <summary>
	/// Category for performance-related diagnostics.
	/// </summary>
	private const string PerformanceCategory = "Excalibur.Dispatch.Performance";

	/// <summary>
	/// DISP001: Handler Not Discoverable.
	/// </summary>
	/// <remarks>
	/// Warning when a class implements IDispatchHandler&lt;T&gt; but may not be discovered
	/// by source generators (missing [AutoRegister] or not in scanned assembly).
	/// </remarks>
	public static readonly DiagnosticDescriptor HandlerNotDiscoverable = new(
		id: "DISP001",
		title: "Handler may not be discoverable",
		messageFormat: "Handler '{0}' implements {1} but may not be discovered by source generators",
		category: HandlersCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Handlers that implement IDispatchHandler<T> should be discoverable by the source generator. Use the [AutoRegister] attribute to ensure automatic registration.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/DISP001");

	/// <summary>
	/// DISP002: Missing AutoRegister Attribute.
	/// </summary>
	/// <remarks>
	/// Info when a handler could benefit from the [AutoRegister] attribute for automatic
	/// discovery and registration.
	/// </remarks>
	public static readonly DiagnosticDescriptor MissingAutoRegisterAttribute = new(
		id: "DISP002",
		title: "Consider adding [AutoRegister] attribute",
		messageFormat: "Handler '{0}' could benefit from [AutoRegister] attribute for automatic registration",
		category: HandlersCategory,
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "The [AutoRegister] attribute enables automatic handler discovery and registration by the source generator, reducing manual DI configuration.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/DISP002");

	/// <summary>
	/// DISP003: Reflection Without AOT Annotation.
	/// </summary>
	/// <remarks>
	/// Warning when reflection-based methods are used without proper AOT annotations.
	/// This may cause issues in Native AOT deployment scenarios.
	/// </remarks>
	public static readonly DiagnosticDescriptor ReflectionWithoutAotAnnotation = new(
		id: "DISP003",
		title: "Reflection usage without AOT annotation",
		messageFormat: "Method '{0}' uses {1} without AOT annotation",
		category: CompatibilityCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Reflection-based operations should be annotated with [RequiresDynamicCode] or [DynamicallyAccessedMembers] to ensure proper behavior in Native AOT scenarios.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/DISP003");

	/// <summary>
	/// DISP004: Optimization Hint.
	/// </summary>
	/// <remarks>
	/// Info for potential performance improvements such as sealing classes,
	/// using ValueTask, or applying readonly struct patterns.
	/// </remarks>
	public static readonly DiagnosticDescriptor OptimizationHint = new(
		id: "DISP004",
		title: "Optimization hint",
		messageFormat: "{0}",
		category: PerformanceCategory,
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		description: "Performance optimization suggestions to improve handler throughput and reduce allocations.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/DISP004");
}
