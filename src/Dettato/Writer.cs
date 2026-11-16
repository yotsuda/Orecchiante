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

        // Four bars a line, and a line of the page each (`break`: after every four bars and where a section ends, so that
        // a section begins a line; the owner, 2026-11-16). The pickup goes on with the first line.
        IEnumerable<string> Rows(List<List<Bar>> bars, string indent, bool breaks = true)
        {
            int i = 0;
            foreach (int size in LineSizes(bars.Count))
            {
                yield return indent + string.Join(" | ", bars.Skip(i).Take(size).Select(Slots)) + (breaks ? " break |" : " |");
                i += size;
            }
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
            lines.AddRange(Rows([.. chart.Bars.Select(bar => new List<Bar> { bar })], "  "));
            lines.AddRange(["}", "", "score {", "  chords harmony", "}", ""]);
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
        foreach (string name in names)
        {
            lines.Add($"section {name} {{");
            if (name == "Pickup") lines.Add($"  partial {(chart.Time == "6/8" ? "4." : Reader.Partial[pickup])}");
            lines.Add("  chords harmony {");
            lines.AddRange(Rows(sections[name], "    ", name != "Pickup"));
            lines.AddRange(["  }", "}", ""]);
        }
        // Repeat signs only in the form, so that the form alone shows how the song goes (the owner, 2026-11-16).
        lines.AddRange(["form { " + FormWithRepeats(chart.Form) + " }", "", "score {", "  chords harmony", "}", ""]);
        return string.Join("\n", lines);
    }

    /// <summary>How many bars each line of a section has, as tools/read_chords.py's line_sizes: four, but a last line of
    /// one bar goes on the line before (five), and four and two are three and three.</summary>
    static List<int> LineSizes(int bars)
    {
        var sizes = Enumerable.Repeat(4, bars / 4).ToList();
        int left = bars % 4;
        if (left == 1 && sizes.Count > 0) sizes[^1] = 5;
        else if (left == 2 && sizes.Count > 0) { sizes[^1] = 3; sizes.Add(3); }
        else if (left > 0) sizes.Add(left);
        return sizes;
    }

    /// <summary>The form with repeat signs where sections come again running, as tools/read_chords.py's
    /// form_with_repeats: a run of sections said again (|: A B :|, |: A :|*3), or a section followed by a different
    /// section each time (|: A [1. B] :| [2. C]). The pickup is written as it comes.</summary>
    static string FormWithRepeats(List<string> form)
    {
        int n = form.Count, i = 0;
        var said = new List<string>();
        while (i < n)
        {
            (int Saved, string Words, int Takes)? best = null;
            for (int length = 1; length <= (n - i) / 2; length++)
            {
                var unit = form.GetRange(i, length);
                if (unit.Contains("Pickup")) break;
                int times = 1;
                while (i + (times + 1) * length <= n && form.GetRange(i + times * length, length).SequenceEqual(unit)) times++;
                if (times > 1 && (best is null || (times - 1) * length > best.Value.Saved))
                    best = ((times - 1) * length, "|: " + string.Join(" ", unit) + (times == 2 ? " :|" : $" :|*{times}"), times * length);
            }
            if (form[i] != "Pickup")
            {
                int j = i;
                var endings = new List<string>();
                while (j + 1 < n && form[j] == form[i] && form[j + 1] != form[i] && !endings.Contains(form[j + 1]))
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
                i += best.Value.Takes;
            }
        }
        return string.Join(" ", said);
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
