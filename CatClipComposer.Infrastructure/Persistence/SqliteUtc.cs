using System.Globalization;

namespace CatClipComposer.Infrastructure.Persistence;

internal static class SqliteUtc
{
    public static string Format(DateTime value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    public static DateTime Parse(string value) =>
        DateTime.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
}
