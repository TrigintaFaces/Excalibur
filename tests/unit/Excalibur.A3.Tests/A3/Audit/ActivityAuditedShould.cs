#pragma warning disable IL2026 // Members annotated with RequiresUnreferencedCodeAttribute
#pragma warning disable IL3050 // Members annotated with RequiresDynamicCodeAttribute

using Excalibur.A3.Audit.Events;
using Excalibur.A3.Events;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Tests.A3.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ActivityAuditedShould
{
	[Fact]
	public void Initialize_from_IActivityAudited()
	{
		// Arrange
		var source = A.Fake<IActivityAudited>();
		A.CallTo(() => source.ActivityName).Returns("TestActivity");
		A.CallTo(() => source.ApplicationName).Returns("TestApp");
		A.CallTo(() => source.ClientAddress).Returns("10.0.0.1");
		A.CallTo(() => source.CorrelationId).Returns(Guid.NewGuid());
		A.CallTo(() => source.Exception).Returns("Error occurred");
		A.CallTo(() => source.Login).Returns("jdoe");
		A.CallTo(() => source.Request).Returns("{\"id\":1}");
		A.CallTo(() => source.Response).Returns("OK");
		A.CallTo(() => source.StatusCode).Returns(200);
		A.CallTo(() => source.TenantId).Returns("tenant-1");
		A.CallTo(() => source.Timestamp).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => source.UserId).Returns("user-123");
		A.CallTo(() => source.UserName).Returns("John Doe");

		// Act
		var audited = new ActivityAudited(source);

		// Assert
		audited.ActivityName.ShouldBe("TestActivity");
		audited.ApplicationName.ShouldBe("TestApp");
		audited.ClientAddress.ShouldBe("10.0.0.1");
		audited.Exception.ShouldBe("Error occurred");
		audited.Login.ShouldBe("jdoe");
		audited.Request.ShouldBe("{\"id\":1}");
		audited.Response.ShouldBe("OK");
		audited.StatusCode.ShouldBe(200);
		audited.TenantId.ShouldBe("tenant-1");
		audited.UserId.ShouldBe("user-123");
		audited.UserName.ShouldBe("John Doe");
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ActivityAudited(null!));
	}

	[Fact]
	public void Inherit_from_DomainEventBase()
	{
		// Arrange
		var source = CreateAuditSource();

		// Act
		var audited = new ActivityAudited(source);

		// Assert
		audited.ShouldBeAssignableTo<DomainEventBase>();
		audited.ShouldBeAssignableTo<IDomainEvent>();
		audited.MessageId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Implement_IActivityAudited()
	{
		// Arrange
		var source = CreateAuditSource();

		// Act
		var audited = new ActivityAudited(source);

		// Assert
		audited.ShouldBeAssignableTo<IActivityAudited>();
	}

	[Fact]
	public void Accept_null_optional_fields()
	{
		// Arrange
		var source = A.Fake<IActivityAudited>();
		A.CallTo(() => source.ActivityName).Returns("Test");
		A.CallTo(() => source.ApplicationName).Returns("App");
		A.CallTo(() => source.Request).Returns("{}");
		A.CallTo(() => source.UserId).Returns("user");
		A.CallTo(() => source.UserName).Returns("name");
		A.CallTo(() => source.ClientAddress).Returns(null);
		A.CallTo(() => source.Exception).Returns(null);
		A.CallTo(() => source.Login).Returns(null);
		A.CallTo(() => source.Response).Returns(null);
		A.CallTo(() => source.TenantId).Returns(null);

		// Act
		var audited = new ActivityAudited(source);

		// Assert
		audited.ClientAddress.ShouldBeNull();
		audited.Exception.ShouldBeNull();
		audited.Login.ShouldBeNull();
		audited.Response.ShouldBeNull();
		audited.TenantId.ShouldBeNull();
	}

	[Fact]
	public void Allow_setting_status_code()
	{
		// Arrange
		var audited = new ActivityAudited(CreateAuditSource());

		// Act
		audited.StatusCode = 404;

		// Assert
		audited.StatusCode.ShouldBe(404);
	}

	[Fact]
	public void Allow_setting_timestamp()
	{
		// Arrange
		var audited = new ActivityAudited(CreateAuditSource());
		var newTimestamp = DateTimeOffset.UtcNow.AddHours(-1);

		// Act
		audited.Timestamp = newTimestamp;

		// Assert
		audited.Timestamp.ShouldBe(newTimestamp);
	}

	private static IActivityAudited CreateAuditSource()
	{
		var source = A.Fake<IActivityAudited>();
		A.CallTo(() => source.ActivityName).Returns("TestActivity");
		A.CallTo(() => source.ApplicationName).Returns("TestApp");
		A.CallTo(() => source.Request).Returns("{}");
		A.CallTo(() => source.UserId).Returns("user-123");
		A.CallTo(() => source.UserName).Returns("John Doe");
		return source;
	}
}
