// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.A3.Governance.NonHumanIdentity;
using Excalibur.A3.Governance.Reporting;

namespace Excalibur.A3.Governance.Tests.Reporting;

/// <summary>
/// Unit tests for <see cref="JsonReportFormatter"/>: content type, JSON output,
/// round-trip deserialization, empty snapshots, and null guard.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class JsonReportFormatterShould : UnitTestBase
{
	private readonly JsonReportFormatter _sut = new();

	private static EntitlementSnapshot MakeSnapshot(params EntitlementEntry[] entries) =>
		new(DateTimeOffset.UtcNow, EntitlementReportType.UserEntitlements, "user-1", entries);

	private static EntitlementEntry MakeEntry(string userId = "user-1", string scope = "Admin") =>
		new(userId, PrincipalType.Human, scope, DateTimeOffset.UtcNow, "admin", null, true, null);

	[Fact]
	public void HaveCorrectContentType()
	{
		_sut.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public async Task ProduceValidJson()
	{
		var snapshot = MakeSnapshot(MakeEntry());
		var bytes = await _sut.FormatAsync(snapshot, CancellationToken.None);

		bytes.ShouldNotBeEmpty();

		// Verify it parses as valid JSON
		var json = Encoding.UTF8.GetString(bytes);
		var doc = JsonDocument.Parse(json);
		doc.RootElement.GetProperty("reportType").GetInt32().ShouldBe((int)EntitlementReportType.UserEntitlements);
		doc.RootElement.GetProperty("scope").GetString().ShouldBe("user-1");
		doc.RootElement.GetProperty("entries").GetArrayLength().ShouldBe(1);
	}

	[Fact]
	public async Task HandleEmptySnapshot()
	{
		var snapshot = MakeSnapshot();
		var bytes = await _sut.FormatAsync(snapshot, CancellationToken.None);

		var json = Encoding.UTF8.GetString(bytes);
		var doc = JsonDocument.Parse(json);
		doc.RootElement.GetProperty("entries").GetArrayLength().ShouldBe(0);
	}

	[Fact]
	public async Task IncludeReviewStatusInOutput()
	{
		var entry = new EntitlementEntry("user-1", PrincipalType.ServiceAccount, "Admin",
			DateTimeOffset.UtcNow, "admin", null, true,
			new EntitlementReviewStatus(true, DateTimeOffset.UtcNow, ["policy-1"]));
		var snapshot = MakeSnapshot(entry);

		var bytes = await _sut.FormatAsync(snapshot, CancellationToken.None);
		var json = Encoding.UTF8.GetString(bytes);

		json.ShouldContain("reviewStatus");
		json.ShouldContain("hasBeenReviewed");
		json.ShouldContain("policy-1");
	}

	[Fact]
	public async Task ThrowOnFormat_WhenSnapshotIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.FormatAsync(null!, CancellationToken.None));
	}
}
