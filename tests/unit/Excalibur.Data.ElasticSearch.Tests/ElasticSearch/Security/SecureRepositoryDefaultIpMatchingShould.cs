// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Security;

/// <summary>
/// The built-in IP matching decides whether a caller is blocked, so it is locked directly rather than
/// through a repository operation.
/// </summary>
/// <remarks>
/// This predicate used to be abstract, which meant every consumer wrote their own CIDR matcher for an
/// access-control decision. These arms cover what a hand-rolled one usually gets wrong: IPv6, a literal
/// entry spelled differently from the address it should match, an address just outside a range, and a
/// malformed entry — which must not match, because a blacklist entry that silently fails to block is
/// worse than one that is absent.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class SecureRepositoryDefaultIpMatchingShould
{
	private sealed class Document
	{
		public string Id { get; init; } = string.Empty;
	}

	/// <summary>Exposes the protected default without overriding it.</summary>
	private sealed class Repository()
		: SecureElasticRepositoryBase<Document>(
			new ElasticsearchClient(),
			A.Fake<IElasticsearchFieldEncryptor>(),
			A.Fake<IElasticsearchSecurityAuditor>(),
			A.Fake<IElasticsearchSecurityMonitor>(),
			Options.Create(new ElasticsearchSecurityOptions()),
			NullLogger<SecureElasticRepositoryBase<Document>>.Instance)
	{
		public override Task InitializeIndexAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		protected override string? GetCurrentUserId() => "user-1";

		protected override string? GetCurrentSourceIpAddress() => null;

		public bool Match(string ipAddress, string rangeOrIp) => IsIpInRange(ipAddress, rangeOrIp);
	}

	[Theory]
	// IPv4 CIDR, inside and outside
	[InlineData("10.0.0.5", "10.0.0.0/24", true)]
	[InlineData("10.0.1.5", "10.0.0.0/24", false)]
	[InlineData("10.0.0.255", "10.0.0.0/24", true)]
	[InlineData("10.0.1.0", "10.0.0.0/24", false)]
	// a /32 matches only itself
	[InlineData("192.168.1.1", "192.168.1.1/32", true)]
	[InlineData("192.168.1.2", "192.168.1.1/32", false)]
	// literal entries
	[InlineData("192.168.1.1", "192.168.1.1", true)]
	[InlineData("192.168.1.2", "192.168.1.1", false)]
	// IPv6, including two spellings of the same address
	[InlineData("2001:db8::1", "2001:db8::/32", true)]
	[InlineData("2001:dba::1", "2001:db8::/32", false)]
	[InlineData("2001:0db8:0000:0000:0000:0000:0000:0001", "2001:db8::1", true)]
	// families do not cross
	[InlineData("10.0.0.5", "2001:db8::/32", false)]
	// malformed input never matches
	[InlineData("10.0.0.5", "not-an-address", false)]
	[InlineData("10.0.0.5", "10.0.0.0/99", false)]
	[InlineData("not-an-address", "10.0.0.0/24", false)]
	public void MatchAnAddressAgainstARangeOrLiteral(string ipAddress, string rangeOrIp, bool expected) =>
		new Repository().Match(ipAddress, rangeOrIp).ShouldBe(expected);
}
