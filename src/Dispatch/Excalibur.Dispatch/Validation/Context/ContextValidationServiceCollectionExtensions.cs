// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Options.Validation;
using Excalibur.Dispatch.Validation.Context;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering context validation services.
/// </summary>
public static class ContextValidationServiceCollectionExtensions
{
	private const string ConfigurationSectionName = "Dispatch:ContextValidation";

	/// <summary>
	/// Adds context validation services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> Optional configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
		Justification = "Options types are preserved through DI registration and configuration binding")]
	[RequiresDynamicCode("Configuration binding requires dynamic code generation for property reflection and value conversion.")]
	public static IServiceCollection AddContextValidation(
		this IServiceCollection services,
		IConfiguration? configuration = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register options
		_ = services.AddOptions<ContextValidationOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		if (configuration != null)
		{
			var section = configuration.GetSection(ConfigurationSectionName);
			if (section.Exists())
			{
				_ = services.Configure<ContextValidationOptions>(section);
			}
		}

		// Register default validator
		services.TryAddSingleton<IContextValidator, DefaultContextValidator>();

		// Register middleware
		_ = services.AddDispatchMiddleware<ContextValidationMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds context validation services with custom configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddContextValidation(
		this IServiceCollection services,
		Action<ContextValidationOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.Configure(configureOptions);
		services.TryAddSingleton<IContextValidator, DefaultContextValidator>();
		_ = services.AddDispatchMiddleware<ContextValidationMiddleware>();

		return services;
	}

	/// <summary>
	/// Adds a custom context validator.
	/// </summary>
	/// <typeparam name="TValidator"> The type of validator to add. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddContextValidator<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TValidator>(this IServiceCollection services)
		where TValidator : class, IContextValidator
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddSingleton<IContextValidator, TValidator>();

		return services;
	}

	/// <summary>
	/// Configures context validation options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection ConfigureContextValidation(
		this IServiceCollection services,
		Action<ContextValidationOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.Configure(configureOptions);

		return services;
	}

	/// <summary>
	/// Sets context validation to strict mode.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection UseStrictContextValidation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.Configure<ContextValidationOptions>(static options =>
		{
			options.Mode = ValidationMode.Strict;
			options.ValidateRequiredFields = true;
			options.ValidateMultiTenancy = true;
			options.ValidateAuthentication = true;
			options.ValidateTracing = true;
			options.ValidateVersioning = true;
			options.ValidateCollections = true;
			options.ValidateCorrelationChain = true;
			options.EnableDetailedDiagnostics = true;
		});

		return services;
	}

	/// <summary>
	/// Sets context validation to lenient mode.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection UseLenientContextValidation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.Configure<ContextValidationOptions>(static options =>
		{
			options.Mode = ValidationMode.Lenient;
			options.EnableDetailedDiagnostics = false;
		});

		return services;
	}

	/// <summary>
	/// Adds a required field for context validation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="fieldName"> The name of the required field. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddRequiredContextField(
		this IServiceCollection services,
		string fieldName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

		_ = services.Configure<ContextValidationOptions>(options =>
		{
			if (!options.RequiredFields.Contains(fieldName, StringComparer.Ordinal))
			{
				options.RequiredFields.Add(fieldName);
			}
		});

		return services;
	}

	/// <summary>
	/// Adds a field validation rule.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="fieldName"> The name of the field to validate. </param>
	/// <param name="rule"> The validation rule. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddContextFieldValidation(
		this IServiceCollection services,
		string fieldName,
		FieldValidationRule rule)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
		ArgumentNullException.ThrowIfNull(rule);

		_ = services.Configure<ContextValidationOptions>(options => options.FieldValidationRules[fieldName] = rule);

		return services;
	}
}
