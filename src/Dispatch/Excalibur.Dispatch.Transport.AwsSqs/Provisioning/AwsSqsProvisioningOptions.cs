// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Controls optional, opt-in provisioning of AWS SQS and SNS infrastructure at startup.
/// </summary>
/// <remarks>
/// <para>
/// By default the transport assumes queues, dead-letter redrive policies, and SNS subscriptions
/// are pre-provisioned (for example by infrastructure-as-code). When provisioning is enabled the
/// transport applies the configured dead-letter redrive policy to the source queue and creates the
/// configured SNS-to-SQS subscriptions with their filter policies when the host starts.
/// </para>
/// <para>
/// Provisioning is <b>opt-in</b> (<see cref="Enabled"/> defaults to <see langword="false"/>) because
/// a messaging framework must not mutate cloud infrastructure unless the operator explicitly asks it
/// to. Provisioning is <b>fail-open</b> when <see cref="FailOpen"/> is <see langword="true"/>: a
/// missing permission or transient error is logged and start-up continues, rather than crashing the host.
/// </para>
/// </remarks>
public sealed class AwsSqsProvisioningOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether startup provisioning is enabled.
	/// </summary>
	/// <value><see langword="true"/> to apply redrive policies and create SNS subscriptions at startup; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether provisioning failures are non-fatal.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to log and continue when provisioning fails (for example due to missing
	/// IAM permissions); <see langword="false"/> to surface the failure. Default is <see langword="true"/>.
	/// </value>
	public bool FailOpen { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to apply the dead-letter redrive policy to the source queue.
	/// </summary>
	/// <value><see langword="true"/> to set the redrive policy when a dead-letter queue is configured; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool ApplyDeadLetterRedrivePolicy { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to create the configured SNS-to-SQS subscriptions and filter policies.
	/// </summary>
	/// <value><see langword="true"/> to create subscriptions and apply filter policies; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool CreateSnsSubscriptions { get; set; } = true;
}
