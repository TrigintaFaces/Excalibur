// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Conformance.Transport;

/// <summary>
/// Records which transport conformance arms actually EXECUTED in this run, so the run can be asked the one
/// question a green result does not answer: did anything get verified?
/// </summary>
/// <remarks>
/// <para>
/// Every gated arm in <see cref="TransportConformanceTestBase{TSender,TReceiver}" /> skips when its transport
/// cannot be initialized. Skips are not failures, so an assembly in which NOTHING ran reports exactly what an
/// assembly in which everything passed reports. This ledger is the difference between those two states, made
/// observable.
/// </para>
/// <para>
/// Only arms from transports backed by an external broker are counted. An in-memory transport needs no
/// infrastructure, so it executes whether or not the container runtime is up; counting it would make the
/// liveness gate green in precisely the situation the gate exists to catch.
/// </para>
/// </remarks>
internal static class ConformanceExecutionLedger
{
	private static readonly ConcurrentDictionary<string, byte> BrokerArms = new(StringComparer.Ordinal);
	private static readonly ConcurrentDictionary<string, byte> BrokerSuitesAttempted = new(StringComparer.Ordinal);
	private static readonly ConcurrentDictionary<string, string> UnavailableTransports = new(StringComparer.Ordinal);

	/// <summary>
	/// Records that an external-broker conformance suite began initialization.
	/// </summary>
	/// <remarks>
	/// This is what makes the liveness gate safe under a test filter. The CI matrix runs a single transport
	/// by name, so a gate that unconditionally demanded a broker arm would fail a job that was never asked
	/// to run one. Recording the ATTEMPT lets the gate distinguish "no broker suite was selected" (nothing to
	/// say) from "broker suites were selected and not one arm ran" (the defect).
	/// </remarks>
	internal static void RecordBrokerSuiteAttempted(string transport) =>
		BrokerSuitesAttempted.TryAdd(transport, 0);

	/// <summary>
	/// Records that a conformance arm passed its availability gate and is executing its body.
	/// </summary>
	/// <param name="transport">The conformance suite type name (e.g. <c>KafkaTransportConformanceTests</c>).</param>
	/// <param name="arm">The conformance fact's name.</param>
	/// <param name="usesExternalBroker">
	/// Whether this transport talks to real infrastructure. Only these count toward liveness.
	/// </param>
	internal static void RecordArmExecuted(string transport, string arm, bool usesExternalBroker)
	{
		if (usesExternalBroker)
		{
			_ = BrokerArms.TryAdd($"{transport}.{arm}", 0);
		}
	}

	/// <summary>
	/// Records that a transport could not be initialized, so its skip lines can be reproduced in the
	/// liveness failure rather than leaving the reader to hunt for them in the log.
	/// </summary>
	internal static void RecordTransportUnavailable(string transport, string reason) =>
		UnavailableTransports[transport] = reason;

	/// <summary>
	/// Gets the number of DISTINCT external-broker conformance arms that executed in this run.
	/// </summary>
	internal static int BrokerArmsExecuted => BrokerArms.Count;

	/// <summary>
	/// Gets the number of external-broker conformance suites that began initialization in this run.
	/// </summary>
	internal static int BrokerSuitesSelected => BrokerSuitesAttempted.Count;

	/// <summary>
	/// Gets a human-readable account of what was recorded, used to make a liveness failure diagnosable.
	/// </summary>
	internal static string Describe()
	{
		var executed = BrokerArms.Keys.OrderBy(static k => k, StringComparer.Ordinal).ToList();
		var unavailable = UnavailableTransports
			.OrderBy(static kvp => kvp.Key, StringComparer.Ordinal)
			.Select(static kvp => $"  - {kvp.Key}: {kvp.Value}")
			.ToList();

		var executedText = executed.Count == 0
			? "  (none)"
			: string.Join(Environment.NewLine, executed.Select(static a => $"  - {a}"));
		var unavailableText = unavailable.Count == 0
			? "  (none recorded)"
			: string.Join(Environment.NewLine, unavailable);

		return $"External-broker suites selected: {BrokerSuitesAttempted.Count}"
			+ $"{Environment.NewLine}External-broker conformance arms executed ({executed.Count}):{Environment.NewLine}{executedText}"
			+ $"{Environment.NewLine}Transports reported unavailable:{Environment.NewLine}{unavailableText}";
	}
}
