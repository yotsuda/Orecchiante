using System.Text;

namespace Dettato;

/// <summary>A chart as the two files beside a recording: the Lily# chord chart (.lys) and what it was read from
/// (.dettato.json, docs/SIDECAR.md). As tools/read_chords.py writes them, to the letter.</summary>
static class Writer
{
    /// <summary>The chord on each beat of a bar: what the chart writes of it.</summary>
    static string[] BeatNames(Bar bar, int beats)
    {
        double length = (bar.End - bar.Start) / beats;
        var names = new string[beats];
        for (int i = 0; i < beats; i++)
        {
            double at = bar.Start + (i + 0.01) * length;
            names[i] = (bar.Chords.FirstOrDefault(c => c.Start <= at && at < c.End) ?? bar.Chords[^1]).Name;
        }
        return names;
    }

    public static string Lys(Chart chart, string title)
    {
        string[] key = chart.Key.Split(' ');
        string tonic = key[0], mode = key[1];
        string accidental = tonic[1..] switch { "#" => "is", "b" => "es", _ => "" };
        int beats = chart.Time == "6/8" ? 2 : int.Parse(chart.Time.Split('/')[0]);
        double doubt = chart.Doubt ?? 0;

        // A bar as Lily# writes it: a chord on the beat it starts on, '.' where it goes on. `said`: the bar, each
        // time it is played. A chord is marked when, over those times, the reader is less sure of it than the
        // doubt; the chord offered is the least sure time's.
        string Slots(List<Bar> said)
        {
            int brief = said[0].Pickup;
            var names = BeatNames(said[0], brief != 0 ? brief : beats);
            // Lily# fits a bar's slots to the meter's beats, a short bar's too: three beats of 4/4 cannot be
            // written a chord a beat. The chord that leads into the first bar stands for them.
            if (brief != 0 && names.Distinct().Count() > 1 && beats % brief != 0) names = [names[^1]];
            var marks = names.Select((name, i) => i == 0 || name != names[i - 1] ? name : ".").ToArray();
            var doubts = new Dictionary<string, string>();
            foreach (string name in names.Distinct())
            {
                var heard = said.SelectMany(bar => bar.Chords).Where(c => c.Name == name && c.Sure != null).ToList();
                if (heard.Count == 0) continue;
                double sum = 0;
                foreach (var c in heard) sum += c.Sure!.Value;
                if (sum / heard.Count < doubt) doubts[name] = heard.MinBy(c => c.Sure!.Value)!.OtherName!;
            }
            marks = [.. marks.Select(m => doubts.TryGetValue(m, out string? other) ? $"{m}@todo(\"or {other}?\")" : m)];
            if (marks.Skip(1).All(m => m == ".")) return marks[0];
            int half = beats / 2;
            if (brief == 0 && beats % 2 == 0 && marks.Where((m, i) => i != 0 && i != half).All(m => m == "."))
                return $"{marks[0]} {marks[half]}";
            return string.Join(" ", marks);
        }

        // The bars of a chords row, four to a line, with their repeat signs (Repeats).
        IEnumerable<string> Rows(List<Cell> cells, string indent)
        {
            var said = new List<string>();
            for (int k = 0; k < cells.Count; k++)
            {
                var cell = cells[k];
                string text = (cell.Open && k == 0 ? "|: " : "") + (cell.Ending != 0 ? $"[{cell.Ending}. " : "");
                string sign = cell.Close ?? "|";
                if (k + 1 < cells.Count && cells[k + 1].Open) sign = sign == ":|" ? ":|:" : "|:";
                said.Add(text + Slots(cell.Said) + " " + sign + (cell.Bracket ? " ]" : ""));
            }
            for (int i = 0; i < said.Count; i += 4)
                yield return indent + string.Join(" ", said.Skip(i).Take(4));
        }

        // `bars`: each a list of the bar each time it is played. As the cells Rows writes, the repeats inside them
        // (Repeats) written once, every playing of a bar in its one cell.
        List<Cell> CellsOf(List<List<Bar>> bars, bool signs = true)
        {
            var cells = new List<Cell>();
            var keys = bars.Select(said => string.Join("\u0001", BeatNames(said[0], said[0].Pickup != 0 ? said[0].Pickup : beats))).ToList();
            int i = 0;
            foreach (var item in (signs ? Repeats(keys) : []).Append(new Repeat(false, bars.Count, 0, 0)))
            {
                for (int j = i; j < item.At; j++) cells.Add(new Cell(bars[j]));
                if (item.At == bars.Count) break;
                if (!item.Volta)
                {
                    for (int m = 0; m < item.Length; m++)
                        cells.Add(new Cell([.. Enumerable.Range(0, item.Times).SelectMany(t => bars[item.At + t * item.Length + m])])
                        {
                            Open = m == 0,
                            Close = m == item.Length - 1 ? (item.Times == 2 ? ":|" : $":|*{item.Times}") : null,
                        });
                    i = item.At + item.Times * item.Length;
                }
                else
                {
                    int body = item.Length, ending = item.Times;
                    for (int m = 0; m < body; m++)
                        cells.Add(new Cell([.. bars[item.At + m], .. bars[item.At + body + ending + m]]) { Open = m == 0 });
                    for (int m = 0; m < ending; m++)
                        cells.Add(new Cell(bars[item.At + body + m]) { Ending = m == 0 ? 1 : 0, Close = m == ending - 1 ? ":|" : null });
                    for (int m = 0; m < ending; m++)
                        cells.Add(new Cell(bars[item.At + 2 * body + ending + m]) { Ending = m == 0 ? 2 : 0, Bracket = m == ending - 1 });
                    i = item.At + 2 * (body + ending);
                }
            }
            return cells;
        }

        var lines = new List<string>
        {
            $"// read from {chart.Audio} by dettato",
            $"title \"{title}\"",
            $"tempo {chart.TempoNote} = {chart.Bpm}",
            $"time {chart.Time}",
            $"key {char.ToLowerInvariant(tonic[0])}{accidental} {mode}",
            "",
        };
        int pickup = chart.Bars[0].Pickup;
        if (chart.Form.Distinct().Count() == chart.Form.Count && pickup == 0)   // nothing is said twice: the chart is the bars in their order
        {
            lines.Add("chords harmony {");
            lines.AddRange(Rows(CellsOf([.. chart.Bars.Select(bar => new List<Bar> { bar })]), "  "));
            lines.AddRange(["}", "", "score { chords harmony }", ""]);
            return string.Join("\n", lines);
        }
        var names = new List<string>();
        var sections = new Dictionary<string, List<List<Bar>>>();   // a section's bars, each a list of the bar each time the section is played
        foreach (var bar in chart.Bars)
        {
            if (!sections.TryGetValue(bar.Section!, out var of))
            {
                sections[bar.Section!] = of = [];
                names.Add(bar.Section!);
            }
            if (bar.BarOfSection > of.Count) of.Add([]);
            of[bar.BarOfSection - 1].Add(bar);
        }
        // A short section the form always plays a few times running repeats in its own chords; the form says it once.
        var runs = new Dictionary<string, HashSet<int>>();
        foreach (var (name, times) in RunsOf(chart.Form))
        {
            if (!runs.TryGetValue(name, out var set)) runs[name] = set = [];
            set.Add(times);
        }
        var again = runs.Where(r => r.Key != "Pickup" && r.Value.Count == 1 && r.Value.Min() > 1 && sections[r.Key].Count <= RepeatSection)
            .ToDictionary(r => r.Key, r => r.Value.First());
        var form = RunsOf(chart.Form).SelectMany(r => Enumerable.Repeat(r.Name, again.ContainsKey(r.Name) ? 1 : r.Times)).ToList();
        // Sections said again in the form repeat there; the bars of any other repeat inside its chords.
        // (Repeats do not nest: a section the form repeats has no signs of its own.)
        var (saidForm, repeated) = FormWithRepeats(form, [.. again.Keys, "Pickup"]);
        foreach (string name in names)
        {
            List<Cell> cells;
            if (again.TryGetValue(name, out int times))
            {
                cells = CellsOf(sections[name], signs: false);
                cells[0].Open = true;
                cells[^1].Close = times == 2 ? ":|" : $":|*{times}";
            }
            else cells = CellsOf(sections[name], signs: name != "Pickup" && !repeated.Contains(name));
            lines.Add($"section {name} {{");
            if (name == "Pickup") lines.Add($"  partial {(chart.Time == "6/8" ? "4." : Reader.Partial[pickup])}");
            lines.Add("  chords harmony {");
            lines.AddRange(Rows(cells, "    "));
            lines.AddRange(["  }", "}", ""]);
        }
        lines.AddRange(["form { " + saidForm + " }", "", "score { chords harmony }", ""]);
        return string.Join("\n", lines);
    }

    /// <summary>A section of at most this many bars that the form always plays twice or more running repeats inside its
    /// own chords; a longer one, or one also played once, repeats in the form.</summary>
    const int RepeatSection = 4;

    /// <summary>A bar of a chords row as written: every playing of it, and the signs at it. Open: a repeat begins at
    /// it; Ending: it begins that ending; Close: the sign after it (null: a bar line); Bracket: an ending ends after it.</summary>
    sealed class Cell(List<Bar> said)
    {
        public List<Bar> Said = said;
        public bool Open, Bracket;
        public int Ending;
        public string? Close;
    }

    /// <summary>A repeat inside a row: the bars from At said Times times running (a run of Length bars), or, Volta,
    /// Length bars (the body), Times bars, the body again and other Times bars.</summary>
    readonly record struct Repeat(bool Volta, int At, int Length, int Times);

    /// <summary>[(name, how many times running)] along a list.</summary>
    static List<(string Name, int Times)> RunsOf(List<string> names)
    {
        var runs = new List<(string Name, int Times)>();
        foreach (string name in names)
            if (runs.Count > 0 && runs[^1].Name == name) runs[^1] = (name, runs[^1].Times + 1);
            else runs.Add((name, 1));
        return runs;
    }

    /// <summary>The repeats to write inside a run of bars, from the chord on each beat of each (one key a bar), left
    /// to right and apart, as tools/read_chords.py's repeats_in: what repeats is two bars or more and not one chord
    /// held; the one that saves the most bars is taken first, a run before a volta that saves as many; a repeat does
    /// not begin right where one with endings or played more than twice ends.</summary>
    static List<Repeat> Repeats(List<string> keys)
    {
        int n = keys.Count, i = 0;
        var found = new List<Repeat>();
        bool after = false;
        bool Same(int a, int b, int length) { for (int k = 0; k < length; k++) if (keys[a + k] != keys[b + k]) return false; return true; }
        while (i < n)
        {
            (int Saved, Repeat Item, int Takes)? best = null;
            for (int length = 2; !after && length <= (n - i) / 2; length++)
            {
                if (keys.Skip(i).Take(length).Distinct().Count() == 1) continue;
                int times = 1;
                while (i + (times + 1) * length <= n && Same(i, i + times * length, length)) times++;
                if (times > 1 && (best is null || (times - 1) * length > best.Value.Saved))
                    best = ((times - 1) * length, new Repeat(false, i, length, times), times * length);
            }
            for (int body = 2; !after && body <= (n - i) / 2; body++)
            {
                if (keys.Skip(i).Take(body).Distinct().Count() == 1) continue;
                for (int ending = 1; ending <= 2; ending++)
                {
                    int end = i + 2 * (body + ending);
                    if (end <= n && Same(i, i + body + ending, body) && !Same(i + body, end - ending, ending)
                        && (best is null || body > best.Value.Saved))
                        best = (body, new Repeat(true, i, body, ending), 2 * (body + ending));
                }
            }
            if (best is null)
            {
                after = false;
                i++;
                continue;
            }
            found.Add(best.Value.Item);
            i += best.Value.Takes;
            after = best.Value.Item.Volta || best.Value.Item.Times > 2;
        }
        return found;
    }

    /// <summary>The form with repeat signs where sections come again running, as tools/read_chords.py's
    /// form_with_repeats: a run of sections said again (|: A B :|, |: A :|*3), or a section followed by a different
    /// section each time (|: A [1. B] :| [2. C]). Sections in `unsigned` are written as they come. Returns the form as
    /// written, and the sections inside its repeats.</summary>
    static (string Form, HashSet<string> Inside) FormWithRepeats(List<string> form, HashSet<string> unsigned)
    {
        int n = form.Count, i = 0;
        var said = new List<string>();
        var inside = new HashSet<string>();
        while (i < n)
        {
            (int Saved, string Words, int Takes)? best = null;
            for (int length = 1; length <= (n - i) / 2; length++)
            {
                var unit = form.GetRange(i, length);
                if (unit.Any(unsigned.Contains)) break;
                int times = 1;
                while (i + (times + 1) * length <= n && form.GetRange(i + times * length, length).SequenceEqual(unit)) times++;
                if (times > 1 && (best is null || (times - 1) * length > best.Value.Saved))
                    best = ((times - 1) * length, "|: " + string.Join(" ", unit) + (times == 2 ? " :|" : $" :|*{times}"), times * length);
            }
            if (!unsigned.Contains(form[i]))
            {
                int j = i;
                var endings = new List<string>();
                while (j + 1 < n && form[j] == form[i] && form[j + 1] != form[i] && !unsigned.Contains(form[j + 1]) && !endings.Contains(form[j + 1]))
                {
                    endings.Add(form[j + 1]);
                    j += 2;
                }
                if (endings.Count > 1 && (best is null || endings.Count - 1 > best.Value.Saved))
                {
                    string words = string.Join(" ", endings.Select((e, k) => $"[{k + 1}. {e}]" + (k < endings.Count - 1 ? " :|" : "")));
                    best = (endings.Count - 1, $"|: {form[i]} {words}", j - i);
                }
            }
            if (best is null)
            {
                said.Add(form[i]);
                i++;
            }
            else
            {
                said.Add(best.Value.Words);
                inside.UnionWith(form.GetRange(i, best.Value.Takes));
                i += best.Value.Takes;
            }
        }
        return (string.Join(" ", said), inside);
    }

    /// <summary>As Python's json.dumps(result, indent=2) writes read_chords.py's result, and a line's end.</summary>
    public static string Json(Chart chart)
    {
        var s = new StringBuilder();
        s.Append("{\n  \"dettato\": 1,\n");
        s.Append($"  \"audio\": {Quoted(chart.Audio)},\n");
        s.Append($"  \"duration\": {Np.Repr(chart.Duration)},\n");
        s.Append($"  \"tempo\": {{\n    \"note\": {Quoted(chart.TempoNote)},\n    \"bpm\": {chart.Bpm}\n  }},\n");
        s.Append($"  \"time\": {Quoted(chart.Time)},\n");
        s.Append($"  \"key\": {Quoted(chart.Key)},\n");
        if (chart.Tuning is int tuning) s.Append($"  \"tuning\": {tuning},\n");
        if (chart.Doubt is double doubt) s.Append($"  \"doubt\": {Np.Repr(doubt)},\n");
        s.Append(chart.Form.Count == 0 ? "  \"form\": [],\n"
            : "  \"form\": [\n" + string.Join(",\n", chart.Form.Select(name => "    " + Quoted(name))) + "\n  ],\n");
        s.Append("  \"bars\": [\n");
        s.Append(string.Join(",\n", chart.Bars.Select(bar =>
        {
            var b = new StringBuilder("    {\n");
            b.Append($"      \"bar\": {bar.Number},\n");
            if (bar.Pickup != 0) b.Append($"      \"pickup\": {bar.Pickup},\n");
            b.Append($"      \"start\": {Np.Repr(bar.Start)},\n      \"end\": {Np.Repr(bar.End)},\n");
            b.Append("      \"beats\": [\n" + string.Join(",\n", bar.Beats.Select(t => "        " + Np.Repr(t))) + "\n      ],\n");
            b.Append("      \"chords\": [\n");
            b.Append(string.Join(",\n", bar.Chords.Select(c =>
            {
                string chord = $"        {{\n          \"start\": {Np.Repr(c.Start)},\n          \"end\": {Np.Repr(c.End)},\n"
                    + $"          \"symbol\": {Quoted(c.Name)},\n          \"fit\": {Np.Repr(c.Fit)}";
                if (c.Sure is double sure) chord += $",\n          \"sure\": {Np.Repr(sure)}";
                if (c.OtherName is string other) chord += $",\n          \"other\": {Quoted(other)}";
                return chord + "\n        }";
            })));
            b.Append("\n      ]");
            if (bar.Section is string section) b.Append($",\n      \"section\": {Quoted(section)},\n      \"barOfSection\": {bar.BarOfSection}");
            return b.Append("\n    }").ToString();
        })));
        s.Append("\n  ]\n}\n");
        return s.ToString();
    }

    static string Quoted(string text)
    {
        var s = new StringBuilder("\"");
        foreach (char c in text)
            s.Append(c switch
            {
                '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", '\b' => "\\b", '\f' => "\\f",
                < ' ' or > '\x7f' =>$"\\u{(int)c:x4}",
                _ => c.ToString(),
            });
        return s.Append('"').ToString();
    }
}
