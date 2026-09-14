// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Delivery.Handlers;

/// <summary>
/// Records that an assembly scan found more than one handler for a message type that takes exactly one --
/// <c>IActionHandler{TMessage}</c>, <c>IActionHandler{TMessage,TResponse}</c> or <c>IDocumentHandler{TMessage}</c>.
/// </summary>
/// <param name="MessageType"> The message type that has more than one scanned handler. </param>
/// <param name="InterfaceName"> The name of the single-handler interface the handlers collide on. </param>
/// <param name="HandlerTypes"> Every scanned handler type found for <paramref name="MessageType" />. </param>
/// <remarks>
/// Which of the colliding handlers a plain DI <c>TryAdd</c> keeps depends on <see cref="System.Reflection.Assembly.GetTypes" />
/// enumeration order, which is neither documented nor stable -- so an arbitrary winner is never a safe
/// default, and the winner picked today is not guaranteed to be the same one picked tomorrow. This record
/// carries the finding from scan time (<c>RegisterMessageHandlers</c>, where every candidate is still
/// visible) to start-up, where <see cref="AmbiguousHandlerScanStartupWarning"/> reports it -- by then the
/// handler registry itself has already kept only the winner and lost the others.
/// </remarks>
internal sealed record AmbiguousHandlerScanFinding(Type MessageType, string InterfaceName, IReadOnlyList<Type> HandlerTypes);

/// <summary>
/// Reports, once at start-up, every ambiguous single-handler registration an assembly scan found.
/// </summary>
/// <remarks>
/// Warns rather than throws: a broad assembly scan legitimately finding a second implementation of a
/// single-handler interface is not always a mistake -- a test assembly or a sample may deliberately carry
/// an alternate/experimental handler alongside the real one for manual registration elsewhere, and
/// throwing at composition time would make that ordinary shape a startup failure. Naming the ambiguity
/// loudly, without picking a side, is the same asymmetry <see cref="NoHandlersRegisteredStartupWarning"/>
/// already applies to the zero-handler case.
/// </remarks>
internal sealed partial class AmbiguousHandlerScanStartupWarning(
	IEnumerable<AmbiguousHandlerScanFinding> findings,
	ILogger<AmbiguousHandlerScanStartupWarning> logger) : IHostedService
{
	private readonly IEnumerable<AmbiguousHandlerScanFinding> _findings =
		findings ?? throw new ArgumentNullException(nameof(findings));

	private readonly ILogger<AmbiguousHandlerScanStartupWarning> _logger =
		logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		foreach (var finding in _findings)
		{
			LogAmbiguousHandlerScan(
				_logger,
				finding.MessageType.FullName ?? finding.MessageType.Name,
				finding.InterfaceName,
				finding.HandlerTypes.Count,
				string.Join(", ", finding.HandlerTypes.Select(static t => t.FullName ?? t.Name)));
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(
		EventId = 2410,
		Level = LogLevel.Warning,
		Message = "Assembly scan found {HandlerCount} handlers for {MessageType}, which takes exactly one "
			+ "({InterfaceName}): {HandlerTypes}. Which one runs depends on an undocumented, unstable type "
			+ "enumeration order. Remove or rename all but the intended handler, or register it explicitly "
			+ "before scanning.")]
	private static partial void LogAmbiguousHandlerScan(
		ILogger logger, string messageType, string interfaceName, int handlerCount, string handlerTypes);
}
