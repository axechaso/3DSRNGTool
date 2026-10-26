using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Pk3DSRNGTool;
using Pk3DSRNGTool.Core;
using Pk3DSRNGTool.RNG;

internal static class Gen6Tests
{
    private static int assertions, failures;
    private static readonly uint[] State = new TinyMT(0x12345678).status;
    [STAThread]
    private static int Main()
    {
        Check("TinyFinder reference vectors", ReferenceVectors);
        Check("Timeline can be searched repeatedly", RepeatTimeline);
        Check("Empty queue, negative delay and validation", EmptyQueue);
        Check("Frame clipping and parity", FrameRanges);
        Check("Long delay with frequent NPC calls", LongDelay);
        Check("Friend Safari slot probabilities", Safari);
        Check("Hidden form settings survive translation", FormSettings);
        Check("Index boundaries, filters, RTC and cancellation", IndexSearch);
        Check("Main search keeps MT aligned to displayed frames", MainTimelineSearch);
        Check("Index window searches and transfers state", IndexWindow);
        Console.WriteLine("{0} assertions; {1} failed cases.", assertions, failures);
        return failures == 0 ? 0 : 1;
    }
    private static void Check(string name, Action run)
    {
        try { run(); Console.WriteLine("PASS: " + name); }
        catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL: " + name + ": " + ex); }
    }
    private static void Equal<T>(T expected, T actual, string name)
    {
        assertions++;
        if (!Equals(expected, actual)) throw new Exception(name + ": expected " + expected + ", got " + actual);
    }
    private static void Throws<T>(Action run) where T : Exception
    {
        assertions++;
        try { run(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    public sealed class Vector
    {
        public string Name { get; set; }
        public TinyIndexSettings Settings { get; set; }
        public uint[] State { get; set; }
        public Expected Expected { get; set; }
    }
    public sealed class Expected
    {
        public bool Sync { get; set; }
        public bool Trigger { get; set; }
        public bool EggMove { get; set; }
        public bool Shiny { get; set; }
        public int Slot { get; set; }
        public int HA { get; set; }
        public int Potential { get; set; }
        public int Delay { get; set; }
        public int TID { get; set; }
        public int SID { get; set; }
        public int[] Items { get; set; }
        public int[] Flutes { get; set; }
        public string Details { get; set; }
    }
    private static void ReferenceVectors()
    {
        var json = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TinyFinder-vectors.json"));
        var vectors = new JavaScriptSerializer().Deserialize<Vector[]>(json);
        foreach (var v in vectors)
        {
            var r = TinyIndexSearch.Predict(v.State, v.Settings);
            var e = v.Expected;
            var m = v.Settings.Method;
            bool dex = m == TinyIndexMethod.DexNavStep || m == TinyIndexMethod.DexNavSearch;
            bool radarChain = m == TinyIndexMethod.Radar && v.Settings.ChainLength > 0;
            if (m != TinyIndexMethod.ID)
            {
                Equal(e.Sync, r.Synchronized, v.Name + " sync");
                Equal(e.Slot, r.Slot, v.Name + " slot");
                Equal(e.HA, r.HiddenAbility, v.Name + " HA");
                if (!radarChain) Equal(string.Join(",", e.Items), string.Join(",", r.Items), v.Name + " items");
                if (v.Settings.IsORAS) Equal(string.Join(",", e.Flutes), string.Join(",", r.Flutes), v.Name + " flutes");
            }
            if (m == TinyIndexMethod.Wild || m == TinyIndexMethod.FriendSafari || m == TinyIndexMethod.HordeStep ||
                m == TinyIndexMethod.Fishing || m == TinyIndexMethod.RockSmash || m == TinyIndexMethod.DexNavStep)
                Equal(e.Trigger, r.Triggered, v.Name + " trigger");
            if (dex)
            {
                Equal(e.Potential, r.Potential, v.Name + " potential");
                Equal(e.EggMove, r.EggMove, v.Name + " egg move");
                Equal(e.Shiny, r.SpecialShiny, v.Name + " shiny");
            }
            if (m == TinyIndexMethod.Radar)
            {
                Equal(e.Shiny, r.SpecialShiny, v.Name + " shiny patch");
                Equal(e.Details, r.Details, v.Name + " patch board");
            }
            if (r.HordeData != null)
            {
                Equal(e.Sync, r.HordeData.Sync, v.Name + " transferred horde sync");
                Equal(e.Slot, (int)r.HordeData.Slot, v.Name + " transferred horde slot");
                Equal(e.HA, (int)r.HordeData.HA, v.Name + " transferred horde HA");
                Equal(string.Join(",", e.Items), string.Join(",", r.HordeData.HeldItems.Select(i => i < 50 ? 0 : i < 55 ? 1 : 2)), v.Name + " transferred horde items");
            }
            Equal(e.Delay, r.Delay, v.Name + " delay");
            Equal(e.TID, r.TID, v.Name + " TID"); Equal(e.SID, r.SID, v.Name + " SID");
            Equal(true, v.State.SequenceEqual(r.State), v.Name + " input state");
        }
    }
    private static TinyTimeline Timeline(byte method = 0, int delay = 14)
    {
        var line = new TinyTimeline(State) { Startingframe = 500, Maxframe = 1700, Delay = delay, Method = method, P1 = 3, P2 = 13 };
        line.Add(500, 0); line.Add(510, 3);
        return line;
    }
    private static string Describe(TinyTimeline line) => string.Join(";", line.results.Select(r => r.FrameRange + ":" + r.Status + ":" + r.HitIndex + ":" + r.Sync));
    private static void RepeatTimeline()
    {
        var line = Timeline(); line.Generate(true);
        string first = Describe(line); line.Generate(true);
        Equal(first, Describe(line), "repeated search");
        line.Maxframe += 200; line.Generate(true);
        Equal(true, line.FramesBetween(502, 1700).Any(), "extended timeline");
        Equal("-", new Frame_Tiny().HA, "non-horde HA display");
    }
    private static void EmptyQueue()
    {
        var status = new TinyStatus(State) { Currentframe = 500 };
        status.time_elapse(20);
        Equal(520, status.Currentframe, "no event time elapse");
        Equal(true, State.SequenceEqual(status.Status), "empty queue does not advance RNG");
        Throws<ArgumentOutOfRangeException>(() => status.time_elapse(-1));
        Throws<InvalidOperationException>(() => new TinyTimeline(State) { Maxframe = 1000 }.Generate());
        Throws<ArgumentException>(() => Timeline(6, 10).Generate(true));
        status.Add(520, 2); status.time_elapse(1);
        Equal(true, status.ScheduledCalls.Any(c => c.Value == 1), "short blink calibration event");
    }
    private static void FrameRanges()
    {
        foreach (bool split in new[] { false, true })
        {
            var line = Timeline(); line.Generate(split, ForMainForm: true);
            int[] frames = line.FramesBetween(503, 611).Select(f => f.Frame).ToArray();
            Equal(string.Join(",", Enumerable.Range(0, 54).Select(i => 504 + 2 * i)), string.Join(",", frames), "odd lower bound");
            Equal(frames.Length, frames.Distinct().Count(), "no duplicate frames");
            Equal(0, line.FramesBetween(600, 599).Count(), "reversed range");
        }
    }
    private static void LongDelay()
    {
        var line = new TinyTimeline(State) { Startingframe = 500, Maxframe = 1200, Delay = 6000, Method = 0 };
        line.Add(500, 6); line.Generate(true);
        var frames = line.FramesBetween(502, 1200).ToArray();
        Equal(350, frames.Length, "no dropped tail frames");
        foreach (var frame in frames)
        {
            var rng = new TinyMT(State);
            int calls = (frame.Frame + 6000 - 500 + 15) / 16;
            for (int i = 0; i < calls; i++) rng.Next();
            Equal(rng.Nextuint() < 0x80000000, frame.Span.sync, "long-delay synchronization at " + frame.Frame);
        }
    }
    private static void Safari()
    {
        foreach (int slots in new[] { 2, 3 })
        {
            var line = Timeline(3, 6); line.P1 = slots; line.Generate(true);
            foreach (var row in line.results)
            {
                int frame = row.framemin + 2;
                var status = row.tinystate.Clone(); status.Currentframe = frame; status.RemoveType(3); status.time_elapse(6);
                status.Next(); status.Next(); int rand = status.Rand(100);
                int expected = slots == 2 ? rand < 50 ? 1 : 2 : rand < 34 ? 1 : rand < 67 ? 2 : 3;
                Equal(expected, (int)row.Slot, "safari probability distribution");
            }
        }
    }
    private static void FormSettings()
    {
        using (var tool = new TinyTimelineTool())
        {
            tool.Translate(); tool.Method.SelectedIndex = 3;
            tool.Parameter1.Value = 2; tool.Parameter2.Value = 77;
            var type = (ComboBox)tool.Controls.Find("Type1", true)[0]; type.SelectedValue = 2;
            var frame = (NumericUpDown)tool.Controls.Find("Frame1", true)[0]; frame.Value = 700;
            tool.Translate();
            Equal(3, tool.Method.SelectedIndex, "translated method");
            Equal(2m, tool.Parameter1.Value, "translated slot count");
            Equal(77m, tool.Parameter2.Value, "translated encounter rate");
            Equal(2, (int)type.SelectedValue, "short blink retained");
            Equal(700m, frame.Value, "event frame retained");
            Equal(77, tool.gettimeline().P2, "hidden tool encounter rate");
            tool.Method.SelectedIndex = 1;
            Equal(6m, tool.Parameter1.Value, "default party size");
            tool.Delay.Value = 14;
            Control<CheckBox>(tool, "ConsiderDelay").Checked = false;
            Equal(0, tool.gettimeline().Delay, "preview may omit delay");
            Equal(14, tool.gettimeline(ForMainForm: true).Delay, "main timeline always includes encounter delay");
            tool.ImportIndexState(State); Equal(true, tool.NeedsCalibration, "import invalidates timeline calibration");
        }
    }
    private static void IndexSearch()
    {
        var settings = new TinyIndexSettings { Method = TinyIndexMethod.ID };
        var rows = TinyIndexSearch.Search(State, 3, 5, settings, null);
        Equal("3,4,5", string.Join(",", rows.Select(r => r.Index)), "inclusive bounds");
        var rng = new TinyMT(State); for (int i = 0; i < 3; i++) rng.Next();
        Equal(true, rng.status.SequenceEqual(rows[0].State), "index advance count");
        var filtered = TinyIndexSearch.Search(State, 0, 1000, settings, new TinyIndexFilter { TID = rows[0].TID });
        Equal(true, filtered.Any(r => r.Index == 3), "TID filter");
        Equal(true, filtered.All(r => r.TID == rows[0].TID), "TID filter excludes other IDs");
        Equal(2, TinyIndexSearch.Search(State, 0, 20, settings, null, 2).Count, "result limit");
        var canceled = new CancellationTokenSource(); canceled.Cancel();
        Throws<OperationCanceledException>(() => TinyIndexSearch.Search(State, 100, 200, settings, null, cancel: canceled.Token));
        Throws<ArgumentException>(() => TinyIndexSearch.Search(new uint[4], 0, 10, settings, null));
        Throws<ArgumentException>(() => TinyIndexSearch.Search(State, 10, 0, settings, null));
        Equal(0U, TinyIndexSearch.SeedFromRTC(new DateTime(2000, 1, 1)), "RTC epoch");
        Equal(1000U, TinyIndexSearch.SeedFromRTC(new DateTime(2000, 1, 1, 0, 0, 1)), "RTC milliseconds");
        Equal(TinyIndexSearch.SeedFromRTC(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            TinyIndexSearch.SeedFromRTC(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local)), "RTC independent of host timezone");
    }

    private static T Field<T>(object source, string name) => (T)source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
    private static void Invoke(object source, string name) => source.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { null, EventArgs.Empty });
    private static T Control<T>(Form form, string name) where T : System.Windows.Forms.Control => (T)form.Controls.Find(name, true)[0];
    private static void MainTimelineSearch()
    {
        var program = typeof(MainForm).Assembly.GetType("Pk3DSRNGTool.Program");
        using (var main = new MainForm())
        {
            program.GetField("mainform").SetValue(null, main);
            Invoke(main, "MainForm_Load");
            main.Ver = 0;
            Control<TabControl>(main, "RNGMethod").SelectedIndex = 0;
            Control<ComboBox>(main, "CB_Category").SelectedIndex = 0;
            Invoke(main, "Poke_SelectedIndexChanged");
            Control<CheckBox>(main, "IgnoreFilters").Checked = true;
            Control<CheckBox>(main, "AlwaysSynced").Checked = false;
            Control<RadioButton>(main, "AroundTarget").Checked = false;
            Control<NumericUpDown>(main, "Frame_min").Value = 503;
            Control<NumericUpDown>(main, "Frame_max").Value = 563;
            Control<NumericUpDown>(main, "TimeSpan").Value = 1;
            Control<NumericUpDown>(main, "Timedelay").Value = 14;
            Control<CheckBox>(main, "ConsiderDelay").Checked = true;
            var tool = Field<TinyTimelineTool>(main, "TTT");
            tool.Gen6Tiny = State; tool.Method.SelectedIndex = 0; tool.Delay.Value = 14;
            Control<CheckBox>(tool, "ConsiderDelay").Checked = true;
            Control<NumericUpDown>(tool, "Frame1").Value = 500;
            Control<ComboBox>(tool, "Type1").SelectedValue = 0;
            main.GetType().GetMethod("Search6_Timeline", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(main, null);
            var timeline = Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().ToArray();
            Equal(30, timeline.Length, "timeline clips requested range");
            main.GetType().GetMethod("Search6_Normal", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(main, null);
            var normal = Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().ToDictionary(
                f => (int)f.GetType().GetProperty("FrameNum").GetValue(f, null), f => (uint)f.GetType().GetProperty("EC").GetValue(f, null));
            foreach (var frame in timeline)
            {
                int n = (int)frame.GetType().GetProperty("FrameNum").GetValue(frame, null);
                Equal(normal[n], (uint)frame.GetType().GetProperty("EC").GetValue(frame, null), "main MT EC at frame " + n);
            }
            Control<TabControl>(main, "RNGMethod").SelectedIndex = 2;
            Control<ComboBox>(main, "CB_Category").SelectedIndex = 1;
            Invoke(main, "Poke_SelectedIndexChanged");
            var horde = TinyIndexSearch.Predict(State, new TinyIndexSettings { Method = TinyIndexMethod.HordeHoney, HoneyDelay = 500 });
            main.ApplyTinyIndexState(horde);
            main.GetType().GetMethod("Search6_Horde", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(main, null);
            var pool = typeof(MainForm).Assembly.GetType("Pk3DSRNGTool.Core.RNGPool");
            Equal(true, ReferenceEquals(horde.HordeData, pool.GetField("horde").GetValue(null)), "main uses exact indexed horde data");
        }
        program.GetField("mainform").SetValue(null, null);
    }
    private static void IndexWindow()
    {
        using (var main = new MainForm())
        using (var tool = new TinyIndexTool(main))
        {
            Field<ComboBox>(tool, "source").SelectedIndex = 1;
            Field<Pk3DSRNGTool.Controls.HexMaskedTextBox>(tool, "seed").Value = 0x12345678;
            Field<NumericUpDown>(tool, "max").Value = 20;
            Field<ComboBox>(tool, "method").SelectedIndex = (int)TinyIndexMethod.ID;
            Invoke(tool, "Search");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!Field<Button>(tool, "search").Enabled && timer.ElapsedMilliseconds < 10000)
            { Application.DoEvents(); Thread.Sleep(1); }
            var rows = (List<TinyIndexResult>)Field<DataGridView>(tool, "grid").DataSource;
            Equal(21, rows.Count, "window search inclusive indices");
            main.ApplyTinyIndexState(rows[7]);
            Equal(true, rows[7].State.SequenceEqual(main.TinySeeds), "window transfer preserves word order");
            Equal(rows[7].State[3], Control<Pk3DSRNGTool.Controls.HexMaskedTextBox>(main, "ID_Tiny3").Value, "ID transfer updates ID search input");
            Equal(true, Field<TinyTimelineTool>(main, "TTT").NeedsCalibration, "window transfer requires new event frames");
            Equal(true, tool.PreferredSize.Width > 0, "window lays out controls");
        }
    }
}
