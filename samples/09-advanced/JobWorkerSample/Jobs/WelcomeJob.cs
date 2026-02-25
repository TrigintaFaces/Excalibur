// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;

namespace JobWorkerSample.Jobs;

/// <summary>
///     A sample job that displays a welcome message after the application starts.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="WelcomeJob" /> class.
/// </remarks>
/// <param name="logger"> The logger instance. </param>
public class WelcomeJob(ILogger<WelcomeJob> logger) : IBackgroundJob
{
	private readonly ILogger<WelcomeJob> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task ExecuteAsync(CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Executing welcome job at {Timestamp}", DateTimeOffset.UtcNow);

		try
		{
			await DisplayWelcomeMessageAsync(cancellationToken).ConfigureAwait(false);
			await LogApplicationStatusAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Welcome job completed successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Welcome job failed");
			throw;
		}
	}

	/// <summary>
	///     Displays a welcome message.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task DisplayWelcomeMessageAsync(CancellationToken cancellationToken)
	{
		await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);

		var welcomeMessage = $"""

		                      ╔══════════════════════════════════════════════════════════════╗
		                      ║ 🎉 Welcome to Excalibur Jobs! 🎉 ║
		                      ╠══════════════════════════════════════════════════════════════╣
		                      ║ ║
		                      ║ Your Job Worker Service is now running successfully! ║
		                      ║ ║
		                      ║ This sample demonstrates: ║
		                      ║ • Individual job configuration with fluent API ║
		                      ║ • Different job scheduling patterns (cron, interval, etc.) ║
		                      ║ • Job context and parameter passing ║
		                      ║ • Conditional job registration ║
		                      ║ • Multiple instances of the same job type ║
		                      ║ ║
		                      ║ Application started at: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC} ║
		                      ║ ║
		                      ╚══════════════════════════════════════════════════════════════╝

		                      """;

		_logger.LogInformation("{WelcomeMessage}", welcomeMessage);
	}

	/// <summary>
	///     Logs the current application status.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	private async Task LogApplicationStatusAsync(CancellationToken cancellationToken)
	{
		await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);

		var statusInfo = new
		{
			ApplicationName = "JobWorkerSample",
			Version = "1.0.0",
			Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
			StartupTime = DateTimeOffset.UtcNow,
			Environment.ProcessId,
			ThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count
		};

		_logger.LogInformation("Application Status: {@StatusInfo}", statusInfo);
	}
}
