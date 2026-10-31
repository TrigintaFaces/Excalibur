// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Serialization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Validation.FluentValidation.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Validation")]
[Trait("Pattern", "Regression")]
public sealed class ValidatorScopeShould
{
    [Fact]
    public async Task AggregateEachValidatorsFailuresExactlyOnce()
    {
        var failing = new InlineValidator<ScopeMessage>();
        failing.RuleFor(message => message).Must(_ => false).WithMessage("first validator failed");
        var passing = new InlineValidator<ScopeMessage>();
        passing.RuleFor(message => message).Must(_ => true);
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<ScopeMessage>>(failing);
        services.AddSingleton<IValidator<ScopeMessage>>(passing);
        await using var root = services.BuildServiceProvider();
        var resolver = new FluentValidatorResolver(root);
        var result = await resolver.TryValidateAsync(new ScopeMessage());
        result!.IsValid.ShouldBeFalse();
        result.Errors.Cast<object>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowOverlayProviderWhoseSelfResolutionDelegatesToRoot(bool aot)
    {
        var observed = new List<ScopedState>();
        await using var root = Build(aot, observed);
        await using var scope = root.CreateAsyncScope();
        var expected = scope.ServiceProvider.GetRequiredService<ScopedState>();
        var overlay = new OverlayProvider(root, scope.ServiceProvider);
        var resolver = root.GetRequiredService<IValidatorResolver>();
        (await resolver.TryValidateAsync(new ScopeMessage(), overlay))!.IsValid.ShouldBeTrue();
        observed.ShouldHaveSingleItem().ShouldBeSameAs(expected);
        expected.Disposed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task OwnFreshScopesForMissingOrRootRequestServices(bool aot, int rootForm)
    {
        var observed = new List<ScopedState>();
        await using var root = Build(aot, observed);
        var resolver = root.GetRequiredService<IValidatorResolver>();
        var requestServices = rootForm switch
        {
            1 => root,
            2 => root.GetRequiredService<IServiceProvider>(),
            _ => null,
        };

        for (var i = 0; i < 2; i++)
        {
            var result = await resolver.TryValidateAsync(new ScopeMessage(), requestServices);
            result!.IsValid.ShouldBeTrue();
            observed[i].Disposed.ShouldBeTrue();
        }

        observed[0].ShouldNotBeSameAs(observed[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowExplicitCustomProviderBeforeAmbientAndNeverDisposeEither(bool aot)
    {
        var observed = new List<ScopedState>();
        var ambient = new AmbientScope();
        await using var root = Build(aot, observed, ambient);
        await using var ambientScope = root.CreateAsyncScope();
        await using var explicitScope = root.CreateAsyncScope();
        ambient.CurrentServiceProvider = ambientScope.ServiceProvider;
        var resolver = root.GetRequiredService<IValidatorResolver>();
        var expected = explicitScope.ServiceProvider.GetRequiredService<ScopedState>();

        _ = await resolver.TryValidateAsync(new ScopeMessage(), new BorrowedProvider(explicitScope.ServiceProvider));
        observed[0].ShouldBeSameAs(expected);
        expected.Disposed.ShouldBeFalse();

        _ = await resolver.TryValidateAsync(new ScopeMessage());
        observed[1].ShouldBeSameAs(ambientScope.ServiceProvider.GetRequiredService<ScopedState>());
        observed[1].Disposed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DisposeOwnedScopeAfterCancellationOrRuleFailure(bool aot, bool cancel)
    {
        var observed = new List<ScopedState>();
        await using var root = Build(aot, observed);
        using var cancellation = new CancellationTokenSource();
        var message = new ScopeMessage { Cancel = cancel ? cancellation : null, Throw = !cancel };
        var resolver = root.GetRequiredService<IValidatorResolver>();

        if (cancel)
        {
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await resolver.TryValidateAsync(message, cancellationToken: cancellation.Token));
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(async () => await resolver.TryValidateAsync(message));
        }

        observed.ShouldHaveSingleItem();
        observed[0].Disposed.ShouldBeTrue();
    }

    private static ServiceProvider Build(bool aot, List<ScopedState> observed, AmbientScope? ambient = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedState>();
        services.AddScoped<IValidator<ScopeMessage>>(provider =>
            new ScopeValidator(provider.GetRequiredService<ScopedState>(), observed));
        services.AddSingleton<IAotValidationDispatcher, ScopeDispatcher>();
        services.AddSingleton<IValidatorResolver>(provider => aot
            ? new AotFluentValidatorResolver(provider)
            : new FluentValidatorResolver(provider));
        if (ambient is not null)
        {
            services.AddSingleton<IDispatchAmbientScopeAccessor>(ambient);
        }

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private sealed class ScopeMessage : IDispatchMessage
    {
        public CancellationTokenSource? Cancel { get; init; }
        public bool Throw { get; init; }
    }

    private sealed class ScopedState : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Disposed = true;
        }
    }

    private sealed class ScopeValidator : AbstractValidator<ScopeMessage>
    {
        public ScopeValidator(ScopedState state, List<ScopedState> observed)
        {
            RuleFor(message => message).MustAsync(async (message, token) =>
            {
                observed.Add(state);
                state.Disposed.ShouldBeFalse();
                await Task.Yield();
                state.Disposed.ShouldBeFalse();
                message.Cancel?.Cancel();
                token.ThrowIfCancellationRequested();
                if (message.Throw)
                {
                    throw new InvalidOperationException("Rule dependency failed.");
                }

                return true;
            });
        }
    }

    private sealed class ScopeDispatcher : IAotValidationDispatcher
    {
        public async ValueTask<IValidationResult?> TryValidateAsync(
            IDispatchMessage message, IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            var validator = provider.GetRequiredService<IValidator<ScopeMessage>>();
            var result = await validator.ValidateAsync((ScopeMessage)message, cancellationToken);
            return result.IsValid ? SerializableValidationResult.Success() : SerializableValidationResult.Failed("invalid");
        }
    }

    private sealed class AmbientScope : IDispatchAmbientScopeAccessor
    {
        public IServiceProvider? CurrentServiceProvider { get; set; }
    }

    private sealed class BorrowedProvider(IServiceProvider provider) : IServiceProvider
    {
        public object? GetService(Type serviceType) => provider.GetService(serviceType);
    }

    private sealed class OverlayProvider(IServiceProvider root, IServiceProvider scope) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IServiceProvider)
            ? root.GetService(serviceType)
            : scope.GetService(serviceType);
    }
}
