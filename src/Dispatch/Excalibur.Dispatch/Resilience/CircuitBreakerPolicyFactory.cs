// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Default <see cref="ICircuitBreakerPolicyFactory"/> that produces in-memory <see cref="CircuitBreakerPolicy"/>
/// instances. Registered as a singleton; each <see cref="Create"/> call returns a new, independent policy.
/// </summary>
internal sealed class CircuitBreakerPolicyFactory : ICircuitBreakerPolicyFactory
{
	private readonly ILoggerFactory? _loggerFactory;

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerPolicyFactory"/> class.
	/// </summary>
	/// <param name="loggerFactory">
	/// Optional logger factory used to create a policy logger when the caller does not supply one.
	/// </param>
	public CircuitBreakerPolicyFactory(ILoggerFactory? loggerFactory = null)
	{
		_loggerFactory = loggerFactory;
	}

	/// <inheritdoc />
	public ICircuitBreakerPolicy Create(string name, CircuitBreakerOptions options, ILogger? logger = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(options);

		var effectiveLogger = logger ?? _loggerFactory?.CreateLogger<CircuitBreakerPolicy>();
		return new CircuitBreakerPolicy(options, name, effectiveLogger);
	}
}
