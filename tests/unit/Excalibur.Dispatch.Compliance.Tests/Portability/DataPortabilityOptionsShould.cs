namespace Excalibur.Dispatch.Compliance.Tests.Portability;

public class DataPortabilityOptionsShould
{
    [Fact]
    public void Have_exports_directory_by_default()
    {
        var options = new DataPortabilityOptions();

        options.ExportDirectory.ShouldBe("exports");
    }

    [Fact]
    public void Have_100mb_max_export_size_by_default()
    {
        var options = new DataPortabilityOptions();

        options.MaxExportSize.ShouldBe(100 * 1024 * 1024);
    }

    [Fact]
    public void Have_7_day_retention_period_by_default()
    {
        var options = new DataPortabilityOptions();

        options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
    }
}
