// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Elastic.Clients.Elasticsearch;

using Excalibur.AuditLogging;
using Excalibur.Compliance;
using Excalibur.Data.ElasticSearch.Internal;
using Excalibur.Data.ElasticSearch.Security;
using Excalibur.Data.ElasticSearch.Security.Auditing;
using Excalibur.Dispatch.Telemetry;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Elasticsearch security services in the dependency injection container.
/// </summary>
public static class SecurityServiceCollectionExtensions
{
	/// <summary>
	/// Adds comprehensive Elasticsearch security services to the specified service collection.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The configuration to bind security settings from. </param>
	/// <param name="configureOptions"> Optional action to configure security settings. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configuration is null. </exception>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddElasticsearchSecurity(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<ElasticsearchSecurityOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		ArgumentNullException.ThrowIfNull(configuration);

		// Configure security settings
		var securitySection = configuration.GetSection("Elasticsearch:Security");
		_ = services.AddOptions<ElasticsearchSecurityOptions>()
			.Bind(securitySection)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ElasticsearchSecurityOptions>, ElasticsearchSecurityOptionsValidator>());

		if (configureOptions != null)
		{
			_ = services.Configure(configureOptions);
		}

		// Register core security services
		_ = services.AddSecurityCore();
		_ = services.AddAuthentication(configuration);
		_ = services.AddFieldEncryption();
		_ = services.AddKeyManagement(configuration);
		_ = services.AddSecurityAuditing();
		_ = services.AddSecurityMonitoring(configuration);

		return services;
	}

	/// <summary>
	/// Adds authentication services with configurable providers.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The configuration to bind authentication settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddAuthentication(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// Configure authentication settings
		_ = services.AddOptions<AuthenticationOptions>()
			.Bind(
				configuration.GetSection("Elasticsearch:Security:Authentication"),
				static binder => binder.ErrorOnUnknownConfiguration = true)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuthenticationOptions>, AuthenticationOptionsValidator>());

		// The provider takes an IHttpClientFactory (OAuth2 token acquisition and refresh go through the
		// named "ElasticsearchOAuth2" client). Nothing else in this package registers one, so without this
		// call the provider is advertised by the registration above and cannot be constructed. Registering
		// the factory here rather than newing an HttpClient keeps handler lifetime, pooling and DNS
		// refresh in the framework's hands.
		_ = services.AddHttpClient();

		// The connection-credential store backing the authentication provider's OAuth tokens, service-account
		// secrets, passwords, and API keys. TryAdd: a consumer registering their own IElasticsearchKeyStorage
		// (e.g. an Azure Key Vault-backed one) before this call keeps it; otherwise the in-memory development
		// default applies. This is a distinct concern from field-encryption key management -- see
		// AddKeyManagement below.
		services.TryAddSingleton<IElasticsearchKeyStorage, LocalKeyProvider>();

		// Register authentication provider
		services.TryAddSingleton<IElasticsearchAuthenticationProvider, SecureElasticsearchAuthenticationProvider>();

		return services;
	}

	/// <summary>
	/// Configures Azure Key Vault as the connection-credential store: OAuth tokens, service-account secrets,
	/// passwords, and API keys used to authenticate to Elasticsearch itself.
	/// </summary>
	/// <remarks>
	/// This is not encryption-key custody -- call it BEFORE <see cref="AddAuthentication"/> (or
	/// <see cref="AddElasticsearchSecurity"/>, which calls it) so the registration below wins over the
	/// in-memory development default via <c>TryAdd</c>. For cloud-backed field-encryption keys, see
	/// <see cref="AddKeyManagement"/> instead.
	/// </remarks>
	/// <param name="services"> The service collection to add the credential store to. </param>
	/// <param name="configuration"> The configuration to bind Azure Key Vault settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configuration is null. </exception>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddAzureKeyVaultCredentialStorage(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Nested under the same parent section AddKeyManagement reads its own settings from, so that a
		// consumer who puts their key-management configuration in one place has all of it bound.
		_ = services.AddOptions<AzureKeyVaultOptions>()
			.Bind(configuration.GetSection("Elasticsearch:Security:Encryption:KeyManagement:AzureKeyVault"))
			.ValidateOnStart();

		services.TryAddSingleton<IElasticsearchKeyStorage, AzureKeyVaultProvider>();

		return services;
	}

	/// <summary>
	/// Adds field-level encryption services.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddFieldEncryption(this IServiceCollection services)
	{
		// Register field encryption service (concrete + parent + sub-interfaces forwarded to same singleton)
		services.TryAddSingleton<FieldEncryptor>();
		services.TryAddSingleton<IElasticsearchFieldEncryptor>(static sp => sp.GetRequiredService<FieldEncryptor>());
		services.TryAddSingleton<IElasticsearchFieldEncryption>(static sp => sp.GetRequiredService<FieldEncryptor>());
		services.TryAddSingleton<IElasticsearchFieldEncryptionPolicy>(static sp => sp.GetRequiredService<FieldEncryptor>());
		services.TryAddSingleton<IElasticsearchFieldEncryptionMaintenance>(static sp => sp.GetRequiredService<FieldEncryptor>());
		services.TryAddSingleton<IElasticsearchFieldEncryptorEvents>(static sp => sp.GetRequiredService<FieldEncryptor>());

		return services;
	}

	/// <summary>
	/// Adds field-encryption key management: the <see cref="Excalibur.Compliance.IKeyManagementProvider"/> and
	/// <see cref="Excalibur.Compliance.IEncryptionProviderRegistry"/> that <see cref="FieldEncryptor"/> resolves
	/// keys and performs cryptographic operations through.
	/// </summary>
	/// <remarks>
	/// This package never implements cloud-backed key custody itself -- that is <c>Excalibur.Compliance.Azure</c>
	/// / <c>.Aws</c> / <c>.Vault</c>'s job, following the exact pattern every other Compliance-backed subsystem
	/// (SqlServer encryption, event-store encryption) already uses. A consumer wanting a cloud-backed provider
	/// registers the corresponding Compliance package's key-management extension (e.g.
	/// <c>services.AddEncryption(e =&gt; e.UseKeyManagement&lt;AzureKeyVaultProvider&gt;())</c>) BEFORE calling
	/// <see cref="AddElasticsearchSecurity"/>, and names that provider in configuration; this method only
	/// verifies the registration exists (or supplies the in-process development default).
	/// </remarks>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The configuration to bind key management settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddKeyManagement(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// Configure key management settings
		var keyManagementSection = configuration.GetSection("Elasticsearch:Security:Encryption:KeyManagement");
		_ = services.AddOptions<KeyManagementOptions>()
			.Bind(keyManagementSection)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<KeyManagementOptions>, KeyManagementOptionsValidator>());

		// The in-process development provider holds key material in memory only, so field data
		// encrypted under it is permanently unreadable after the next restart -- and nothing reports
		// the loss at the point it happens, because encryption itself keeps appearing to work. It is
		// therefore only ever selected when a consumer names it. An absent Provider key binds to the
		// zero-valued enum member, which is indistinguishable from a deliberate choice, so the absence
		// is detected from the raw configuration value rather than from the bound enum.
		//
		// The refusal is scoped to compositions where field-level encryption is actually enabled:
		// AddElasticsearchSecurity always calls this method, and a consumer using authentication,
		// auditing or monitoring without field encryption has no key material at risk.
		var provider = keyManagementSection.GetValue<KeyManagementProvider>("Provider");
		var providerWasNamed = keyManagementSection["Provider"] is not null;
		var fieldEncryptionEnabled = configuration.GetValue<bool>(
			"Elasticsearch:Security:Encryption:FieldLevelEncryption");

		if (fieldEncryptionEnabled && !providerWasNamed)
		{
			throw new InvalidOperationException(
				"Elasticsearch field-level encryption is enabled but no key-management provider is " +
				"configured. Set 'Elasticsearch:Security:Encryption:KeyManagement:Provider' to the " +
				"provider that holds your keys. To use the in-process development provider, name it " +
				$"explicitly as '{nameof(KeyManagementProvider.Local)}' -- it keeps keys in memory " +
				"only, loses them on restart, and must not be used in production.");
		}

		return provider == KeyManagementProvider.Local
			? services.AddDevEncryption()
			: services.RequireExternallySuppliedKeyManagement(provider.ToString());
	}

	/// <summary>
	/// Completes registration for a cloud key-management provider that this package does not implement.
	/// </summary>
	/// <remarks>
	/// Fails closed by design. A cloud key service and the in-process development provider have entirely
	/// different durability and custody properties, so substituting one for the other silently would leave a
	/// host believing its keys were held in a managed key service when they were held in memory. There is
	/// deliberately no code path here that binds the development provider; the only ways forward are a
	/// caller-supplied <see cref="Excalibur.Compliance.IKeyManagementProvider"/> (registered by, for example,
	/// <c>Excalibur.Compliance.Azure</c>) or an explicit configured choice of the local provider.
	/// </remarks>
	private static IServiceCollection RequireExternallySuppliedKeyManagement(
		this IServiceCollection services,
		string? providerName)
	{
		ArgumentNullException.ThrowIfNull(services);

		var hasKeyManagement = services.Any(static d => d.ServiceType == typeof(IKeyManagementProvider));
		var hasRegistry = services.Any(static d => d.ServiceType == typeof(IEncryptionProviderRegistry));

		if (!hasKeyManagement || !hasRegistry)
		{
			throw new NotSupportedException(
				$"{providerName} key management is not implemented inside Excalibur.Data.ElasticSearch. Register " +
				$"the corresponding Excalibur.Compliance key-management package (e.g. Excalibur.Compliance.Azure's " +
				$"AzureKeyVaultProvider via services.AddEncryption(e => e.UseKeyManagement<AzureKeyVaultProvider>())) " +
				"before configuring Elasticsearch security, or set 'Elasticsearch:Security:Encryption:KeyManagement:" +
				$"Provider' to '{nameof(KeyManagementProvider.Local)}' to choose the in-process development provider " +
				"explicitly. The development provider keeps keys in memory only, loses them on restart, and must " +
				"not be used in production.");
		}

		return services;
	}

	/// <summary>
	/// Adds security auditing services.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddSecurityAuditing(this IServiceCollection services)
	{
		// The auditor takes the monitoring settings as IOptions, so they must be registered here too --
		// this entry point is callable on its own. Registering the options twice is additive, so a host
		// that also calls AddSecurityMonitoring(configuration) still gets the bound values.
		_ = services.AddOptions<SecurityMonitoringOptions>().ValidateOnStart();
		services.AddSecurityMonitoringOptionsValidator();

		// Shared audit-integrity strategy (keyed-MAC) + default options-backed signing-key provider, keyed
		// from AuditIntegrityOptions — one key-config story across every audit sink. Both register via
		// TryAddSingleton, so a consumer can override either (e.g. a KMS-backed IAuditSigningKeyProvider)
		// by registering it first. With no key configured the default fails closed (never an unprotected tag).
		_ = services.AddAuditIntegrity();

		// Default ES-local PII sanitizer for audit events (secure-by-default,): masking works
		// zero-config without pulling core Excalibur.Dispatch/Compliance in. Consumers can override with a
		// richer ITelemetrySanitizer (e.g. AddDispatchObservability's HashingTelemetrySanitizer) via a
		// non-Try registration before this call.
		services.TryAddSingleton<ITelemetrySanitizer, DefaultAuditTelemetrySanitizer>();

		// The auditor drives its compliance-report and audit-queue-drain schedules from TimeProvider, so the
		// wall clock is a registered dependency a test or a consumer can substitute rather than an ambient one.
		services.TryAddSingleton(TimeProvider.System);

		// The flush target, registered so it is a dependency rather than something the auditor derives
		// for itself. The default is the same adapter over the same client the auditor would have built,
		// so nothing changes for a consumer who supplies nothing; what changes is that the seam the
		// drain-flush locks drive is now reachable through this registration instead of only through the
		// constructor that takes it.
		services.TryAddSingleton<ISecurityAuditStore>(static sp =>
			new SecurityAuditStoreAdapter(sp.GetRequiredService<ElasticsearchClient>()));

		// Registered as an explicit factory rather than by type. Container construction considers only
		// PUBLIC constructors, and every public SecurityAuditor constructor derives its own audit store
		// from the client -- so a type registration silently ignores the ISecurityAuditStore above and
		// the store is substitutable in tests that construct the auditor directly and nowhere else.
		// Naming the constructor is what makes the registered flush target the one that actually runs.
		services.TryAddSingleton(static sp => new SecurityAuditor(
			sp.GetRequiredService<ElasticsearchClient>(),
			sp.GetRequiredService<ISecurityAuditStore>(),
			sp.GetRequiredService<IOptions<AuditOptions>>(),
			sp.GetRequiredService<IOptions<SecurityMonitoringOptions>>(),
			sp.GetRequiredService<IAuditIntegrityStrategy>(),
			sp.GetService<ITelemetrySanitizer>(),
			sp.GetRequiredService<TimeProvider>(),
			sp.GetRequiredService<ILogger<SecurityAuditor>>()));

		// Register auditing service (core + parent + sub-interfaces forwarded to the same singleton)
		services.TryAddSingleton<IElasticsearchSecurityAuditor>(static sp => sp.GetRequiredService<SecurityAuditor>());
		services.TryAddSingleton<IElasticsearchSecurityAuditorCore>(static sp => sp.GetRequiredService<SecurityAuditor>());
		services.TryAddSingleton<IElasticsearchSecurityAuditorRecording>(static sp => sp.GetRequiredService<SecurityAuditor>());
		services.TryAddSingleton<IElasticsearchSecurityAuditorEvents>(static sp => sp.GetRequiredService<SecurityAuditor>());
		services.TryAddSingleton<IElasticsearchSecurityAuditorReporting>(static sp => sp.GetRequiredService<SecurityAuditor>());
		services.TryAddSingleton<IElasticsearchSecurityAuditorMaintenance>(static sp => sp.GetRequiredService<SecurityAuditor>());

		// Fail the host fast at startup when EnsureLogIntegrity=true but the signing-key provider
		// cannot produce a key — provider-agnostic (default or KMS-backed), so the misconfiguration surfaces
		// before the first audit write rather than failing closed silently at runtime. Same shape as
		// SecurityMonitoringOptions above: IValidateOptions + ValidateOnStart(), not a hand-rolled
		// IHostedService -- AuditOptions is declared only in this package, so there is no separate
		// host-less-composition placement question to answer.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditOptions>, AuditSigningKeyStartupProbe>());
		_ = services.AddOptions<AuditOptions>().ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds security monitoring and threat detection services using the built-in defaults.
	/// </summary>
	/// <param name="services"> The service collection to register the monitoring services in. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <remarks>
	/// Use the overload taking an <see cref="IConfiguration"/> to bind the monitoring settings from
	/// configuration.
	/// </remarks>
	public static IServiceCollection AddSecurityMonitoring(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// The monitoring services take their settings as IOptions, so the options must be registered
		// even when there is nothing to bind -- otherwise nothing can construct them and the host
		// fails at start, when it resolves the monitoring background service.
		_ = services.AddOptions<SecurityMonitoringOptions>().ValidateOnStart();
		services.AddSecurityMonitoringOptionsValidator();

		return services.AddSecurityMonitoringServices();
	}

	/// <summary>
	/// Adds security monitoring and threat detection services, binding their settings from configuration.
	/// </summary>
	/// <param name="services"> The service collection to register the monitoring services in. </param>
	/// <param name="configuration"> The configuration to bind monitoring settings from. </param>
	/// <returns> The service collection for method chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddSecurityMonitoring(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// The settings are init-only, which a Configure delegate cannot set -- an init accessor is not
		// callable once construction is over. The configuration binder assigns them by reflection, so
		// binding is the mechanism that works here, and it is the same one every sibling settings type
		// in this package already uses.
		_ = services.AddOptions<SecurityMonitoringOptions>()
			.Bind(
				configuration.GetSection("Elasticsearch:Security:Monitoring"),
				static binder => binder.ErrorOnUnknownConfiguration = true)
			.ValidateOnStart();
		services.AddSecurityMonitoringOptionsValidator();

		return services.AddSecurityMonitoringServices();
	}

	private static IServiceCollection AddSecurityMonitoringServices(this IServiceCollection services)
	{
		// Register monitoring service (concrete + parent + sub-interfaces forwarded to same singleton)
		services.TryAddSingleton<SecurityMonitor>();
		services.TryAddSingleton<IElasticsearchSecurityMonitor>(static sp => sp.GetRequiredService<SecurityMonitor>());
		services.TryAddSingleton<IElasticsearchSecurityAnalysis>(static sp => sp.GetRequiredService<SecurityMonitor>());
		services.TryAddSingleton<IElasticsearchSecurityAlerting>(static sp => sp.GetRequiredService<SecurityMonitor>());
		services.TryAddSingleton<IElasticsearchSecurityMonitorEvents>(static sp => sp.GetRequiredService<SecurityMonitor>());

		// Register background service for monitoring
		_ = services.AddHostedService<SecurityMonitoringBackgroundService>();

		return services;
	}

	/// <summary>
	/// Adds core security services and infrastructure.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for method chaining. </returns>
	private static IServiceCollection AddSecurityCore(this IServiceCollection services)
	{
		// Register the security provider consumers depend on. TryAdd, so a host that registers its own
		// IElasticsearchSecurityProvider before calling this keeps it.
		services.TryAddSingleton<IElasticsearchSecurityProvider, DefaultElasticsearchSecurityProvider>();

		return services;
	}

	/// <summary>
	/// Registers the startup validator for <see cref="SecurityMonitoringOptions"/>. Every entry point that
	/// registers the options calls this, so the settings are validated whichever one a host uses.
	/// </summary>
	private static void AddSecurityMonitoringOptionsValidator(this IServiceCollection services) =>
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SecurityMonitoringOptions>, SecurityMonitoringOptionsValidator>());
}
