// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.ContextValues;

/// <summary>
/// Unit tests for <see cref="TenantId"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ContextValues")]
[Trait("Priority", "0")]
public sealed class TenantIdShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithValue_SetsValue()
	{
		// Act
		var tenantId = new TenantId("tenant-123");

		// Assert
		tenantId.Value.ShouldBe("tenant-123");
	}

	// REJECT, DO NOT COERCE. These three previously asserted that a missing tenant becomes the empty
	// string. That is the fail-open shape: an identifier that no longer names the tenant the caller
	// intended, produced with no diagnostic at the point the mistake was made, and carried downstream
	// where an empty tenant term matches either nothing or everything depending on the store. The
	// assertions are inverted rather than deleted -- the input cases still deserve coverage, and what
	// changed is the required outcome.

	[Fact]
	public void Constructor_WithNullValue_Throws()
	{
		_ = Should.Throw<ArgumentNullException>(() => new TenantId(null!));
	}

	[Fact]
	public void Constructor_WithEmptyString_Throws()
	{
		_ = Should.Throw<ArgumentException>(() => new TenantId(string.Empty));
	}

	[Fact]
	public void Constructor_WithWhitespace_Throws()
	{
		// Whitespace is the case a null-or-empty guard alone would let through, and it reaches storage as
		// a tenant term that silently matches nothing.
		_ = Should.Throw<ArgumentException>(() => new TenantId("   "));
	}

	#endregion

	#region MaxLength Tests

	// SAFETY: an over-length identifier is rejected at construction, where the caller still has context,
	// rather than reaching a store that could truncate or reject it far from the call that caused it.
	// LIVENESS (the paired arm): a legal identifier — including one at exactly the boundary — still
	// constructs and round-trips through Value unchanged. A guard that rejected everything would pass the
	// safety arm alone; asserting the boundary length actually succeeds is what proves it does not.

	[Fact]
	public void Constructor_WithValueAtMaxLength_Succeeds()
	{
		var value = new string('t', TenantId.MaxLength);

		var tenantId = new TenantId(value);

		tenantId.Value.ShouldBe(value);
		tenantId.Value.Length.ShouldBe(TenantId.MaxLength);
	}

	[Fact]
	public void Constructor_WithValueOneOverMaxLength_Throws()
	{
		// RED pre-fix: no shipped provider is guaranteed to store this whole (the narrowest shipped tenant
		// column is exactly TenantId.MaxLength characters), so accepting it here would let the framework
		// hand a store an identifier it may silently truncate.
		var value = new string('t', TenantId.MaxLength + 1);

		var ex = Should.Throw<ArgumentException>(() => new TenantId(value));
		ex.ParamName.ShouldBe("value");
	}

	[Fact]
	public void FromString_WithValueOneOverMaxLength_Throws()
	{
		var value = new string('t', TenantId.MaxLength + 1);

		_ = Should.Throw<ArgumentException>(() => TenantId.FromString(value));
	}

	#endregion

	#region Value Property Tests

	[Fact]
	public void Value_IsImmutableAfterConstruction()
	{
		// The predecessor of this test asserted Value COULD be reassigned. A mutable tenant identifier can
		// be changed after every scope check that read it, so the value authorising an operation need not
		// be the value the operation runs under. Immutability is now structural -- there is no setter to
		// call, so the old test does not compile -- and this asserts the property that replaced it.
		var tenantId = new TenantId("original");

		tenantId.Value.ShouldBe("original");

		var sameReference = tenantId;
		sameReference.Value.ShouldBe(
			"original",
			"a second reference must observe the same value; the identifier cannot be mutated through it");
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsValue()
	{
		// Arrange
		var tenantId = new TenantId("my-tenant");

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldBe("my-tenant");
	}

	[Fact]
	public void ToString_NeverReturnsEmpty_BecauseAnEmptyIdentifierCannotBeConstructed()
	{
		// This previously built an empty identifier and asserted ToString returned "". No such instance can
		// exist now, so the meaningful assertion is the invariant that replaced it: whatever a TenantId
		// renders as, it is never the empty string a downstream tenant predicate would treat as unscoped.
		var tenantId = new TenantId("t");

		tenantId.ToString().ShouldNotBeNullOrWhiteSpace();
		tenantId.ToString().ShouldBe("t");
	}

	#endregion

	#region FromString Tests

	[Fact]
	public void FromString_CreatesNewInstance()
	{
		// Act
		var tenantId = TenantId.FromString("from-string-value");

		// Assert
		tenantId.Value.ShouldBe("from-string-value");
	}

	[Fact]
	public void FromString_WithNull_Throws()
	{
		// The named factory must reject exactly as the constructor does. A factory that coerced while the
		// constructor rejected would be a second, quieter way to obtain the fail-open value the constructor
		// exists to prevent.
		_ = Should.Throw<ArgumentNullException>(() => TenantId.FromString(null!));
	}

	#endregion

	#region Explicit Construction Tests

	// THE IMPLICIT CONVERSION IS GONE, AND ITS ABSENCE IS THE POINT. `TenantId x = someString;` used to
	// compile, so any string in scope could become a tenant identifier silently -- including a null, which
	// became the empty tenant. Removal is enforced by the compiler, so no runtime test can assert it; what
	// these arms do instead is pin the surviving path and prove it still rejects, which is the property the
	// conversion was smuggling around.

	[Fact]
	public void ExplicitConstruction_FromString_CreatesInstance()
	{
		var tenantId = new TenantId("explicit-construction");

		tenantId.Value.ShouldBe("explicit-construction");
	}

	[Fact]
	public void ExplicitConstruction_FromNull_Throws()
	{
		string? candidate = null;

		_ = Should.Throw<ArgumentNullException>(() => new TenantId(candidate!));
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameValue_ReturnsTrue()
	{
		// Arrange
		var tenantId1 = new TenantId("same-value");
		var tenantId2 = new TenantId("same-value");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentCase_ReturnsFalse()
	{
		// INVERTED, security-relevant. Case-insensitive equality makes "Acme" and "acme" ONE tenant in
		// .NET while storage decides separately by collation — a mismatch that resolves tenant identity
		// differently on either side of a database call, and fails open. Comparison is Ordinal.
		var tenantId1 = new TenantId("TENANT");
		var tenantId2 = new TenantId("tenant");

		tenantId1.Equals(tenantId2).ShouldBeFalse(
			"Ordinal comparison must treat a case difference as a different tenant; conflating them is a "
			+ "cross-tenant identity collision");
	}

	[Fact]
	public void Equals_WithDifferentValue_ReturnsFalse()
	{
		// Arrange
		var tenantId1 = new TenantId("value-a");
		var tenantId2 = new TenantId("value-b");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var tenantId = new TenantId("test");

		// Act & Assert
		tenantId.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithSameReference_ReturnsTrue()
	{
		// Arrange
		var tenantId = new TenantId("test");

		// Act & Assert
		tenantId.Equals(tenantId).ShouldBeTrue();
	}

	[Fact]
	public void ObjectEquals_WithSameValue_ReturnsTrue()
	{
		// Arrange
		var tenantId1 = new TenantId("same");
		object tenantId2 = new TenantId("same");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeTrue();
	}

	[Fact]
	public void ObjectEquals_WithNonTenantId_ReturnsFalse()
	{
		// Arrange
		var tenantId = new TenantId("test");

		// Act - Cast to object to avoid implicit conversion from string to TenantId
		var result = tenantId.Equals((object)123);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_WithSameValue_ReturnsSameHash()
	{
		// Arrange
		var tenantId1 = new TenantId("same-value");
		var tenantId2 = new TenantId("same-value");

		// Act & Assert
		tenantId1.GetHashCode().ShouldBe(tenantId2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_WithDifferentCase_ReturnsDifferentHash()
	{
		// Follows the equality change and must not be forgotten alongside it: a case-insensitive hash beside
		// a case-sensitive Equals would collide two distinct tenants in every dictionary and set they key —
		// which is where tenant-scoped lookups actually happen.
		var tenantId1 = new TenantId("TENANT");
		var tenantId2 = new TenantId("tenant");

		tenantId1.GetHashCode().ShouldNotBe(tenantId2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_WithDifferentValue_ReturnsDifferentHash()
	{
		// Arrange
		var tenantId1 = new TenantId("value-a");
		var tenantId2 = new TenantId("value-b");

		// Act & Assert
		tenantId1.GetHashCode().ShouldNotBe(tenantId2.GetHashCode());
	}

	#endregion
}
