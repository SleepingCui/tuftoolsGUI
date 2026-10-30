using System.Text.Json;
using TuftoolsGui.Services;

namespace TuftoolsGui.Models;

public sealed class KeyValueRow {
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class ScoreRow {
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Global { get; set; } = "";
    public string Country { get; set; } = "";
}

public sealed class PlayerRow {
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public string Ranked { get; set; } = "";
    public string Accuracy { get; set; } = "";
    public string Discord { get; set; } = "";

    public static PlayerRow From(JsonElement p) {
        return new PlayerRow {
            Id = p.Get("id").Str(),
            Name = p.Get("name").Str(),
            Country = p.Get("country").Str(),
            Ranked = Fmt.Num(p.Get("rankedScore").Num(), 2),
            Accuracy = Fmt.Pct(p.Get("averageXacc").Num()),
            Discord = p.Get("discord", "username").Str(p.Get("username").Str())
        };
    }
}

public sealed class LevelRow {
    public string Id { get; set; } = "";
    public string Song { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string Tiles { get; set; } = "";
    public string Clears { get; set; } = "";
    public string Downloads { get; set; } = "";

    public static LevelRow From(JsonElement l) {
        return new LevelRow {
            Id = l.Get("id").Str(),
            Song = l.Get("song").Str(),
            Artist = l.Get("artist").Str(),
            Difficulty = l.Get("difficulty", "name").Str(),
            Tiles = l.Get("tilecount").Str(),
            Clears = l.Get("clears").Str(),
            Downloads = l.Get("downloadCount").Str()
        };
    }
}

public sealed class PassRow {
    public string Date { get; set; } = "";
    public string LevelId { get; set; } = "";
    public string Song { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string Score { get; set; } = "";
    public string Accuracy { get; set; } = "";
    public string Note { get; set; } = "";

    public static PassRow From(JsonElement r) {
        var notes = new List<string>();
        if (r.Get("isWorldsFirst").Bool()) notes.Add("首通");
        if (r.Get("isWorldsFirstPP").Bool()) notes.Add("首杀");
        if (r.Get("is16K").Bool()) notes.Add("16K");
        else if (r.Get("is12K").Bool()) notes.Add("12K");
        if (r.Get("isNoHoldTap").Bool()) notes.Add("无长条");
        if (r.Get("isXPerfectMode").Bool()) notes.Add("XPerfect");
        if (r.Get("speed").Num() is double sp && Math.Abs(sp - 1) > 1e-9) notes.Add("速度 " + Fmt.Num(sp, 2));
        return new PassRow {
            Date = Fmt.Date(r.Get("vidUploadTime").Str()),
            LevelId = r.Get("levelId").Str(),
            Song = r.Get("level", "song").Str(),
            Difficulty = r.Get("level", "difficulty", "name").Str(),
            Score = r.Get("scoreV2").Str(),
            Accuracy = Fmt.Pct(r.Get("accuracy").Num()),
            Note = string.Join(" ", notes)
        };
    }
}

public sealed class SolutionRow {
    public string Index { get; set; } = "";
    public string FailMiss { get; set; } = "";
    public string TooEarly { get; set; } = "";
    public string Early { get; set; } = "";
    public string EPerfect { get; set; } = "";
    public string Perfect { get; set; } = "";
    public string LPerfect { get; set; } = "";
    public string Late { get; set; } = "";

    public static SolutionRow From(int index, JsonElement c) {
        return new SolutionRow {
            Index = index.ToString(),
            FailMiss = c.Get("failMiss").Str("0"),
            TooEarly = c.Get("tooEarly").Str("0"),
            Early = c.Get("early").Str("0"),
            EPerfect = c.Get("ePerfect").Str("0"),
            Perfect = c.Get("perfect").Str("0"),
            LPerfect = c.Get("lPerfect").Str("0"),
            Late = c.Get("late").Str("0")
        };
    }
}

public sealed class PresetItem {
    public string Display { get; set; } = "";
    public string? Key { get; set; }
    public string? Custom { get; set; }
}
