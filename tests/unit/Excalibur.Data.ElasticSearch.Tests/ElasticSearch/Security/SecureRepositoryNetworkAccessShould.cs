// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security;

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security;

/// <summary>
/// An IP whitelist is a deny-by-default control. A host that configures one and whose request context
/// cannot supply a source address must be denied, not waved through: otherwise the whitelist is a knob a
/// consumer can set that provably cannot deny anything.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SecureRepositoryNetworkAccessShould
{
	private sealed class Document
	{
		public string Id { get; init; } = string.Empty;
	}

	private sealed class Repository(
		IOptions<ElasticsearchSecurityOptions> options,
		string? sourceIpAddress)
		: SecureElasticRepositoryBase<Document>(
			new ElasticsearchClient(),
			A.Fake<IElasticsearchFieldEncryptor>(),
			A.Fake<IElasticsearchSecurityAuditor>(),
			A.Fake<IElasticsearchSecurityMonitor>(),
			options,
			NullLogger<SecureElasticRepositoryBase<Document>>.Instance)
	{
		public override Task InitializeIndexAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		protected override string? GetCurrentUserId() => "user-1";

		protected override string? GetCurrentSourceIpAddress() => sourceIpAddress;

		protected override bool IsIpInRange(string ipAddress, string rangeOrIp) =>
			string.Equals(ipAddress, rangeOrIp, StringComparison.Ordinal);
	}

	private static Repository BuildRepository(string? sourceIpAddress, params string[] whitelist) =>
		new(
			Options.Create(new ElasticsearchSecurityOptions
			{
				Enabled = true,
				NetworkSecurity = new NetworkSecurityOptions { Enabled = true, IpWhitelist = [.. whitelist] },
			}),
			sourceIpAddress);

	[Fact]
	public async Task DenyTheOperation_WhenAWhitelistIsConfiguredAndTheSourceAddressIsUnavailable()
	{
		var repository = BuildRepository(sourceIpAddress: null, "10.0.0.1");

		var ex = await Should.ThrowAsync<SecurityException>(
			() => repository.SecureIndexAsync(new Document(), TestContext.Current.CancellationToken));

		ex.Message.ShouldContain("whitelist");
	}

	/// <summary>
	/// The liveness arm: with no whitelist configured there is nothing to satisfy, so an unknown source
	/// address must not become a denial. Without this the safety arm above could be passed by refusing
	/// every operation.
	/// </summary>
	[Fact]
	public async Task NotDenyTheOperation_WhenNoWhitelistIsConfiguredAndTheSourceAddressIsUnavailable()
	{
		var repository = BuildRepository(sourceIpAddress: null);

		// Getting past security validation is the assertion. What happens afterwards depends on whether an
		// Elasticsearch instance answers, which this test deliberately does not depend on -- only that the
		// failure, if any, is not a denial.
		Exception? caught = null;
		try
		{
			_ = await repository.SecureIndexAsync(new Document(), TestContext.Current.CancellationToken);
		}
		catch (Exception ex)
		{
			caught = ex;
		}

		caught.ShouldNotBeOfType<SecurityException>();
	}
}
