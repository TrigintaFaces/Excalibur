using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MiddlewareAttributesAndResultsShould
{
	// --- ContractVersionAttribute ---

	[Fact]
	public void ContractVersionAttribute_StoreVersion()
	{
		var attr = new ContractVersionAttribute("1.0.0");

		attr.Version.ShouldBe("1.0.0");
	}

	[Fact]
	public void ContractVersionAttribute_ThrowOnNull()
	{
		Should.Throw<ArgumentNullException>(() => new ContractVersionAttribute(null!));
	}

	// --- SchemaIdAttribute ---

	[Fact]
	public void SchemaIdAttribute_StoreSchemaId()
	{
		var attr = new SchemaIdAttribute("orders.v1");

		attr.SchemaId.ShouldBe("orders.v1");
	}

	[Fact]
	public void SchemaIdAttribute_ThrowOnNull()
	{
		Should.Throw<ArgumentNullException>(() => new SchemaIdAttribute(null!));
	}

	// --- ValidationError ---

	[Fact]
	public void ValidationError_StoreProperties()
	{
		var error = new ValidationError("Name", "Name is required");

		error.PropertyName.ShouldBe("Name");
		error.ErrorMessage.ShouldBe("Name is required");
	}

	[Fact]
	public void ValidationError_ThrowOnNullPropertyName()
	{
		Should.Throw<ArgumentNullException>(() => new ValidationError(null!, "msg"));
	}

	[Fact]
	public void ValidationError_ThrowOnNullErrorMessage()
	{
		Should.Throw<ArgumentNullException>(() => new ValidationError("Name", null!));
	}

	// --- MessageValidationResult ---

	[Fact]
	public void MessageValidationResult_Success_IsValid()
	{
		var result = MessageValidationResult.Success();

		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void MessageValidationResult_Failure_HasErrors()
	{
		var result = MessageValidationResult.Failure(
			new ValidationError("Field1", "Required"),
			new ValidationError("Field2", "Too long"));

		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
		result.Errors[0].PropertyName.ShouldBe("Field1");
		result.Errors[1].ErrorMessage.ShouldBe("Too long");
	}

	[Fact]
	public void MessageValidationResult_CreateWithConstructor()
	{
		var errors = new[] { new ValidationError("A", "B") };
		var result = new MessageValidationResult(true, errors);

		result.IsValid.ShouldBeTrue();
		result.Errors.Count.ShouldBe(1);
	}

	[Fact]
	public void MessageValidationResult_NullErrorsDefaultsToEmpty()
	{
		var result = new MessageValidationResult(true, null!);

		result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	// --- VersionCompatibilityResult ---

	[Fact]
	public void VersionCompatibilityResult_Compatible()
	{
		var result = VersionCompatibilityResult.Compatible();

		result.Status.ShouldBe(VersionCompatibilityStatus.Compatible);
		result.Reason.ShouldBeNull();
	}

	[Fact]
	public void VersionCompatibilityResult_Deprecated()
	{
		var result = VersionCompatibilityResult.Deprecated("Use v2 instead");

		result.Status.ShouldBe(VersionCompatibilityStatus.Deprecated);
		result.Reason.ShouldBe("Use v2 instead");
	}

	[Fact]
	public void VersionCompatibilityResult_Incompatible()
	{
		var result = VersionCompatibilityResult.Incompatible("Breaking change in v3");

		result.Status.ShouldBe(VersionCompatibilityStatus.Incompatible);
		result.Reason.ShouldBe("Breaking change in v3");
	}

	[Fact]
	public void VersionCompatibilityResult_Unknown()
	{
		var result = VersionCompatibilityResult.Unknown("No version info");

		result.Status.ShouldBe(VersionCompatibilityStatus.Unknown);
		result.Reason.ShouldBe("No version info");
	}
}
