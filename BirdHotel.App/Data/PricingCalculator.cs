using BirdHotel.App.Models;

namespace BirdHotel.App.Data;

// 籠1つあたりの料金を計算する。
// 30日と31日はどちらも「1か月」、14日と15日はどちらも「2週間」として数える。
public static class PricingCalculator
{
    private record Rates(int Daily, int Week, int TwoWeeks, int Month);

    private static Rates RatesFor(BirdSize size) => size == BirdSize.中大型
        ? new Rates(Daily: 1000, Week: 6400, TwoWeeks: 12800, Month: 25000)
        : new Rates(Daily: 800, Week: 5000, TwoWeeks: 10000, Month: 18000);

    public static (int Total, string Detail) Calculate(int days, BirdSize size)
    {
        if (days <= 0) return (0, "0");

        var rates = RatesFor(size);
        var parts = new List<string>();
        var total = 0;
        var remaining = days;

        // 1か月ぶん（30日または31日）。同じ料金なので、31日として数えられる分は先に充てる。
        var months = remaining / 30;
        if (months > 0)
        {
            var extraDays = Math.Min(remaining - months * 30, months);
            remaining -= months * 30 + extraDays;
            for (var i = 0; i < months; i++)
            {
                var unitDays = 30 + (i < extraDays ? 1 : 0);
                parts.Add($"{unitDays}({rates.Month})");
                total += rates.Month;
            }
        }

        // 2週間ぶん（14日または15日）
        var twoWeeks = remaining / 14;
        if (twoWeeks > 0)
        {
            var extraDays = Math.Min(remaining - twoWeeks * 14, twoWeeks);
            remaining -= twoWeeks * 14 + extraDays;
            for (var i = 0; i < twoWeeks; i++)
            {
                var unitDays = 14 + (i < extraDays ? 1 : 0);
                parts.Add($"{unitDays}({rates.TwoWeeks})");
                total += rates.TwoWeeks;
            }
        }

        // 1週間ぶん（7日）
        var weeks = remaining / 7;
        if (weeks > 0)
        {
            remaining -= weeks * 7;
            for (var i = 0; i < weeks; i++)
            {
                parts.Add($"7({rates.Week})");
                total += rates.Week;
            }
        }

        // 残りは1日単位
        if (remaining > 0)
        {
            parts.Add($"{remaining}({rates.Daily}x{remaining})");
            total += rates.Daily * remaining;
        }

        return (total, string.Join("+", parts));
    }
}
