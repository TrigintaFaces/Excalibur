using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for serializable DTO types: SerializableAuthorizationResult,
/// SerializableCausationId, SerializableCorrelationId, SerializableTenantId,
/// and SerializableValidationResult.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SerializableTypesShould : UnitTestBase
{
	// SerializableAuthorizationResult

	[Fact]
	public void AuthorizationResult_Authorized_ReturnsIsAuthorizedTrue()
	{
		// Act
		var result = SerializableAuthorizationResult.Authorized();

		// Assert
		result.IsAuthorized.ShouldBeTrue();
		result.FailureMessage.ShouldBeNull();
	}

	[Fact]
	public void AuthorizationResult_Unauthorized_ReturnsIsAuthorizedFalse()
	{
		// Act
		var result = SerializableAuthorizationResult.Unauthorized("Access denied");

		// Assert
		result.IsAuthorized.ShouldBeFalse();
		result.FailureMessage.ShouldBe("Access denied");
	}

	[Fact]
	public void AuthorizationResult_InitProperties_Work()
	{
		// Act
		var result = new SerializableAuthorizationResult
		{
			IsAuthorized = true,
			FailureMessage = "msg",
		};

		// Assert
		result.IsAuthorized.ShouldBeTrue();
		result.FailureMessage.ShouldBe("msg");
	}

	// SerializableCausationId

	[Fact]
	public void CausationId_Create_WithNoArgs_GeneratesNewGuid()
	{
		// Act
		var id = SerializableCausationId.Create();

		// Assert
		id.Value.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void CausationId_Create_WithGuid_UsesProvidedGuid()
	{
		// Arrange
		var guid = Guid.NewGuid();

		// Act
		var id = SerializableCausationId.Create(guid);

		// Assert
		id.Value.ShouldBe(guid);
	}

	[Fact]
	public void CausationId_ToString_ReturnsGuidString()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var id = SerializableCausationId.Create(guid);

		// Act & Assert
		id.ToString().ShouldBe(guid.ToString());
	}

	[Fact]
	public void CausationId_ValueCanBeSet()
	{
		// Arrange
		var id = new SerializableCausationId();
		var guid = Guid.NewGuid();

		// Act
		id.Value = guid;

		// Assert
		id.Value.ShouldBe(guid);
	}

	// SerializableCorrelationId

	[Fact]
	public void CorrelationId_Create_WithNoArgs_GeneratesNewGuid()
	{
		// Act
		var id = SerializableCorrelationId.Create();

		// Assert
		id.Value.ShouldNotBe(Guid.Empty);
		id.CorrelationId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CorrelationId_Create_WithGuid_UsesProvidedGuid()
	{
		// Arrange
		var guid = Guid.NewGuid();

		// Act
		var id = SerializableCorrelationId.Create(guid);

		// Assert
		id.Value.ShouldBe(guid);
		id.CorrelationId.ShouldBe(guid.ToString());
	}

	[Fact]
	public void CorrelationId_ToString_ReturnsGuidString()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var id = SerializableCorrelationId.Create(guid);

		// Act & Assert
		id.ToString().ShouldBe(guid.ToString());
	}

	[Fact]
	public void CorrelationId_ValueCanBeSet()
	{
		// Arrange
		var id = new SerializableCorrelationId();
		var guid = Guid.NewGuid();

		// Act
		id.Value = guid;

		// Assert
		id.Value.ShouldBe(guid);
	}

	// SerializableTenantId

	[Fact]
	public void TenantId_Create_WithNoArgs_ReturnsEmpty()
	{
		// Act
		var id = SerializableTenantId.Create();

		// Assert
		id.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void TenantId_Create_WithValue_UsesProvidedValue()
	{
		// Act
		var id = SerializableTenantId.Create("tenant-abc");

		// Assert
		id.Value.ShouldBe("tenant-abc");
	}

	[Fact]
	public void TenantId_Create_WithNull_ReturnsEmpty()
	{
		// Act
		var id = SerializableTenantId.Create(null);

		// Assert
		id.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void TenantId_SetValueNull_SetsEmpty()
	{
		// Arrange
		var id = new SerializableTenantId { Value = "test" };

		// Act
		id.Value = null!;

		// Assert
		id.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void TenantId_ToString_ReturnsValue()
	{
		// Arrange
		var id = SerializableTenantId.Create("my-tenant");

		// Act & Assert
		id.ToString().ShouldBe("my-tenant");
	}

	// SerializableValidationResult

	[Fact]
	public void ValidationResult_Success_IsValid()
	{
		// Act
		var result = SerializableValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidationResult_Failed_IsNotValid()
	{
		// Act
		var result = SerializableValidationResult.Failed("error1", "error2");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
	}

	[Fact]
	public void ValidationResult_SetErrorsNull_ReturnsEmpty()
	{
		// Arrange
		var result = new SerializableValidationResult();

		// Act
		result.Errors = null!;

		// Assert
		result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void ValidationResult_InterfaceFailed_ReturnsIValidationResult()
	{
		// Act
		IValidationResult result = SerializableValidationResult.Failed("err");

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ValidationResult_InterfaceSuccess_ReturnsIValidationResult()
	{
		// Act
		IValidationResult result = SerializableValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
	}
}
