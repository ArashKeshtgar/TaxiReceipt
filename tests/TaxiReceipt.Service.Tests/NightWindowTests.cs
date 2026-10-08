using TaxiReceipt.Service;

namespace TaxiReceipt.Service.Tests;

public class NightWindowTests
{
    private static readonly TimeOnly Nine = new(21, 0), Six = new(6, 0);

    [Theory]
    [InlineData("21:00", true)]
    [InlineData("23:59", true)]
    [InlineData("00:00", true)]  // the original compared Hour with 24 and never got here
    [InlineData("05:59", true)]
    [InlineData("06:00", false)]
    [InlineData("12:00", false)]
    [InlineData("20:59", false)]
    public void Window_crosses_midnight(string time, bool active) =>
        Assert.Equal(active, NightWindow.IsActive(TimeOnly.Parse(time), Nine, Six));

    [Fact]
    public void Window_within_one_day_and_all_day()
    {
        Assert.True(NightWindow.IsActive(new(18, 30), new(18, 0), new(23, 0)));
        Assert.False(NightWindow.IsActive(new(23, 0), new(18, 0), new(23, 0)));
        Assert.True(NightWindow.IsActive(new(3, 0), new(0, 0), new(0, 0)));
    }

    [Fact]
    public void A_punch_after_midnight_belongs_to_the_previous_evening()
    {
        Assert.Equal(new DateOnly(2026, 10, 7), NightWindow.NightOf(new DateTime(2026, 10, 8, 2, 0, 0), Nine, Six));
        Assert.Equal(new DateOnly(2026, 10, 8), NightWindow.NightOf(new DateTime(2026, 10, 8, 22, 0, 0), Nine, Six));
    }

    [Fact]
    public void Table_name_is_the_persian_year_and_month()
    {
        // 2026-10-08 = 1405/07/16; 2015-09-17 = 1394/06/26 (the original's backup date).
        Assert.Equal("C140507", NightWindow.TableName("C{0}", new DateTime(2026, 10, 8)));
        Assert.Equal("C139406", NightWindow.TableName("C{0}", new DateTime(2015, 9, 17)));
        // 1 Mehr 1405 = 2026-09-23: the first day of month 7.
        Assert.Equal("C140506", NightWindow.TableName("C{0}", new DateTime(2026, 9, 22)));
        Assert.Equal("C140507", NightWindow.TableName("C{0}", new DateTime(2026, 9, 23)));
    }
}
