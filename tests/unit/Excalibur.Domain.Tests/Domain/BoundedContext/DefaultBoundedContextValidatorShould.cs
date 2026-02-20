using System.Reflection;

using Excalibur.Domain.BoundedContext;

using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Domain.BoundedContext;

// Test types outside the test class to avoid CA1034
[BoundedContext("Orders")]
internal sealed class TestOrderService
{
	public TestOrderService(TestInventoryItem inventoryItem, TestOrderItem orderItem)
	{
		InventoryItem = inventoryItem;
		OrderItem = orderItem;
	}

	public TestInventoryItem InventoryItem { get; }
	public TestOrderItem OrderItem { get; }
}

[BoundedContext("Inventory")]
internal sealed class TestInventoryItem
{
	public string Name { get; set; } = string.Empty;
}

[BoundedContext("Orders")]
internal sealed class TestOrderItem
{
	public string ProductName { get; set; } = string.Empty;
}

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class DefaultBoundedContextValidatorShould
{
	[Fact]
	public async Task ReturnEmptyViolations_WhenNoBoundedContextTypesExist()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new BoundedContextOptions());
		var validator = new DefaultBoundedContextValidator(options, Array.Empty<Assembly>());

		// Act
		var violations = await validator.ValidateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		violations.ShouldBeEmpty();
	}

	[Fact]
	public async Task DetectCrossBoundaryViolation()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new BoundedContextOptions());
		var assemblies = new[] { typeof(TestOrderService).Assembly };
		var validator = new DefaultBoundedContextValidator(options, assemblies);

		// Act
		var violations = await validator.ValidateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		violations.ShouldNotBeEmpty();
		violations.ShouldContain(v => v.SourceContext == "Orders" && v.TargetContext == "Inventory");
	}

	[Fact]
	public async Task AllowExplicitlyPermittedCrossBoundaryPatterns()
	{
		// Arrange
		var opts = new BoundedContextOptions();
		opts.AllowedCrossBoundaryPatterns.Add("Orders->Inventory");
		var options = Microsoft.Extensions.Options.Options.Create(opts);
		var assemblies = new[] { typeof(TestOrderService).Assembly };
		var validator = new DefaultBoundedContextValidator(options, assemblies);

		// Act
		var violations = await validator.ValidateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		violations.ShouldNotContain(v => v.SourceContext == "Orders" && v.TargetContext == "Inventory");
	}

	[Fact]
	public async Task RespectCancellationToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);
		var options = Microsoft.Extensions.Options.Options.Create(new BoundedContextOptions());
		var assemblies = new[] { typeof(TestOrderService).Assembly };
		var validator = new DefaultBoundedContextValidator(options, assemblies);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => validator.ValidateAsync(cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DefaultBoundedContextValidator(null!, Array.Empty<Assembly>()));
	}

	[Fact]
	public void ThrowOnNullAssemblies()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new BoundedContextOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DefaultBoundedContextValidator(options, null!));
	}

	[Fact]
	public async Task NotReportViolations_ForSameBoundedContext()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new BoundedContextOptions());
		var assemblies = new[] { typeof(TestOrderItem).Assembly };
		var validator = new DefaultBoundedContextValidator(options, assemblies);

		// Act
		var violations = await validator.ValidateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — TestOrderService references TestOrderItem, both in "Orders" context: no violation
		violations.ShouldNotContain(v =>
			v.SourceType == typeof(TestOrderService) && v.TargetType == typeof(TestOrderItem));
	}
}
