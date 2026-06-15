using System.Globalization;

namespace qubic_spotlight.Client.Services;

// Kompakte Zahlenformatierung für die Statistik-Kacheln.
public static class Format
{
    public static string Compact(string raw)
        => double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? Compact(v) : raw;

    public static string Compact(double v)
    {
        var a = Math.Abs(v);
        return a switch
        {
            >= 1e12 => (v / 1e12).ToString("0.##") + "T",
            >= 1e9 => (v / 1e9).ToString("0.##") + "B",
            >= 1e6 => (v / 1e6).ToString("0.##") + "M",
            >= 1e3 => (v / 1e3).ToString("0.##") + "K",
            _ => v.ToString("0.##")
        };
    }

    public static string Money(string raw)
        => double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? "$" + Compact(v) : raw;

    public static string Price(double price)
    {
        if (price <= 0) return "$0";
        // Sehr kleine Preise (z. B. Qubic ~0,0000005) voll dezimal anzeigen,
        // nicht in wissenschaftlicher Notation. Nachkommastellen so wählen,
        // dass ~3 signifikante Stellen sichtbar sind.
        if (price < 1)
        {
            var decimals = Math.Max(2, 2 - (int)Math.Floor(Math.Log10(price)));
            var format = "0." + new string('#', decimals);
            return "$" + price.ToString(format, CultureInfo.InvariantCulture);
        }
        return "$" + price.ToString("0.######", CultureInfo.InvariantCulture);
    }

    public static string Number(long v) => v.ToString("#,0", CultureInfo.InvariantCulture);
}
