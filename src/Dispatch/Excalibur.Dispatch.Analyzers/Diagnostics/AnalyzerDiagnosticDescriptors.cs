// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.Analyzers;

/// <summary>
/// Central diagnostic descriptors for Dispatch namespace and convention analyzers.
/// </summary>
/// <remarks>
/// <para>
/// Diagnostic ID range: DISP100-DISP199 (reserved for convention analyzers).
/// DISP001-DISP099 are used by SourceGenerators.Analyzers for handler/AOT diagnostics.
/// </para>
/// </remarks>
internal static class AnalyzerDiagnosticDescriptors
{
    /// <summary>
    /// Category for namespace convention diagnostics.
    /// </summary>
    private const string NamingCategory = "Excalibur.Dispatch.Naming";

    /// <summary>
    /// Category for API design diagnostics.
    /// </summary>
    private const string DesignCategory = "Excalibur.Dispatch.Design";

    /// <summary>
    /// DISP101: DI extension class in wrong namespace.
    /// </summary>
    /// <remarks>
    /// Warning when a ServiceCollectionExtensions class is not in the
    /// <c>Microsoft.Extensions.DependencyInjection</c> namespace, as required by .NET conventions.
    /// </remarks>
    public static readonly DiagnosticDescriptor DiExtensionWrongNamespace = new(
        id: "DISP101",
        title: "DI extension class should be in Microsoft.Extensions.DependencyInjection namespace",
        messageFormat: "Class '{0}' contains IServiceCollection extension methods but is in namespace '{1}'. Move to 'Microsoft.Extensions.DependencyInjection' for discoverability.",
        category: NamingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ServiceCollection extension methods should be in the Microsoft.Extensions.DependencyInjection namespace so consumers discover them via IntelliSense without extra using directives. This follows Microsoft's own convention for first-party packages.",
        helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/DISP101");

    /// <summary>
    /// DISP102: Extension class has interface-style 'I' prefix.
    /// </summary>
    /// <remarks>
    /// Warning when a static extension class name starts with 'I' followed by an uppercase letter,
    /// which is reserved for interfaces in .NET naming conventions.
    /// </remarks>
    public static readonly DiagnosticDescriptor ExtensionClassIPrefixNaming = new(
        id: "DISP102",
        title: "Extension class should not use 'I' prefix",
        messageFormat: "Extension class '{0}' uses interface-style 'I' prefix. Rename to '{1}' to follow .NET naming conventions.",
        category: NamingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Static extension classes should not start with 'I' followed by an uppercase letter. The 'I' prefix is reserved for interfaces per .NET Framework Design Guidelines. Rename the class to drop the 'I' prefix (e.g., IDispatcherExtensions → DispatcherExtensions).",
        helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/DISP102");

    /// <summary>
    /// DISP103: CancellationToken parameter has default value in interface.
    /// </summary>
    /// <remarks>
    /// Warning when an interface method declares <c>CancellationToken cancellationToken = default</c>.
    /// Framework interfaces should require cancellation token parameters so consumers cannot
    /// accidentally omit them.
    /// </remarks>
    public static readonly DiagnosticDescriptor CancellationTokenOptionalInInterface = new(
        id: "DISP103",
        title: "CancellationToken should not have default value in interface",
        messageFormat: "Parameter '{0}' in '{1}.{2}' should not have a default value. Framework interfaces must require CancellationToken to prevent accidental omission.",
        category: DesignCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "CancellationToken parameters in framework interfaces should be required (no default value). Optional CancellationToken hides cancellation support from consumers and makes it easy to accidentally create non-cancellable call chains.",
        helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/DISP103");

    /// <summary>
    /// DISP104: Namespace contains '.Core.' segment.
    /// </summary>
    /// <remarks>
    /// Warning when a namespace contains a '.Core.' segment, which violates
    /// the framework's namespace convention (ADR-075).
    /// </remarks>
    public static readonly DiagnosticDescriptor NamespaceContainsCoreSegment = new(
        id: "DISP104",
        title: "Namespace should not contain '.Core.' segment",
        messageFormat: "Namespace '{0}' contains a '.Core.' segment. Use a direct namespace like '{1}' instead.",
        category: NamingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Namespaces in the Excalibur framework must not contain a '.Core.' segment. This follows ADR-075 (.NET Best Practices) which prohibits '.Core.' in namespace paths.",
        helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/DISP104");

    /// <summary>
    /// DISP105: Missing ConfigureAwait(false) in library code.
    /// </summary>
    /// <remarks>
    /// Warning when an await expression in framework/library code does not use
    /// <c>ConfigureAwait(false)</c>, which can cause deadlocks in synchronization
    /// context-dependent hosting environments.
    /// </remarks>
    public static readonly DiagnosticDescriptor MissingConfigureAwait = new(
        id: "DISP105",
        title: "Await expression should use ConfigureAwait(false) in library code",
        messageFormat: "Add ConfigureAwait(false) to this await expression. Library code must not capture the synchronization context to avoid deadlocks.",
        category: DesignCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Framework and library code should always use ConfigureAwait(false) on await expressions. Without it, the continuation may be scheduled on the caller's synchronization context, causing deadlocks in UI or ASP.NET Classic hosting environments.",
        helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/DISP105");

    /// <summary>
    /// DISP106: Blocking call in async method.
    /// </summary>
    /// <remarks>
    /// Warning when an async method uses <c>.Result</c>, <c>.Wait()</c>, or
    /// <c>.GetAwaiter().GetResult()</c>, which can cause thread pool starvation
    /// in message handlers and middleware pipelines.
    /// </remarks>
    public static readonly DiagnosticDescriptor BlockingCallInAsyncMethod = new(
        id: "DISP106",
        title: "Avoid blocking calls in async methods",
        messageFormat: "Async method uses blocking call '.{0}'. Use 'await' instead to prevent thread pool starvation in dispatch handlers and middleware.",
        category: DesignCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Synchronous blocking calls (.Result, .Wait(), .GetAwaiter().GetResult()) in async methods can cause thread pool starvation, especially in high-throughput message dispatch pipelines. Use 'await' instead.",
        helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/DISP106");
}
