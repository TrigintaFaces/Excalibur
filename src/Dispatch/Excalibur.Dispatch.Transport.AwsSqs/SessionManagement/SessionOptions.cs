// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Options for session management.
/// </summary>
public sealed class SessionOptions
{
	/// <summary>
	/// Gets or sets the default session timeout.
	/// </summary>
	/// <value>
	/// The default session timeout.
	/// </value>
	public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the lock renewal interval.
	/// </summary>
	/// <value>
	/// The lock renewal interval.
	/// </value>
	public TimeSpan LockRenewalInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the maximum session idle time before expiration.
	/// </summary>
	/// <value>
	/// The maximum session idle time before expiration.
	/// </value>
	public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic lock renewal.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable automatic lock renewal.
	/// </value>
	public bool EnableAutoRenewal { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of concurrent sessions.
	/// </summary>
	/// <value>
	/// The maximum number of concurrent sessions.
	/// </value>
	public int MaxConcurrentSessions { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to enable session persistence.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable session persistence.
	/// </value>
	public bool EnablePersistence { get; set; }

	/// <summary>
	/// Gets or sets the session cleanup interval.
	/// </summary>
	/// <value>
	/// The session cleanup interval.
	/// </value>
	public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Gets or sets the connection string for Redis session store.
	/// </summary>
	/// <value>
	/// The connection string for Redis session store.
	/// </value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the key prefix for Redis session store.
	/// </summary>
	/// <value>
	/// The key prefix for Redis session store.
	/// </value>
	public string KeyPrefix { get; set; } = "sessions:";

	/// <summary>
	/// Gets or sets the default expiry time for Redis sessions.
	/// </summary>
	/// <value>
	/// The default expiry time for Redis sessions.
	/// </value>
	public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>
	/// Gets or sets the lock timeout for session management.
	/// </summary>
	/// <value>
	/// The lock timeout for session management.
	/// </value>
	public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
