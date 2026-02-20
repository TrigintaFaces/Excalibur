// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Microsoft.Extensions.Options;

namespace Excalibur.Domain.BoundedContext;

/// <summary>
/// Default implementation of <see cref="IBoundedContextValidator"/> that uses reflection to discover
/// <see cref="BoundedContextAttribute"/> decorations and detect cross-boundary violations.
/// </summary>
/// <remarks>
/// <para>
/// At validation time, each type decorated with <see cref="BoundedContextAttribute"/> is inspected.
/// The validator examines constructor parameters, public properties, and fields for references to types
/// from different bounded contexts. When a cross-boundary reference is found that is not in the
/// <see cref="BoundedContextOptions.AllowedCrossBoundaryPatterns"/> list, a <see cref="BoundedContextViolation"/>
/// is reported.
/// </para>
/// <para>
/// Attribute lookups are cached in a <see cref="ConcurrentDictionary{TKey, TValue}"/> for thread safety
/// and performance on repeated validation calls.
/// </para>
/// </remarks>
public sealed class DefaultBoundedContextValidator : IBoundedContextValidator
{
	private readonly ConcurrentDictionary<Type, string?> _contextCache = new();
	private readonly BoundedContextOptions _options;
	private readonly IReadOnlyList<Assembly> _assemblies;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultBoundedContextValidator"/> class.
	/// </summary>
	/// <param name="options">The bounded context enforcement options.</param>
	public DefaultBoundedContextValidator(IOptions<BoundedContextOptions> options)
		: this(options, AppDomain.CurrentDomain.GetAssemblies())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultBoundedContextValidator"/> class
	/// with explicit assemblies to scan.
	/// </summary>
	/// <param name="options">The bounded context enforcement options.</param>
	/// <param name="assemblies">The assemblies to scan for bounded context types.</param>
	public DefaultBoundedContextValidator(IOptions<BoundedContextOptions> options, IReadOnlyList<Assembly> assemblies)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(assemblies);

		_options = options.Value;
		_assemblies = assemblies;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<BoundedContextViolation>> ValidateAsync(CancellationToken cancellationToken)
	{
		var violations = new List<BoundedContextViolation>();
		var allowedPatterns = new HashSet<string>(_options.AllowedCrossBoundaryPatterns, StringComparer.OrdinalIgnoreCase);

		// Discover all types decorated with [BoundedContext]
		var contextTypes = DiscoverBoundedContextTypes();

		foreach (var (type, sourceContext) in contextTypes)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Check constructor parameters
			foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
			{
				foreach (var param in ctor.GetParameters())
				{
					CheckTypeReference(type, sourceContext, param.ParameterType, allowedPatterns, violations);
				}
			}

			// Check public properties
			foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				CheckTypeReference(type, sourceContext, prop.PropertyType, allowedPatterns, violations);
			}

			// Check public fields
			foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				CheckTypeReference(type, sourceContext, field.FieldType, allowedPatterns, violations);
			}
		}

		IReadOnlyList<BoundedContextViolation> result = violations;
		return Task.FromResult(result);
	}

	private List<(Type Type, string Context)> DiscoverBoundedContextTypes()
	{
		var result = new List<(Type, string)>();

		foreach (var assembly in _assemblies)
		{
			Type[] types;
			try
			{
				types = assembly.GetTypes();
			}
#pragma warning disable CA1031 // Do not catch general exception types -- assemblies may fail to load types
			catch (ReflectionTypeLoadException ex)
#pragma warning restore CA1031
			{
				types = ex.Types.Where(t => t is not null).ToArray()!;
			}

			foreach (var type in types)
			{
				var context = GetBoundedContext(type);
				if (context is not null)
				{
					result.Add((type, context));
				}
			}
		}

		return result;
	}

	private string? GetBoundedContext(Type type)
	{
		return _contextCache.GetOrAdd(type, static t =>
			t.GetCustomAttribute<BoundedContextAttribute>()?.Name);
	}

	private void CheckTypeReference(
		Type sourceType,
		string sourceContext,
		Type referencedType,
		HashSet<string> allowedPatterns,
		List<BoundedContextViolation> violations)
	{
		// Unwrap generic types (e.g., ICollection<OrderItem> -> OrderItem)
		var targetType = UnwrapType(referencedType);
		if (targetType is null)
		{
			return;
		}

		var targetContext = GetBoundedContext(targetType);
		if (targetContext is null)
		{
			return; // Target type is not in any bounded context; no violation
		}

		if (string.Equals(sourceContext, targetContext, StringComparison.OrdinalIgnoreCase))
		{
			return; // Same context; no violation
		}

		// Check if this cross-boundary pattern is explicitly allowed
		var pattern = $"{sourceContext}->{targetContext}";
		if (allowedPatterns.Contains(pattern))
		{
			return;
		}

		var violation = new BoundedContextViolation(
			sourceType,
			targetType,
			sourceContext,
			targetContext,
			$"Type '{sourceType.Name}' in bounded context '{sourceContext}' references " +
			$"type '{targetType.Name}' from bounded context '{targetContext}'.");

		// Avoid duplicate violations for the same source-target pair
		if (!violations.Exists(v => v.SourceType == sourceType && v.TargetType == targetType))
		{
			violations.Add(violation);
		}
	}

	private static Type? UnwrapType(Type type)
	{
		// Skip primitive types, strings, and system types
		if (type.IsPrimitive || type == typeof(string) || type == typeof(object) ||
			type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
			type == typeof(Guid) || type == typeof(TimeSpan))
		{
			return null;
		}

		// Unwrap Nullable<T>
		var underlying = Nullable.GetUnderlyingType(type);
		if (underlying is not null)
		{
			return UnwrapType(underlying);
		}

		// Unwrap generic collections (e.g., IList<T>, IEnumerable<T>)
		if (type.IsGenericType)
		{
			var args = type.GetGenericArguments();
			// For single-argument generics (collections), check the element type
			if (args.Length == 1)
			{
				return UnwrapType(args[0]);
			}

			// For multi-argument generics, skip (e.g., Dictionary<K,V>)
			return null;
		}

		// Skip arrays - check element type
		if (type.IsArray)
		{
			var elementType = type.GetElementType();
			return elementType is not null ? UnwrapType(elementType) : null;
		}

		// Skip types without assembly info (open generics, etc.)
		if (type.Assembly.IsDynamic)
		{
			return null;
		}

		return type;
	}
}
