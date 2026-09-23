// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Tests.Tenancy;

/// <summary>
/// Binds the shipped <see cref="ITenantContext"/> view over the ambient tenant.
/// </summary>
/// <remarks>
/// <para>
/// The arms fail under different mutations. The scoped arm is RED for a view that ignores the ambient
/// scope and answers one fixed partition. The unscoped arms are RED for the reading a consumer would
/// most plausibly write by hand — a bare <see cref="TenantContextHolder.Current"/> read, which resolves
/// <see langword="null"/> outside a scope.
/// </para>
/// <para>
/// That second mutation is the reason this view is shipped at all. Null does not mean "untenanted", it
/// means "undecided", and a store that requires a tenant refuses it. Since every arm of a store suite
/// that is not about tenancy runs outside a scope, the naive reading fails the whole suite rather than
/// the tenancy arms — so the last arm below binds the difference directly, by driving both readings
/// through the same conversion the stores use.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AmbientTenantContextViewShould
{
	[Fact]
	public async Task ResolveTheEstablishedTenant_AndRestoreOnDispose()
	{
		var context = TenantContextHolder.AmbientContext;

		using (TenantContextHolder.BeginScope("acme"))
		{
			context.TenantId.ShouldBe("acme");
			context.HasTenant.ShouldBeTrue();

			await Task.Yield(); // the ambient tenant is AsyncLocal-backed: it must flow across the await
			context.TenantId.ShouldBe("acme");

			using (TenantContextHolder.BeginScope("contoso"))
			{
				context.TenantId.ShouldBe("contoso");
			}

			context.TenantId.ShouldBe("acme");
		}

		context.TenantId.ShouldBe(TenantScope.UntenantedSentinel);
	}

	[Fact]
	public void ResolveTheUntenantedPartition_WhenNoScopeIsEstablished()
	{
		var context = TenantContextHolder.AmbientContext;

		TenantContextHolder.Current.ShouldBeNull(); // precondition: genuinely outside any scope

		context.TenantId.ShouldNotBeNull();
		context.TenantId.ShouldBe(TenantScope.UntenantedSentinel);
		context.HasTenant.ShouldBeTrue(); // the untenanted partition is a partition
	}

	[Fact]
	public void ResolveTheUntenantedPartition_WhenTheEstablishedTenantIsBlank()
	{
		var context = TenantContextHolder.AmbientContext;

		using (TenantContextHolder.BeginScope("   "))
		{
			// A blank scope establishes nothing, so it must read as the untenanted partition rather
			// than as a tenant named by whitespace, which no store can address.
			context.TenantId.ShouldBe(TenantScope.UntenantedSentinel);
		}
	}

	[Fact]
	public void ConvertToTheUntenantedPartitionOutsideAScope_WhereTheNaiveReadingRefuses()
	{
		// THE ARM THAT MATTERS. Both contexts read the same ambient value; only the fallback differs.
		// Driving both through the conversion every tenant-partitioned store uses shows the consequence:
		// the shipped view yields a partition, the naive one fails closed on a caller that did nothing
		// wrong. If the shipped view were ever written as a bare Current read, these two lines agree
		// and this arm goes RED.
		TenantContextHolder.Current.ShouldBeNull();

		TenantScope.FromContext(TenantContextHolder.AmbientContext).ShouldBe(TenantScope.Untenanted);
		KeyedTenantPartition.FromContext(TenantContextHolder.AmbientContext)
			.ShouldBe(KeyedTenantPartition.Untenanted);

		_ = Should.Throw<TenantRequiredException>(
			() => TenantScope.FromContext(new NaiveAmbientTenantContext()));
	}

	/// <summary>
	/// The reading a consumer writes by hand when the framework does not ship one: the raw ambient
	/// identifier, with no fallback. Present only so the arm above can bind the difference.
	/// </summary>
	private sealed class NaiveAmbientTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}
}
