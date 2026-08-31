// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.A3.Authorization;
using Excalibur.Data.Firestore.Authorization;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Binds the tenant term a grant document carries: stored verbatim in the field, carried into the
/// document id under the id encoding, with no reserved value substituted for any input, and with the id
/// and the field naming the same tenant.
/// </summary>
/// <remarks>
/// The id and the field disagreeing is the specific defect this binds: the two were built by different
/// rules, so a tenant could be filed under one value and stored under another.
/// The id is a constant "grant" term joined to the four escaped terms. The escaping is what makes the
/// join injective -- "_" is legal inside a tenant slug, so joining raw aliased distinct grants onto one
/// document -- and the constant leading term keeps an untenanted id off Firestore's reserved
/// <c>__.*__</c> shape. Each case therefore pins the exact encoded id rather than deriving it, so a
/// change to the encoding fails here instead of passing vacuously.
/// FirestoreGrantDocument is internal, so it is reached by reflection as its sibling tests do.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "Authorization")]
public sealed class GrantTenantTermShould
{
	private static readonly DateTimeOffset GrantedOn = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private readonly Type _documentType = typeof(FirestoreAuthorizationOptions).Assembly
		.GetType("Excalibur.Data.Firestore.Authorization.FirestoreGrantDocument")!;

	private static Grant GrantFor(string tenantId) =>
		new("user-1", null, tenantId, "role", "admin", null, "system", GrantedOn);

	private string CreateDocumentId(string tenantId) =>
		(string)_documentType.GetMethod("CreateDocumentId", BindingFlags.Public | BindingFlags.Static)!
			.Invoke(null, [tenantId, "user-1", "role", "admin"])!;

	private Dictionary<string, object> ToDocumentData(Grant grant) =>
		(Dictionary<string, object>)_documentType
			.GetMethod("ToDocumentData", BindingFlags.Public | BindingFlags.Static)!
			.Invoke(null, [grant])!;

	[Theory]
	[InlineData("tenant-a", "tenant-a")]
	// "__null__" was the reserved sentinel; must now be an ordinary tenant.
	[InlineData("__null__", "%5F%5Fnull%5F%5F")]
	// "__untenanted__" is the framework's reserved value; not special to this store either.
	[InlineData("__untenanted__", "%5F%5Funtenanted%5F%5F")]
	[InlineData("null", "null")]
	public void CarryTheTenantIntoTheDocumentId(string tenantId, string encodedTenantId) =>
		CreateDocumentId(tenantId).ShouldBe($"grant_{encodedTenantId}_user-1_role_admin");

	[Theory]
	[InlineData("tenant-a")]
	[InlineData("__null__")]
	[InlineData("__untenanted__")]
	[InlineData("null")]
	public void StoreTheTenantVerbatimInTheField(string tenantId) =>
		ToDocumentData(GrantFor(tenantId))["tenant_id"].ShouldBe(tenantId);

	[Theory]
	[InlineData("tenant-a", "tenant-a")]
	[InlineData("__null__", "%5F%5Fnull%5F%5F")]
	[InlineData("", "")]
	public void FileTheDocumentUnderTheSameTenantItStores(string tenantId, string encodedTenantId)
	{
		// The id and the field are built by separate code paths. They must name the same tenant for every
		// input, including the empty string, which the two rules used to treat differently.
		var storedField = (string)ToDocumentData(GrantFor(tenantId))["tenant_id"];

		// The field is the tenant itself; the id carries that same tenant under the id encoding. Both
		// halves are asserted, so neither can drift into a substituted value on its own.
		storedField.ShouldBe(tenantId);

		// Exact, not a prefix: a prefix check passes vacuously when the stored field is empty and the id
		// begins with a substituted value that happens to start with the separator.
		CreateDocumentId(tenantId).ShouldBe($"grant_{encodedTenantId}_user-1_role_admin");
	}

	[Fact]
	public void NotCollideATenantNamedLikeTheFormerSentinelWithAnyOtherTenant()
	{
		// Liveness: each names its own tenant in the stored field.
		ToDocumentData(GrantFor("__null__"))["tenant_id"].ShouldBe("__null__");
		ToDocumentData(GrantFor("__untenanted__"))["tenant_id"].ShouldBe("__untenanted__");
		ToDocumentData(GrantFor("tenant-a"))["tenant_id"].ShouldBe("tenant-a");

		// Safety: three distinct documents, none mistakable for another.
		new[] { CreateDocumentId("__null__"), CreateDocumentId("__untenanted__"), CreateDocumentId("tenant-a") }
			.Distinct().Count().ShouldBe(3);
	}
}
