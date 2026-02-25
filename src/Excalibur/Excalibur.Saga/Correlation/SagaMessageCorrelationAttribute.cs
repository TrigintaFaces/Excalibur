// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Correlation;

/// <summary>
/// Marks a message property for automatic saga correlation.
/// The decorated property value will be used to locate the target saga instance.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to message properties that contain saga instance identifiers.
/// The <see cref="ConventionBasedCorrelator"/> discovers these attributes at runtime
/// and uses them to route messages to the correct saga instance.
/// </para>
/// <para>
/// When no property is decorated with this attribute, the correlator falls back
/// to convention-based matching using well-known property names such as
/// <c>CorrelationId</c> and <c>SagaId</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderPaymentReceived
/// {
///     [SagaMessageCorrelation]
///     public string OrderId { get; set; }
///
///     public decimal Amount { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SagaMessageCorrelationAttribute : Attribute
{
}
