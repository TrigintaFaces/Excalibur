// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Excalibur.Dispatch.Migration.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Migration.Tests;

/// <summary>
/// End-to-end measurement of the MediatR migration codemod over a single <b>non-trivial</b> fixture app
/// that exercises every migration category at once — auto-fixable (EXMIG0001 registration, EXMIG0003
/// using-swap, EXMIG0004 handler-signature) and the deliberately-manual EXMIG0002 non-portable
/// constructs (pre/post processors, exception handler + action, stream pipeline behavior).
/// <para>
/// The per-category detection/fix behaviour is already locked by the sibling
/// <c>*AnalyzerShould</c>/<c>*CodeFixShould</c> tests. This measurement proves the codemod's behaviour
/// on a realistic app where every category is present together: it runs the four analyzers over the
/// fixture and asserts (a) it completes without throwing (<b>no crash</b>), (b) every planted migration
/// point produces its diagnostic (<b>no silent skip</b>), and (c) the exact split of auto-migrated vs
/// manual points — the numbers recorded in the accompanying report.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class MediatRMigrationCodemodMeasurementShould
{
    /// <summary>Minimal MediatR-shaped surface the fixture implements against (the analyzers are syntactic).</summary>
    private const string MediatRStubs = """
        namespace MediatR
        {
            public interface IRequest<out TResponse> { }
            public interface INotification { }
            public interface IStreamRequest<out TResponse> { }
            public interface IRequestHandler<in TRequest, TResponse> { }
            public interface INotificationHandler<in TNotification> { }
            public interface IStreamRequestHandler<in TRequest, out TResponse> { }
            public interface IPipelineBehavior<in TRequest, TResponse> { }
            public interface IRequestPreProcessor<in TRequest> { }
            public interface IRequestPostProcessor<in TRequest, in TResponse> { }
            public interface IRequestExceptionHandler<in TRequest, TResponse, in TException> { }
            public interface IRequestExceptionAction<in TRequest, in TException> { }
            public interface IStreamPipelineBehavior<in TRequest, TResponse> { }
        }

        namespace Microsoft.Extensions.DependencyInjection
        {
            using System.Reflection;
            public interface IServiceCollection { }
            public static class MediatRRegistration
            {
                public static IServiceCollection AddMediatR(this IServiceCollection services, Assembly assembly) => services;
            }
        }
        """;

    /// <summary>
    /// A non-trivial order-processing app: a request handler and a notification handler (both with the
    /// legacy <c>HandleAsync</c> name), a portable pipeline behavior, the <c>AddMediatR</c> registration,
    /// the <c>using MediatR;</c> import, and one of every non-portable construct MediatR supports.
    /// </summary>
    private const string AppSource = """
        using System.Collections.Generic;
        using System.Reflection;
        using System.Threading;
        using System.Threading.Tasks;

        using MediatR;
        using Microsoft.Extensions.DependencyInjection;

        namespace OrderApp
        {
            public sealed record CreateOrder(string CustomerId) : IRequest<int>;
            public sealed record OrderCreated(int OrderId) : INotification;
            public sealed record StreamOrders : IStreamRequest<int>;

            // EXMIG0004 — legacy HandleAsync name on a request handler.
            public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, int>
            {
                public Task<int> HandleAsync(CreateOrder request, CancellationToken ct) => Task.FromResult(1);
            }

            // EXMIG0004 — legacy HandleAsync name on a notification handler.
            public sealed class OrderCreatedHandler : INotificationHandler<OrderCreated>
            {
                public Task HandleAsync(OrderCreated notification, CancellationToken ct) => Task.CompletedTask;
            }

            // Portable — mechanically shimmed, no manual step (should NOT raise EXMIG0002).
            public sealed class LoggingBehavior : IPipelineBehavior<CreateOrder, int> { }

            // EXMIG0002 (manual) — the five non-portable constructs.
            public sealed class ValidationPreProcessor : IRequestPreProcessor<CreateOrder> { }
            public sealed class AuditPostProcessor : IRequestPostProcessor<CreateOrder, int> { }
            public sealed class OrderExceptionHandler : IRequestExceptionHandler<CreateOrder, int, System.Exception> { }
            public sealed class OrderExceptionAction : IRequestExceptionAction<CreateOrder, System.Exception> { }
            public sealed class OrderStreamBehavior : IStreamPipelineBehavior<StreamOrders, int> { }

            public static class Startup
            {
                public static void Configure(IServiceCollection services, Assembly assembly)
                {
                    // EXMIG0001 — MediatR registration call.
                    services.AddMediatR(assembly);
                }
            }
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunCodemodAnalyzersAsync()
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "MediatRMigrationFixture",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(MediatRStubs),
                CSharpSyntaxTree.ParseText(AppSource),
            ],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new AddMediatRRegistrationAnalyzer(),
            new MediatRUsingDirectiveAnalyzer(),
            new HandlerSignatureAnalyzer(),
            new NonDeterministicConstructAnalyzer());

        // GetAnalyzerDiagnosticsAsync returns only analyzer diagnostics (never compiler CS errors),
        // so an intentionally-stubbed fixture does not pollute the EXMIG tally.
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task DetectEveryMigrationCategory_OverANonTrivialApp_WithoutCrashingOrSilentlySkipping()
    {
        var diagnostics = await RunCodemodAnalyzersAsync();

        var byId = diagnostics
            .GroupBy(d => d.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        // Auto-fixable categories each fire (no silent skip on the portable path).
        byId.GetValueOrDefault("EXMIG0001").ShouldBe(1, "the AddMediatR registration must be flagged");
        byId.GetValueOrDefault("EXMIG0003").ShouldBe(1, "the 'using MediatR;' directive must be flagged");
        byId.GetValueOrDefault("EXMIG0004").ShouldBe(2, "both legacy HandleAsync handlers must be flagged");

        // Manual category — every non-portable construct surfaces, never silently skipped.
        byId.GetValueOrDefault("EXMIG0002").ShouldBe(5, "all five non-portable constructs must surface EXMIG0002");
    }

    [Fact]
    public async Task RecordTheAutoFixVersusManualSplit()
    {
        var diagnostics = await RunCodemodAnalyzersAsync();

        var autoFixable = diagnostics.Count(d => d.Id is "EXMIG0001" or "EXMIG0003" or "EXMIG0004");
        var manual = diagnostics.Count(d => d.Id == "EXMIG0002");
        var total = autoFixable + manual;

        // 4 auto-migrated (1 registration + 1 using + 2 handlers) of 9 migration points; 5 manual.
        // Auto-fix rate = 4/9 ≈ 44.4%. See management/reports/s889-s4kwiv-mediatr-codemod-measurement.md.
        total.ShouldBe(9);
        autoFixable.ShouldBe(4);
        manual.ShouldBe(5);
    }
}
