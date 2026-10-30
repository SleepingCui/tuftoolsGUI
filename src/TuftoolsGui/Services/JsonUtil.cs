using System.Globalization;
using System.Text.Json;
using TuftoolsGui.Models;

namespace TuftoolsGui.Services;

public static class Fmt {
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static string Num(double? v) {
        if (v is not double d) return "";
        if (double.IsNaN(d) || double.IsInfinity(d)) return d.ToString(Inv);
        if (Math.Abs(d - Math.Round(d)) < 1e-9 && Math.Abs(d) < 1e15) return Math.Round(d).ToString("0", Inv);
        return d.ToString("0.####", Inv);
    }
    public static string Num(double? v, int decimals) {
        if (v is not double d) return "";
        if (double.IsNaN(d) || double.IsInfinity(d)) return d.ToString(Inv);
        return d.ToString("0." + new string('#', decimals), Inv);
    }
    public static string Pct(double? frac) => frac is double f ? (f * 100).ToString("0.###", Inv) + "%" : "";
    public static string Date(string? iso) {
        if (string.IsNullOrWhiteSpace(iso)) return "";
        if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d.ToLocalTime().ToString("yyyy-MM-dd HH:mm", Inv);
        return iso;
    }
    public static string Bytes(double? b) {
        if (b is not double v) return "";
        string[] units = { "B", "KB", "MB", "GB" };
        var i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return v.ToString("0.##", Inv) + " " + units[i];
    }
    public static string Duration(double? ms) {
        if (ms is not double v) return "";
        var t = TimeSpan.FromMilliseconds(v);
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }
    public static string RankText(string? rank) => string.IsNullOrWhiteSpace(rank) ? "-" : "#" + rank;
}

public static class JsonUtil {
    public static JsonElement? Get(this JsonElement root, params string[] path) {
        JsonElement cur = root;
        foreach (var p in path) {
            if (cur.ValueKind != JsonValueKind.Object) return null;
            if (!cur.TryGetProperty(p, out var next)) return null;
            cur = next;
        }
        if (cur.ValueKind == JsonValueKind.Null) return null;
        return cur;
    }
    public static JsonElement? Get(this JsonElement? root, params string[] path) {
        return root is JsonElement e ? e.Get(path) : null;
    }
    public static JsonElement? Item(this JsonElement? arr, int index) {
        if (arr is not JsonElement e || e.ValueKind != JsonValueKind.Array) return null;
        if (index < 0 || index >= e.GetArrayLength()) return null;
        return e[index];
    }
    public static int Count(this JsonElement? arr) => arr is JsonElement e && e.ValueKind == JsonValueKind.Array ? e.GetArrayLength() : 0;
    public static IEnumerable<JsonElement> Items(this JsonElement? arr) {
        if (arr is not JsonElement e || e.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in e.EnumerateArray()) yield return item;
    }
    public static string Str(this JsonElement? el) {
        if (el is not JsonElement e) return "";
        return e.ValueKind switch {
            JsonValueKind.String => e.GetString() ?? "",
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            _ => e.GetRawText()
        };
    }
    public static string Str(this JsonElement? el, string fallback) {
        var s = el.Str();
        return string.IsNullOrEmpty(s) ? fallback : s;
    }
    public static double? Num(this JsonElement? el) {
        if (el is not JsonElement e) return null;
        if (e.ValueKind == JsonValueKind.Number) return e.GetDouble();
        if (e.ValueKind == JsonValueKind.String) {
            var s = e.GetString();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
        }
        return null;
    }
    public static int? Int(this JsonElement? el) {
        var d = el.Num();
        if (d is not double v) return null;
        return (int)Math.Round(v);
    }
    public static bool Bool(this JsonElement? el) {
        if (el is not JsonElement e) return false;
        return e.ValueKind == JsonValueKind.True;
    }
    public static List<string> Strings(this JsonElement? el) {
        var list = new List<string>();
        if (el is JsonElement e && e.ValueKind == JsonValueKind.Array) {
            foreach (var item in e.EnumerateArray()) list.Add(item.Str());
        }
        return list;
    }
    public static string JoinStrings(this JsonElement? el, string sep) => string.Join(sep, el.Strings());
    public static bool IsObject(this JsonElement? el) => el is JsonElement e && e.ValueKind == JsonValueKind.Object;
    public static bool IsArray(this JsonElement? el) => el is JsonElement e && e.ValueKind == JsonValueKind.Array;
    public static bool IsObject(this JsonElement el) => el.ValueKind == JsonValueKind.Object;
    public static bool IsArray(this JsonElement el) => el.ValueKind == JsonValueKind.Array;
    public static IEnumerable<JsonElement> Items(this JsonElement el) => ((JsonElement?)el).Items();
    public static string Str(this JsonElement el) => ((JsonElement?)el).Str();
    public static string Str(this JsonElement el, string fallback) => ((JsonElement?)el).Str(fallback);
    public static double? Num(this JsonElement el) => ((JsonElement?)el).Num();
    public static int? Int(this JsonElement el) => ((JsonElement?)el).Int();
    public static bool Bool(this JsonElement el) => ((JsonElement?)el).Bool();
    public static List<string> Strings(this JsonElement el) => ((JsonElement?)el).Strings();
    public static string JoinStrings(this JsonElement el, string sep) => ((JsonElement?)el).JoinStrings(sep);
    public static List<KeyValueRow> Flatten(this JsonElement el, string prefix = "", int maxItems = 40) => ((JsonElement?)el).Flatten(prefix, maxItems);
    public static List<KeyValueRow> Flatten(this JsonElement? el, string prefix = "", int maxItems = 40) {
        var rows = new List<KeyValueRow>();
        FlattenInto(rows, el, prefix, 0, maxItems);
        return rows;
    }
    static void FlattenInto(List<KeyValueRow> rows, JsonElement? el, string prefix, int depth, int maxItems) {
        if (el is not JsonElement e) return;
        switch (e.ValueKind) {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject()) {
                    var path = string.IsNullOrEmpty(prefix) ? p.Name : prefix + "." + p.Name;
                    FlattenInto(rows, p.Value, path, depth + 1, maxItems);
                }
                break;
            case JsonValueKind.Array: {
                    var len = e.GetArrayLength();
                    var allScalar = e.EnumerateArray().All(x => x.ValueKind != JsonValueKind.Object && x.ValueKind != JsonValueKind.Array);
                    if (allScalar) {
                        rows.Add(new KeyValueRow { Key = prefix, Value = string.Join(", ", e.EnumerateArray().Select(x => x.Str())) });
                        break;
                    }
                    var shown = Math.Min(len, maxItems);
                    for (int i = 0; i < shown; i++) FlattenInto(rows, e[i], prefix + "[" + i + "]", depth + 1, maxItems);
                    if (len > shown) rows.Add(new KeyValueRow { Key = prefix, Value = "共 " + len + " 项，已省略其余 " + (len - shown) + " 项" });
                    break;
                }
            default:
                rows.Add(new KeyValueRow { Key = prefix, Value = FormatScalar(e) });
                break;
        }
    }
    public static string FormatScalar(JsonElement e) {
        if (e.ValueKind == JsonValueKind.Number) {
            if (e.TryGetInt64(out var l)) return l.ToString(CultureInfo.InvariantCulture);
            return e.GetDouble().ToString("0.####", CultureInfo.InvariantCulture);
        }
        if (e.ValueKind == JsonValueKind.Null) return "";
        return e.Str();
    }
}
