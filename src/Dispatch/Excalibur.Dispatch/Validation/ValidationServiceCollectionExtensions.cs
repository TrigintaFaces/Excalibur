// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Validation;

using ValidationMiddleware = Excalibur.Dispatch.Middleware.ValidationMiddleware;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring validation services in the dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// The default configuration uses <see cref="NoOpValidatorResolver"/> which performs no validation.
/// This is appropriate when validation is not needed or when using custom validation approaches.
/// </para>
/// <para>
/// For validation support, choose one of the following options:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>DataAnnotations</b> (zero dependencies): Use <c>WithDataAnnotationsValidation()</c> on <c>IDispatchBuilder</c>.
/// Validates using <see cref="System.ComponentModel.DataAnnotations"/> attributes.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>FluentValidation</b> (separate package): Install <c>Excalibur.Dispatch.Validation.FluentValidation</c> NuGet package
/// and use <c>WithFluentValidation()</c> on <c>IDispatchBuilder</c>.
/// </description>
/// </item>
/// </list>
/// </remarks>
public static class ValidationServiceCollectionExtensions
{
	/// <summary>
	/// Adds dispatch validation services to the specified service collection.
	/// Registers <see cref="NoOpValidatorResolver"/> by default (no validation performed).
	/// </summary>
	/// <param name="services"> The service collection to add the validation services to. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// To enable actual validation, call <c>WithDataAnnotationsValidation()</c> on the <c>IDispatchBuilder</c>,
	/// or install the <c>Excalibur.Dispatch.Validation.FluentValidation</c> package for FluentValidation support.
	/// </remarks>
	public static IServiceCollection AddDispatchValidation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddSingleton<IDispatchMiddleware, ValidationMiddleware>();
		_ = services.AddSingleton<IValidatorResolver, NoOpValidatorResolver>();

		return services;
	}
}
