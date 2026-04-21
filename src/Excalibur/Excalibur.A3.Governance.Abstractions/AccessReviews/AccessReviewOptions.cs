// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.A3.Governance.AccessReviews;

/// <summary>
/// Configuration options for access review campaigns.
/// </summary>
public sealed class AccessReviewOptions
{
	/// <summary>
	/// Gets or sets the default duration for new campaigns.
	/// </summary>
	/// <value>Defaults to 30 days.</value>
	public TimeSpan DefaultCampaignDuration { get; set; } = TimeSpan.FromDays(30);

	/// <summary>
	/// Gets or sets the default expiry policy for new campaigns.
	/// </summary>
	/// <value>Defaults to <see cref="AccessReviewExpiryPolicy.NotifyAndExtend"/>.</value>
	public AccessReviewExpiryPolicy DefaultExpiryPolicy { get; set; } = AccessReviewExpiryPolicy.NotifyAndExtend;

	/// <summary>
	/// Gets or sets the interval at which the background service checks for expired campaigns.
	/// </summary>
	/// <value>Defaults to 1 hour.</value>
	public TimeSpan ExpiryCheckInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the maximum retry attempts for auto-revoke operations.
	/// </summary>
	/// <value>Defaults to 3.</value>
	[Range(1, 10, ErrorMessage = "MaxRetryAttempts must be between 1 and 10.")]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay for exponential backoff on retry.
	/// </summary>
	/// <value>Defaults to 5 seconds.</value>
	public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets whether campaigns are automatically started upon creation.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	public bool AutoStartOnCreation { get; set; }

	/// <summary>
	/// Gets or sets the number of days a campaign deadline is extended when the
	/// <see cref="AccessReviewExpiryPolicy.NotifyAndExtend"/> policy is applied.
	/// </summary>
	/// <value>Defaults to 7.</value>
	[Range(1, 90, ErrorMessage = "ExtensionDays must be between 1 and 90.")]
	public int ExtensionDays { get; set; } = 7;
}
