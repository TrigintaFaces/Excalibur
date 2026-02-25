// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Extensions;

/// <summary>
/// Provides extension methods for configuring options with defaults and custom configuration.
/// </summary>
public static class ConfigureOptionsExtensions
{
	/// <summary>
	/// Cache for compiled property copiers to improve performance.
	/// </summary>
	private static readonly ConcurrentDictionary<Type, Action<object, object>> PropertyCopiers = new();

	/// <summary>
	/// Configures options of type <typeparamref name="T" /> with defaults and optional custom configuration.
	/// </summary>
	/// <typeparam name="T"> The options type to configure. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional custom configuration action. </param>
	/// <param name="defaults"> Required defaults configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> or <paramref name="defaults" /> is null. </exception>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Options types are preserved through DI registration. Consider using source generators for AOT-safe property copying.")]
	public static IServiceCollection ConfigureOptions<T>(
		this IServiceCollection services,
		Action<T>? configure,
		Action<T> defaults)
		where T : class, new()
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(defaults);

		// Create and configure the template instance
		var template = new T();
		defaults(template);
		configure?.Invoke(template);

		// Get or create the optimized property copier
		var copier = GetOrCreatePropertyCopier<T>();

		// Configure the options
		_ = services.Configure<T>(opts => copier(template, opts));

		return services;
	}

	/// <summary>
	/// Configures options with defaults, custom configuration, and validation.
	/// </summary>
	/// <typeparam name="T"> The options type to configure. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional custom configuration action. </param>
	/// <param name="defaults"> Required defaults configuration action. </param>
	/// <param name="validate"> Validation function that returns validation errors or null if valid. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection ConfigureOptionsWithValidation<T>(
		this IServiceCollection services,
		Action<T>? configure,
		Action<T> defaults,
		Func<T, string?> validate)
		where T : class, new()
	{
		ArgumentNullException.ThrowIfNull(validate);

		_ = services.ConfigureOptions(configure, defaults);

		_ = services.AddSingleton<IValidateOptions<T>>(new ValidateOptions<T>(validate));

		return services;
	}

	/// <summary>
	/// Configures options from a configuration section with defaults as fallback.
	/// </summary>
	/// <typeparam name="T"> The options type to configure. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="sectionName"> The configuration section name. </param>
	/// <param name="defaults"> Required defaults configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Options types are preserved through DI registration and the DynamicallyAccessedMembers attribute on T ensures all members are preserved.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification =
			"Configuration binding relies on runtime type information, but the type T is preserved through the DynamicallyAccessedMembers attribute.")]
	public static IServiceCollection ConfigureOptionsFromConfiguration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IServiceCollection services,
		string sectionName,
		Action<T> defaults)
		where T : class, new()
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(sectionName);
		ArgumentNullException.ThrowIfNull(defaults);

		// First apply defaults
		_ = services.ConfigureOptions(configure: null, defaults);

		// Then bind from configuration (will override defaults where values exist)
		_ = services.AddOptions<T>()
			.BindConfiguration(sectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Configures named options with defaults and custom configuration.
	/// </summary>
	/// <typeparam name="T"> The options type to configure. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="name"> The name of the options instance. </param>
	/// <param name="configure"> Optional custom configuration action. </param>
	/// <param name="defaults"> Required defaults configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Options types are preserved through DI registration. Consider using source generators for AOT-safe property copying.")]
	public static IServiceCollection ConfigureNamedOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
		this IServiceCollection services,
		string name,
		Action<T>? configure,
		Action<T> defaults)
		where T : class, new()
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(defaults);

		var template = new T();
		defaults(template);
		configure?.Invoke(template);

		var copier = GetOrCreatePropertyCopier<T>();

		_ = services.Configure<T>(name, opts => copier(template, opts));

		return services;
	}

	/// <summary>
	/// Creates a deep clone configuration where complex types are properly cloned.
	/// </summary>
	/// <typeparam name="T"> The options type to configure. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional custom configuration action. </param>
	/// <param name="defaults"> Required defaults configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "Options types are preserved through DI registration. JSON serialization requires runtime type information.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification =
			"JSON serialization requires runtime code generation. Consider using source-generated JSON serializers for AOT scenarios.")]
	public static IServiceCollection ConfigureOptionsWithDeepClone<T>(
		this IServiceCollection services,
		Action<T>? configure,
		Action<T> defaults)
		where T : class, new()
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(defaults);

		var template = new T();
		defaults(template);
		configure?.Invoke(template);

		// Use JSON serialization for deep cloning
		var json = JsonSerializer.Serialize(template);

		_ = services.Configure<T>(opts =>
		{
			var cloned = JsonSerializer.Deserialize<T>(json);
			if (cloned != null)
			{
				var copier = GetOrCreatePropertyCopier<T>();
				copier(cloned, opts);
			}
		});

		return services;
	}

	/// <summary>
	/// Gets or creates an optimized property copier for the specified type.
	/// </summary>
	/// <typeparam name="T"> The type to create a copier for. </typeparam>
	/// <returns> An action that copies properties from source to target. </returns>
	[RequiresUnreferencedCode("Uses reflection and expression compilation which is not AOT compatible")]
	private static Action<object, object> GetOrCreatePropertyCopier<T>() =>
		PropertyCopiers.GetOrAdd(typeof(T), static type =>
		{
			var sourceParam = Expression.Parameter(typeof(object), "source");
			var targetParam = Expression.Parameter(typeof(object), "target");

			var sourceTyped = Expression.Variable(type, "sourceTyped");
			var targetTyped = Expression.Variable(type, "targetTyped");

			var expressions = new List<Expression>
			{
				Expression.Assign(sourceTyped, Expression.Convert(sourceParam, type)),
				Expression.Assign(targetTyped, Expression.Convert(targetParam, type)),
			};

			var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
				.Where(static p => p is { CanRead: true, CanWrite: true });

			foreach (var prop in properties)
			{
				try
				{
					var sourceProperty = Expression.Property(sourceTyped, prop);
					var targetProperty = Expression.Property(targetTyped, prop);
					expressions.Add(Expression.Assign(targetProperty, sourceProperty));
				}
				catch
				{
					// Skip properties that can't be accessed
				}
			}

			var body = Expression.Block([sourceTyped, targetTyped], expressions);
			var lambda = Expression.Lambda<Action<object, object>>(body, sourceParam, targetParam);

			return lambda.Compile();
		});

	/// <summary>
	/// Simple validation options implementation.
	/// </summary>
	/// <typeparam name="T"> The options type to validate. </typeparam>
	private sealed class ValidateOptions<T>(Func<T, string?> validate) : IValidateOptions<T>
		where T : class
	{
		public ValidateOptionsResult Validate(string? name, T options)
		{
			var error = validate(options);
			return error == null
				? ValidateOptionsResult.Success
				: ValidateOptionsResult.Fail(error);
		}
	}
}
