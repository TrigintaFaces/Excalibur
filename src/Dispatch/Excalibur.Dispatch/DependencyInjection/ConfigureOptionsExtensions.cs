// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Extensions;

/// <summary>
/// Provides extension methods for configuring options with defaults and custom configuration.
/// </summary>
/// <remarks>
/// Every method here composes ordinary <see cref="OptionsServiceCollectionExtensions" /> configuration callbacks, so the
/// options instance is populated by direct property assignment at resolution time. No reflection, expression compilation
/// or serialization is involved, which keeps these helpers usable under trimming and ahead-of-time compilation.
/// </remarks>
public static class ConfigureOptionsExtensions
{
	/// <summary>
	/// Configures options of type <typeparamref name="T" /> with defaults and optional custom configuration.
	/// </summary>
	/// <typeparam name="T"> The options type to configure. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional custom configuration action. </param>
	/// <param name="defaults"> Required defaults configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// The defaults action runs before the custom configuration action, so custom configuration wins on any property both
	/// of them set. Properties neither action touches keep whatever the options instance already carries.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> or <paramref name="defaults" /> is null. </exception>
	public static IServiceCollection ConfigureOptions<T>(
		this IServiceCollection services,
		Action<T>? configure,
		Action<T> defaults)
		where T : class, new()
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(defaults);

		ApplyEagerly(configure, defaults);

		_ = services.Configure<T>(options =>
		{
			defaults(options);
			configure?.Invoke(options);
		});

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
	/// Configures named options with defaults and custom configuration.
	/// </summary>
	/// <typeparam name="T"> The options type to configure. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="name"> The name of the options instance. </param>
	/// <param name="configure"> Optional custom configuration action. </param>
	/// <param name="defaults"> Required defaults configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection ConfigureNamedOptions<T>(
		this IServiceCollection services,
		string name,
		Action<T>? configure,
		Action<T> defaults)
		where T : class, new()
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(defaults);

		ApplyEagerly(configure, defaults);

		_ = services.Configure<T>(name, options =>
		{
			defaults(options);
			configure?.Invoke(options);
		});

		return services;
	}

	/// <summary>
	/// Configures options such that no state is shared between resolved options instances.
	/// </summary>
	/// <typeparam name="T"> The options type to configure. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Optional custom configuration action. </param>
	/// <param name="defaults"> Required defaults configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Both actions run against each options instance as it is created, so any nested object they allocate belongs to that
	/// instance alone. There is no shared template to clone, which is why this behaves identically to
	/// <see cref="ConfigureOptions{T}(IServiceCollection, Action{T}, Action{T})" />.
	/// </remarks>
	public static IServiceCollection ConfigureOptionsWithDeepClone<T>(
		this IServiceCollection services,
		Action<T>? configure,
		Action<T> defaults)
		where T : class, new() => services.ConfigureOptions(configure, defaults);

	/// <summary>
	/// Runs the configuration actions once at registration time against a throwaway instance.
	/// </summary>
	/// <typeparam name="T"> The options type to configure. </typeparam>
	/// <param name="configure"> Optional custom configuration action. </param>
	/// <param name="defaults"> Required defaults configuration action. </param>
	/// <remarks>
	/// Registration is where a caller can still act on a bad configuration action, so both actions are exercised here and
	/// a throwing action surfaces immediately rather than on the first options resolution. The instance is discarded; the
	/// real options instance is populated by the registered callback.
	/// </remarks>
	private static void ApplyEagerly<T>(Action<T>? configure, Action<T> defaults)
		where T : class, new()
	{
		var probe = new T();
		defaults(probe);
		configure?.Invoke(probe);
	}

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
