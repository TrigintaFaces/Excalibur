// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Plans keyed public-constructor activation without activating candidate dependencies.</summary>
/// <remarks>
/// Follows native DI's keyed argument rules and valid-graph constructor selection. Public availability
/// metadata cannot reproduce recursive container callsite validation of an unselected malformed graph.
/// Once a constructor is selected, dependency/constructor failures propagate; no fallback is attempted.
/// </remarks>
internal static class KeyedStoreTypeActivator
{
    internal static object Create(IServiceProvider provider, Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType,
        object? requestedKey)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);
        if (implementationType.IsAbstract || implementationType.IsInterface || implementationType.ContainsGenericParameters ||
            !serviceType.IsAssignableFrom(implementationType))
        {
            throw new InvalidOperationException($"Cannot activate '{implementationType}' as '{serviceType}'.");
        }
        var constructors = implementationType.GetConstructors();
        if (constructors.Length == 0) { throw new InvalidOperationException($"No public constructor is available for '{implementationType}'."); }
        var services = provider.GetService<IServiceProviderIsService>();
        var keyedServices = provider.GetService<IServiceProviderIsKeyedService>();
        ConstructorInfo? selected = null;
        ArgumentPlan[]? selectedArguments = null;
        if (constructors.Length == 1)
        {
            selected = constructors[0];
            selectedArguments = PlanArguments(selected.GetParameters(), requestedKey, services, keyedServices,
                implementationType, throwForMissing: true);
        }
        else
        {
            Array.Sort(constructors, static (left, right) => right.GetParameters().Length.CompareTo(left.GetParameters().Length));
            HashSet<Type>? selectedTypes = null;
            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                var arguments = PlanArguments(parameters, requestedKey, services, keyedServices,
                    implementationType, throwForMissing: false);
                if (arguments is null) { continue; }
                if (selected is null)
                {
                    selected = constructor;
                    selectedArguments = arguments;
                    continue;
                }
                var compatibleTypes = selectedTypes ??= new HashSet<Type>(selected.GetParameters().Select(static parameter => parameter.ParameterType));
                if (parameters.Any(parameter => !compatibleTypes.Contains(parameter.ParameterType)))
                {
                    throw new InvalidOperationException($"More than one public constructor can activate '{implementationType}': '{selected}' and '{constructor}'.");
                }
            }
        }
        if (selected is null || selectedArguments is null)
        {
            throw new InvalidOperationException($"No resolvable public constructor is available for '{implementationType}'.");
        }

        // Resolve only after the complete selection and ambiguity/missing checks. Registered factories
        // may legitimately return null: required-service APIs would change native constructor semantics.
        var values = new object?[selectedArguments.Length];
        for (var index = 0; index < selectedArguments.Length; index++)
        {
            var argument = selectedArguments[index];
            values[index] = argument.Constant
                ? argument.Value
                : argument.Keyed
                    ? provider.GetKeyedService(argument.ParameterType, argument.ServiceKey)
                    : provider.GetService(argument.ParameterType);
        }
        return selected.Invoke(BindingFlags.DoNotWrapExceptions, binder: null, parameters: values, culture: null);
    }

    private static ArgumentPlan[]? PlanArguments(ParameterInfo[] parameters, object? requestedKey,
        IServiceProviderIsService? services, IServiceProviderIsKeyedService? keyedServices,
        Type implementationType, bool throwForMissing)
    {
        var plans = new ArgumentPlan[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            var type = parameter.ParameterType;
            var constantKey = false;
            var keyed = false;
            object? dependencyKey = null;
            foreach (var attribute in parameter.GetCustomAttributes(inherit: true))
            {
                if (requestedKey is not null && attribute is ServiceKeyAttribute)
                {
                    if (!ReferenceEquals(requestedKey, KeyedService.AnyKey) && type != requestedKey.GetType() && type != typeof(object))
                    {
                        throw new InvalidOperationException($"The service key type cannot bind constructor parameter '{type}' on '{implementationType}'.");
                    }
                    constantKey = true;
                    break;
                }
                if (attribute is FromKeyedServicesAttribute fromKeyed)
                {
                    dependencyKey = fromKeyed.LookupMode switch
                    {
                        ServiceKeyLookupMode.InheritKey => requestedKey,
                        ServiceKeyLookupMode.ExplicitKey => fromKeyed.Key,
                        ServiceKeyLookupMode.NullKey => null,
                        _ => null,
                    };
                    if (dependencyKey is not null) { keyed = true; break; }
                }
            }
            if (constantKey)
            {
                plans[index] = new ArgumentPlan(type, requestedKey, Keyed: false, Constant: true, Value: requestedKey);
                continue;
            }
            var available = keyed
                ? (keyedServices ?? throw MissingAvailabilityMetadata()).IsKeyedService(type, dependencyKey)
                : (services ?? throw MissingAvailabilityMetadata()).IsService(type);
            if (available)
            {
                plans[index] = new ArgumentPlan(type, dependencyKey, keyed, Constant: false, Value: null);
                continue;
            }
            if (parameter.HasDefaultValue)
            {
                var value = parameter.DefaultValue;
                var nullable = Nullable.GetUnderlyingType(type);
                if (value is not null && nullable is { IsEnum: true }) { value = Enum.ToObject(nullable, value); }
                // ConstructorInfo.Invoke supplies the zero-initialized value for a null non-nullable
                // value-type default. Avoid reflection-based uninitialized-object allocation here.
                plans[index] = new ArgumentPlan(type, ServiceKey: null, Keyed: false, Constant: true, Value: value);
                continue;
            }
            if (throwForMissing)
            {
                throw new InvalidOperationException($"Cannot resolve constructor parameter '{type}' while activating '{implementationType}'.");
            }
            return null;
        }
        return plans;
    }

    private static InvalidOperationException MissingAvailabilityMetadata() =>
        new("Key-aware constructor planning requires the provider's public service-availability metadata; candidate dependencies are never activated to probe availability.");

    private readonly record struct ArgumentPlan(Type ParameterType, object? ServiceKey, bool Keyed, bool Constant, object? Value);
}
