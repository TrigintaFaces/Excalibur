// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Implementation of <see cref="IExceptionMapper"/> that evaluates registered mappings.
/// </summary>
/// <remarks>
/// <para>
/// This service is thread-safe and designed to be registered as a singleton.
/// All state is immutable after construction.
/// </para>
/// <para>
/// Mapping evaluation order:
/// <list type="number">
///   <item>Check registered mappings in registration order (first match wins)</item>
///   <item>If <see cref="ExceptionMapperOptions.UseApiExceptionMapping"/> is enabled and exception is ApiException, use ToProblemDetails()</item>
///   <item>Fall back to default mapper</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class ExceptionMapper : IExceptionMapper
{
	private readonly IReadOnlyList<ExceptionMapping> _mappings;
	private readonly Func<Exception, IMessageProblemDetails> _defaultMapper;
	private readonly bool _useApiExceptionMapping;

	/// <summary>
	/// Initializes a new instance of the <see cref="ExceptionMapper"/> class.
	/// </summary>
	/// <param name="options"> The exception mapper options. </param>
	public ExceptionMapper(ExceptionMapperOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		_mappings = options.Mappings;
		_defaultMapper = options.DefaultMapper;
		_useApiExceptionMapping = options.UseApiExceptionMapping;
	}

	/// <inheritdoc />
	public IMessageProblemDetails Map(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		// 1. Check registered mappings first (first match wins)
		foreach (var mapping in _mappings)
		{
			if (mapping.CanHandle(exception))
			{
				return mapping.Map(exception);
			}
		}

		// 2. Use ApiException.ToProblemDetails() if enabled
		if (_useApiExceptionMapping && exception is ApiException apiEx)
		{
			return apiEx.ToProblemDetails();
		}

		// 3. Fall back to default mapper
		return _defaultMapper(exception);
	}

	/// <inheritdoc />
	public async Task<IMessageProblemDetails> MapAsync(Exception exception, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(exception);

		// 1. Check registered mappings first (first match wins)
		foreach (var mapping in _mappings)
		{
			if (mapping.CanHandle(exception))
			{
				return await mapping.MapAsync(exception, cancellationToken).ConfigureAwait(false);
			}
		}

		// 2. Use ApiException.ToProblemDetails() if enabled
		if (_useApiExceptionMapping && exception is ApiException apiEx)
		{
			return apiEx.ToProblemDetails();
		}

		// 3. Fall back to default mapper
		return _defaultMapper(exception);
	}

	/// <inheritdoc />
	public bool CanMap(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		// Check if any registered mapping handles this exception
		foreach (var mapping in _mappings)
		{
			if (mapping.CanHandle(exception))
			{
				return true;
			}
		}

		// Check if ApiException mapping is enabled and this is an ApiException
		if (_useApiExceptionMapping && exception is ApiException)
		{
			return true;
		}

		// Default mapper always handles (if configured, which it always is)
		return true;
	}
}
