// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.AuditLogging.GoogleCloud;

/// <summary>
/// Internal implementation of the Google Cloud audit builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class AuditLoggingGoogleCloudBuilder : IAuditLoggingGoogleCloudBuilder
{
	private readonly GoogleCloudAuditOptions _options;

	internal AuditLoggingGoogleCloudBuilder(GoogleCloudAuditOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal string? BindConfigurationPath { get; private set; }

	public IAuditLoggingGoogleCloudBuilder ProjectId(string projectId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
		_options.ProjectId = projectId;
		BindConfigurationPath = null;
		return this;
	}

	public IAuditLoggingGoogleCloudBuilder LogName(string logName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(logName);
		_options.LogName = logName;
		return this;
	}

	public IAuditLoggingGoogleCloudBuilder CredentialsPath(string credentialsPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(credentialsPath);
		// Credentials path can be used by the exporter to load ADC or service account
		// Store via environment convention -- the exporter reads GOOGLE_APPLICATION_CREDENTIALS
		Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);
		return this;
	}

	public IAuditLoggingGoogleCloudBuilder CredentialsJson(string credentialsJson)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(credentialsJson);
		// Store inline JSON credentials -- the exporter can consume via temp file or env
		// This is a convenience for test/dev scenarios
		var tempPath = Path.Combine(Path.GetTempPath(), $"gcloud-audit-{Guid.NewGuid():N}.json");
		File.WriteAllText(tempPath, credentialsJson);
		Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", tempPath);
		return this;
	}

	public IAuditLoggingGoogleCloudBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		return this;
	}
}
