// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Options for exception-to-problem-details mapping configuration.
/// </summary>
/// <remarks>
/// This class is immutable after construction and is designed to be built by
/// <see cref="ExceptionMappingBuilder"/> and consumed by <see cref="ExceptionMapper"/>.
/// </remarks>
internal sealed class ExceptionMapperOptions
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ExceptionMapperOptions"/> class.
	/// </summary>
	/// <param name="mappings"> The registered exception mappings. </param>
	/// <param name="defaultMapper"> The default mapper for unhandled exceptions. </param>
	/// <param name="useApiExceptionMapping"> Whether to auto-map ApiException types. </param>
	public ExceptionMapperOptions(
		IReadOnlyList<ExceptionMapping> mappings,
		Func<Exception, IMessageProblemDetails> defaultMapper,
		bool useApiExceptionMapping)
	{
		Mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
		DefaultMapper = defaultMapper ?? throw new ArgumentNullException(nameof(defaultMapper));
		UseApiExceptionMapping = useApiExceptionMapping;
	}

	/// <summary>
	/// Gets the registered exception mappings in registration order.
	/// </summary>
	public IReadOnlyList<ExceptionMapping> Mappings { get; }

	/// <summary>
	/// Gets the default mapper for exceptions that don't match any specific mapping.
	/// </summary>
	public Func<Exception, IMessageProblemDetails> DefaultMapper { get; }

	/// <summary>
	/// Gets a value indicating whether ApiException auto-mapping is enabled.
	/// </summary>
	public bool UseApiExceptionMapping { get; }
}
