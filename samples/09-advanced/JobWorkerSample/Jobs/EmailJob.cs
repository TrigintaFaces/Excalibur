// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;

namespace JobWorkerSample.Jobs;

/// <summary>
///     A sample job that sends emails using configuration provided as context.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="EmailJob" /> class.
/// </remarks>
/// <param name="logger"> The logger instance. </param>
public class EmailJob(ILogger<EmailJob> logger) : IBackgroundJob<EmailConfiguration>
{
	private readonly ILogger<EmailJob> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task ExecuteAsync(EmailConfiguration context, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(context);

		_logger.LogInformation("Starting email job with configuration {@EmailConfig}",
			new { context.SmtpServer, context.Port, context.Username });

		try
		{
			await ValidateConfigurationAsync(context, cancellationToken).ConfigureAwait(false);
			await SendWeeklyReportEmailAsync(context, cancellationToken).ConfigureAwait(false);
			await SendNotificationEmailsAsync(context, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Email job completed successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Email job failed");
			throw;
		}
	}

	/// <summary>
	///     Validates the email configuration.
	/// </summary>
	/// <param name="config"> The email configuration. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task ValidateConfigurationAsync(EmailConfiguration config, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Validating email configuration");

		// Simulate configuration validation
		await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);

		if (string.IsNullOrEmpty(config.SmtpServer))
		{
			throw new InvalidOperationException("SMTP server is not configured");
		}

		if (config.Port is <= 0 or > 65535)
		{
			throw new InvalidOperationException($"Invalid SMTP port: {config.Port}");
		}

		_logger.LogDebug("Email configuration validated successfully");
	}

	/// <summary>
	///     Simulates sending a weekly report email.
	/// </summary>
	/// <param name="config"> The email configuration. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task SendWeeklyReportEmailAsync(EmailConfiguration config, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Preparing weekly report email");

		// Simulate report generation
		await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);

		var reportData = new
		{
			WeekStart = DateTimeOffset.UtcNow.AddDays(-7).Date,
			WeekEnd = DateTimeOffset.UtcNow.Date,
			TotalJobs = Random.Shared.Next(100, 500),
			SuccessfulJobs = Random.Shared.Next(90, 99),
			FailedJobs = Random.Shared.Next(1, 10),
			AverageExecutionTime = TimeSpan.FromMilliseconds(Random.Shared.Next(100, 2000))
		};

		// Simulate sending email
		await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

		_logger.LogInformation("Weekly report email sent via {SmtpServer}:{Port} with data: {@ReportData}",
			config.SmtpServer, config.Port, reportData);
	}

	/// <summary>
	///     Simulates sending notification emails.
	/// </summary>
	/// <param name="config"> The email configuration. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task SendNotificationEmailsAsync(EmailConfiguration config, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Sending notification emails");

		var notifications = new[]
		{
			"System maintenance scheduled for next weekend", "New feature deployment completed successfully",
			"Monthly backup verification completed"
		};

		foreach (var notification in notifications)
		{
			// Simulate sending each notification
			await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Notification sent: {Notification}", notification);
		}

		_logger.LogInformation("All notification emails sent successfully via {SmtpServer}", config.SmtpServer);
	}
}

/// <summary>
///     Configuration for email sending operations.
/// </summary>
public class EmailConfiguration
{
	/// <summary>
	///     Gets or sets the SMTP server hostname.
	/// </summary>
	public string SmtpServer { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the SMTP server port.
	/// </summary>
	public int Port { get; set; } = 587;

	/// <summary>
	///     Gets or sets the SMTP username.
	/// </summary>
	public string Username { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets whether to use SSL/TLS encryption.
	/// </summary>
	public bool UseSsl { get; set; } = true;

	/// <summary>
	///     Gets or sets the default sender email address.
	/// </summary>
	public string DefaultFrom { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the email template to use.
	/// </summary>
	public string Template { get; set; } = "default";
}
