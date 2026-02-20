// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Implementation of <see cref="IExceptionMappingBuilder"/> for configuring exception mappings.
/// </summary>
/// <remarks>
/// This builder is used at application startup to configure exception mappings.
/// It is not thread-safe and should only be used during the DI configuration phase.
/// After calling <see cref="Build"/>, the resulting options are immutable.
/// </remarks>
internal sealed class ExceptionMappingBuilder : IExceptionMappingBuilder
{
	private readonly List<ExceptionMapping> _mappings = [];
	private Func<Exception, IMessageProblemDetails>? _defaultMapper;
	private bool _useApiExceptionMapping = true; // Enabled by default

	/// <inheritdoc />
	public IExceptionMappingBuilder Map<TException>(Func<TException, IMessageProblemDetails> mapper)
		where TException : Exception
	{
		ArgumentNullException.ThrowIfNull(mapper);

		_mappings.Add(new TypedExceptionMapping<TException>(mapper));
		return this;
	}

	/// <inheritdoc />
	public IExceptionMappingBuilder MapAsync<TException>(
		Func<TException, CancellationToken, Task<IMessageProblemDetails>> mapper)
		where TException : Exception
	{
		ArgumentNullException.ThrowIfNull(mapper);

		_mappings.Add(new AsyncTypedExceptionMapping<TException>(mapper));
		return this;
	}

	/// <inheritdoc />
	public IExceptionMappingBuilder MapWhen<TException>(
		Func<TException, bool> predicate,
		Func<TException, IMessageProblemDetails> mapper)
		where TException : Exception
	{
		ArgumentNullException.ThrowIfNull(predicate);
		ArgumentNullException.ThrowIfNull(mapper);

		_mappings.Add(new ConditionalExceptionMapping<TException>(predicate, mapper));
		return this;
	}

	/// <inheritdoc />
	public IExceptionMappingBuilder MapDefault(Func<Exception, IMessageProblemDetails> mapper)
	{
		ArgumentNullException.ThrowIfNull(mapper);

		_defaultMapper = mapper;
		return this;
	}

	/// <inheritdoc />
	public IExceptionMappingBuilder UseApiExceptionMapping()
	{
		_useApiExceptionMapping = true;
		return this;
	}

	/// <summary>
	/// Builds the exception mapper options from the configured mappings.
	/// </summary>
	/// <returns> The built options. </returns>
	internal ExceptionMapperOptions Build()
	{
		return new ExceptionMapperOptions(
			_mappings.ToArray(),
			_defaultMapper ?? CreateDefaultMapper(),
			_useApiExceptionMapping);
	}

	/// <summary>
	/// Creates the default mapper for unhandled exceptions.
	/// </summary>
	private static Func<Exception, IMessageProblemDetails> CreateDefaultMapper()
	{
		return static exception => new MessageProblemDetails
		{
			Type = ProblemDetailsTypes.Internal,
			Title = "Internal Server Error",
			ErrorCode = 500,
			Status = 500,
			Detail = "An unexpected error occurred.",
			Instance = $"urn:dispatch:exception:{Guid.NewGuid()}",
		};
	}
}
