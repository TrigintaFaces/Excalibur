using Excalibur.Dispatch.AuditLogging.Retention;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Tests.Retention;

public class AuditRetentionBackgroundServiceShould
{
    private readonly IAuditRetentionService _fakeRetentionService = A.Fake<IAuditRetentionService>();

    [Fact]
    public void Throw_argument_null_for_null_retention_service()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AuditRetentionBackgroundService(
                null!,
                Microsoft.Extensions.Options.Options.Create(new AuditRetentionOptions()),
                NullLogger<AuditRetentionBackgroundService>.Instance));
    }

    [Fact]
    public void Throw_argument_null_for_null_options()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AuditRetentionBackgroundService(
                _fakeRetentionService,
                null!,
                NullLogger<AuditRetentionBackgroundService>.Instance));
    }

    [Fact]
    public void Throw_argument_null_for_null_logger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AuditRetentionBackgroundService(
                _fakeRetentionService,
                Microsoft.Extensions.Options.Options.Create(new AuditRetentionOptions()),
                null!));
    }

    [Fact]
    public void Construct_with_valid_parameters()
    {
        var sut = new AuditRetentionBackgroundService(
            _fakeRetentionService,
            Microsoft.Extensions.Options.Options.Create(new AuditRetentionOptions()),
            NullLogger<AuditRetentionBackgroundService>.Instance);

        sut.ShouldNotBeNull();
        sut.Dispose();
    }
}
