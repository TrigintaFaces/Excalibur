// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Vault;

/// <summary>
/// Internal implementation of the HashiCorp Vault compliance builder.
/// BindConfiguration uses last-wins semantics against programmatic setters.
/// </summary>
internal sealed class ComplianceVaultBuilder : IComplianceVaultBuilder
{
	private readonly VaultOptions _options;

	internal ComplianceVaultBuilder(VaultOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal string? BindConfigurationPath { get; private set; }

	public IComplianceVaultBuilder VaultUri(Uri vaultUri)
	{
		ArgumentNullException.ThrowIfNull(vaultUri);
		_options.VaultUri = vaultUri;
		BindConfigurationPath = null;
		return this;
	}

	public IComplianceVaultBuilder TransitMountPath(string mountPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mountPath);
		_options.Keys.TransitMountPath = mountPath;
		return this;
	}

	public IComplianceVaultBuilder KeyNamePrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.Keys.KeyNamePrefix = prefix;
		return this;
	}

	public IComplianceVaultBuilder Namespace(string ns)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ns);
		_options.Namespace = ns;
		return this;
	}

	public IComplianceVaultBuilder EnableDetailedTelemetry(bool enable = true)
	{
		_options.EnableDetailedTelemetry = enable;
		return this;
	}

	public IComplianceVaultBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		return this;
	}

	/// <summary>
	/// Projects the fields this builder owns — and ONLY those — onto <paramref name="opt"/>, field by field.
	/// </summary>
	/// <remarks>
	/// this is the single source of truth for "which fields the
	/// builder configures". Each fluent setter above has exactly one corresponding field-level assignment
	/// here, co-located in the same class — so a new setter extends the projection in one place and the
	/// cross-file manual-allowlist desync the original nit warned about becomes inexpressible. The builder
	/// MUST NOT touch <c>Auth</c>/<c>Retry</c>/<c>Suspension</c>/<c>MetadataCacheDuration</c>/<c>HttpTimeout</c>
	/// or replace whole <c>Keys</c>: those are owned by the consumer's own <c>Configure&lt;VaultOptions&gt;</c>
	/// / <c>BindConfiguration</c>, and a wholesale copy here would clobber a consumer's prior configuration
	/// with builder defaults (the regression this projection exists to prevent — field-level, never object-level).
	/// </remarks>
	/// <param name="opt">The DI-managed options instance to project the builder-owned fields onto.</param>
	internal void ApplyConfiguredFieldsTo(VaultOptions opt)
	{
		ArgumentNullException.ThrowIfNull(opt);

		opt.VaultUri = _options.VaultUri;
		opt.Keys.TransitMountPath = _options.Keys.TransitMountPath;
		opt.Keys.KeyNamePrefix = _options.Keys.KeyNamePrefix;
		opt.Namespace = _options.Namespace;
		opt.EnableDetailedTelemetry = _options.EnableDetailedTelemetry;
	}
}
