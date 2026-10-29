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
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Check("TinyFinder reference vectors", ReferenceVectors);
        Check("Timeline can be searched repeatedly", RepeatTimeline);
        Check("Empty queue, negative delay and validation", EmptyQueue);
        Check("Frame clipping and parity", FrameRanges);
        Check("Long delay with frequent NPC calls", LongDelay);
        Check("Friend Safari slot probabilities", Safari);
        Check("Hidden form settings survive translation", FormSettings);
        Check("PokeReader u32 seeds match TinyFinder without extra advances", ReaderSeedInput);
        Check("Timeline menus and connection messages follow the selected language", TimelineLanguage);
        Check("Index boundaries, filters, RTC and cancellation", IndexSearch);
        Check("Main search keeps MT aligned to displayed frames", MainTimelineSearch);
        Check("Native TinyMT stationary panel calculates Berry and Ambush", NativeStationaryTiny);
        Check("Index window searches and transfers state", IndexWindow);
        Check("All host TinyMT state inputs accept PokeReader", AllReaderInputs);
        Check("Original TinyFinder wild window generates and transfers results", OriginalTinyFinderWindow);
        Check("TinyFinder wild data and generators work without a window", WildCoreIntegration);
        Check("Main G6 wild search uses TinyFinder and keeps the existing results table", NativeWildIntegration);
        Check("Inline TinyMT options preserve the original layout and follow encounter type", WildOptionsLayout);
        Check("Turning TinyMT off keeps the XY location selector safe", TinyWildXYToggle);
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
            tool.Gen6Tiny = State;
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
    private static void ReaderSeedInput()
    {
        using (var tool = new TinyTimelineTool())
        {
            tool.Translate();
            var mode = Control<CheckBox>(tool, "UseReaderSeed");
            var seed = Control<TextBox>(tool, "TinySeed");
            Equal(true, mode.Checked, "PokeReader is the default input mode");
            Equal(false, tool.HasSeed, "empty input is not seed zero");
            // Exported from TinyFinder 2133fe7: TinyMT.init(seed, 0), state words 0 through 3.
            var vectors = new[] {
                new[] { "00000000", "78A495AE,60127F96,0D6F15E9,1969DE6C" },
                new[] { "00000001", "0CCA24D8,11BA5AD5,F2DAD045,D95DD7B2" },
                new[] { "12345678", "30814D70,E940D010,E0FA3141,21F8CF22" },
                new[] { "FFFFFFFF", "C9617590,E78A7A46,2ACFF562,092F22A7" },
            };
            foreach (var v in vectors)
            {
                seed.Text = v[0];
                Equal(true, tool.HasSeed, "valid reader seed " + v[0]);
                Equal(v[1], string.Join(",", tool.Gen6Tiny.Select(w => w.ToString("X8"))), "TinyFinder reader state " + v[0]);
            }
            foreach (string invalid in new[] { "", "0x", "100000000", "0x123456789", "G1234567", "12 34" })
            {
                seed.Text = invalid;
                Equal(false, tool.HasSeed, "invalid reader seed " + invalid);
                Equal(true, tool.Gen6Tiny.All(w => w == 0), "invalid seed does not reuse an earlier state");
                Throws<ArgumentException>(() => tool.gettimeline());
            }
            seed.Text = " 0x12345678 ";
            Equal(true, State.SequenceEqual(tool.Gen6Tiny), "hex prefix and surrounding whitespace");
            tool.Method.SelectedIndex = 8;
            var readerLine = tool.gettimeline(); readerLine.Generate(true);
            mode.Checked = false;
            Equal(true, State.SequenceEqual(tool.Gen6Tiny), "switch to four words keeps the resolved state");
            Equal(State[3], Control<Pk3DSRNGTool.Controls.HexMaskedTextBox>(tool, "tiny3").Value, "word 3 display order");
            var rawLine = tool.gettimeline(); rawLine.Generate(true);
            Equal(Describe(readerLine), Describe(rawLine), "reader and four-word timeline results agree");
            mode.Checked = true;
            Invoke(tool, "B_Create_Click");
            Equal(false, tool.NeedsCalibration, "manual event frames work without NTR");
            Equal(true, Field<List<Frame_Tiny>>(tool, "list").Count > 0, "manual input generates rows");
            seed.Text = "1";
            Equal(true, tool.NeedsCalibration, "new seed requires checking event frames");
            Equal(null, Control<DataGridView>(tool, "MainDGV").DataSource, "new seed clears stale results");
            var incoming = new uint[] { 0x01234567, 0x89ABCDEF, 0x13579BDF, 0x2468ACE0 };
            tool.Gen6Tiny = incoming;
            Equal(false, mode.Checked, "NTR/backfill selects four-word mode");
            Equal(true, incoming.SequenceEqual(tool.Gen6Tiny), "backfill is not overwritten by an old reader seed");
            tool.ImportIndexState(State);
            Equal(true, tool.NeedsCalibration, "index import still requires calibration");
        }
    }
    private static void TimelineLanguage()
    {
        var settingsType = typeof(MainForm).Assembly.GetType("Pk3DSRNGTool.Properties.Settings");
        var settings = settingsType.GetProperty("Default").GetValue(null, null);
        var language = settingsType.GetProperty("Language");
        string previous = (string)language.GetValue(settings, null);
        var translate = typeof(Translator).GetMethod("TranslateText", BindingFlags.Static | BindingFlags.NonPublic);
        var message = typeof(FormUtil).GetMethod("TranslateMessage", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            language.SetValue(settings, "en", null);
            using (var tool = new TinyTimelineTool())
            {
                tool.Gen6Tiny = State; tool.Translate(); tool.Method.SelectedIndex = 8;
                var type = Control<ComboBox>(tool, "Type1"); type.SelectedValue = 2;
                tool.TypeNum.Value = 2;
                Control<ComboBox>(tool, "Type2").SelectedValue = 6;
                tool.Parameter2.Value = 77;
                Equal("Blink(short)", type.Text, "English consumption label");
                language.SetValue(settings, "zh", null); tool.Translate();
                Equal("短眨眼", type.Text, "Chinese label refreshed with unchanged type choices");
                Equal("跑动 NPC", Control<ComboBox>(tool, "Type2").Text, "second event is translated");
                Equal("普通野外", tool.Method.Text, "Chinese encounter method");
                Equal("PokeReader 种子", Control<CheckBox>(tool, "UseReaderSeed").Text, "reader input translated");
                Equal(2m, tool.TypeNum.Value, "translation keeps event count");
                Equal(77m, tool.Parameter2.Value, "translation keeps encounter rate");
                Equal(true, State.SequenceEqual(tool.Gen6Tiny), "translation keeps seed state");
                Equal("提示", translate.Invoke(null, new object[] { "Prompt" }), "prompt title translated");
                Equal("错误", translate.Invoke(null, new object[] { "Error" }), "error title translated");
                const string connection = "Connection lost, please double check your setup:\r\n(1) Disable PSS communications.\r\n(2) Use One Click function.";
                Equal(string.Join(Environment.NewLine, new[] { "连接已断开，请检查以下设置：", "(1) 关闭 PSS 通信。", "(2) 在 NTR 连接窗口使用「一键完成」。" }),
                    message.Invoke(null, new object[] { connection }), "full connection message translated");
                language.SetValue(settings, "en", null); tool.Translate();
                Equal("Blink(short)", type.Text, "English label restored");
                Equal("PokeReader seed", Control<CheckBox>(tool, "UseReaderSeed").Text, "reader label restored");
                Equal(2, type.SelectedValue, "language roundtrip keeps short blink selection");
            }
        }
        finally { language.SetValue(settings, previous, null); }
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
    private static T WildControl<T>(MainForm form, string name) where T : System.Windows.Forms.Control => Control<T>(form, name);

    private static void WildOptionsLayout()
    {
        var program = typeof(MainForm).Assembly.GetType("Pk3DSRNGTool.Program");
        using (var main = new MainForm())
        {
            program.GetField("mainform").SetValue(null, main);
            try
            {
                main.Show();
                Application.DoEvents();
                main.Ver = 0;
                var tabs = Control<TabControl>(main, "RNGMethod");
                tabs.SelectedIndex = 0;
                var grid = Control<DataGridView>(main, "DGV");
                var bounds = grid.Bounds;
                var idGrid = Control<DataGridView>(main, "DGV_ID");
                var idBounds = idGrid.Bounds;
                for (int repeat = 0; repeat < 3; repeat++)
                    for (int version = 0; version < 9; version++)
                    {
                        main.Ver = version;
                        for (int method = 0; method < tabs.TabCount; method++)
                        {
                            tabs.SelectedIndex = method;
                            Equal(bounds, grid.Bounds, "version/tab switching preserves full results area");
                            Equal(idBounds, idGrid.Bounds, "ID results retain their original bounds");
                        }
                    }
                main.Ver = 0;
                tabs.SelectedIndex = 2;
                Control<ComboBox>(main, "CB_Category").SelectedIndex = 0;
                var originalEncounterBounds = Control<Panel>(main, "EnctrPanel").Bounds;
                var originalSyncBounds = Control<ComboBox>(main, "SyncNature").Bounds;
                int ownedWindows = main.OwnedForms.Length;
                WildControl<CheckBox>(main, "TinyFinderWildEnabled").Checked = true;
                WildControl<TextBox>(main, "TinyFinderWildSeed").Text = "12345678";
                Equal(ownedWindows, main.OwnedForms.Length, "inline settings do not open a separate window");
                Equal(true, WildControl<FlowLayoutPanel>(main, "TinyFinderSeedRow").Visible, "seed is in the left settings group");
                Equal(bounds, grid.Bounds, "inline settings do not shrink results");
                var tips = Field<ToolTip>(main, "tinyWildTips");
                Equal(true, tips.GetToolTip(WildControl<LinkLabel>(main, "TinyFinderHelp")).Contains("NPC"), "question mark explains automatic location parameters");
                Equal(true, tips.GetToolTip(WildControl<NumericUpDown>(main, "TinyFinderParty")).Length > 0, "fields have hover help");
                WildOptionsVisibility(main);
                main.Ver = 0;
                Control<ComboBox>(main, "CB_Category").SelectedIndex = 0;
                WildControl<CheckBox>(main, "TinyFinderWildEnabled").Checked = false;
                Equal(originalEncounterBounds, Control<Panel>(main, "EnctrPanel").Bounds, "disabling TinyMT restores original encounter layout");
                Equal(originalSyncBounds, Control<ComboBox>(main, "SyncNature").Bounds, "disabling TinyMT restores original sync row");
                WildControl<CheckBox>(main, "TinyFinderWildEnabled").Checked = true;
                Equal("12345678", WildControl<TextBox>(main, "TinyFinderWildSeed").Text, "seed survives hiding inline settings");
                WildControl<ComboBox>(main, "TinyFinderWildSource").SelectedIndex = 1;
                CheckWildLayout(main);
                SaveLayoutPreview(Control<GroupBox>(main, "Wild_Setting"), "four-word-state");
                Control<RadioButton>(main, "CreateTimeline").Checked = true;
                Equal(false, WildControl<CheckBox>(main, "TinyFinderWildEnabled").Checked, "timeline still disables direct encounter calculation");
                tabs.SelectedIndex = 0;
                Equal(false, WildControl<FlowLayoutPanel>(main, "TinyFinderSeedRow").Visible, "other RNG tabs hide inline settings");
                tabs.SelectedIndex = 2;
                main.Ver = 7;
                Equal(false, WildControl<CheckBox>(main, "TinyFinderWildEnabled").Visible, "G7 retains its original controls");
                Equal(bounds, grid.Bounds, "all inline interactions preserve the results table");
            }
            finally { program.GetField("mainform").SetValue(null, null); }
        }
    }

    private static void WildOptionsVisibility(MainForm main)
    {
        int preview = 0;
        string[] names = { "Party", "Interact", "SearchLevel", "Honey", "MovingHorde", "ActiveNav", "Exclusive", "Music", "Emulator",
            "NPC", "Bag", "Blink", "HoneyDelay", "PossibleHorde", "RadarGrass" };
        Action<string[]> expect = visible => {
            foreach (var name in names)
                Equal(visible.Contains(name), WildControl<System.Windows.Forms.Control>(main, "TinyFinder" + name).Visible,
                    Control<ComboBox>(main, "CB_Category").Text + ": visibility of " + name);
            foreach (var name in new[] { "Party", "Interact", "SearchLevel" })
                Equal(visible.Contains(name), WildControl<Label>(main, "TinyFinder" + name + "Label").Visible, "labels follow their input: " + name);
            var options = Control<GroupBox>(main, "Wild_Setting");
            Application.DoEvents();
            CheckWildLayout(main);
            SaveLayoutPreview(options, "options-" + preview++);
        };
        Action<EncounterType> select = type => {
            var categories = main.IsORAS ? PKMW6.Species_ORAS : PKMW6.Species_XY;
            Control<ComboBox>(main, "CB_Category").SelectedIndex = Array.FindIndex(categories, c => ((PKMW6)c.List[0]).Type == type);
        };
        select(EncounterType.Normal);
        expect(new[] { "Party", "Honey" });
        WildControl<CheckBox>(main, "TinyFinderHoney").Checked = true;
        expect(new[] { "Party", "Honey", "Emulator" });
        Equal(false, Control<NumericUpDown>(main, "Special_th").Visible, "honey does not display a walking encounter rate");
        WildControl<CheckBox>(main, "TinyFinderHoney").Checked = false;
        select(EncounterType.Horde);
        expect(new[] { "Party", "MovingHorde", "Emulator" });
        WildControl<CheckBox>(main, "TinyFinderMovingHorde").Checked = true;
        expect(new[] { "Party", "MovingHorde" });
        Equal(true, Control<NumericUpDown>(main, "Special_th").Visible, "moving hordes display encounter rate");
        select(EncounterType.Fishing);
        var optionsProperty = main.GetType().GetProperty("TinyFinderWildOptions", BindingFlags.Instance | BindingFlags.NonPublic);
        Equal(false, ((TinyFinder.WildOptions)optionsProperty.GetValue(main, null)).MovingHorde, "hidden horde selection does not affect fishing");
        WildControl<CheckBox>(main, "TinyFinderMovingHorde").Checked = false;
        expect(new[] { "Party", "Interact", "Emulator" });
        Equal(true, Control<NumericUpDown>(main, "ChainLength").Visible, "fishing retains its chain input");
        select(EncounterType.RockSmash);
        expect(new[] { "Party", "Interact", "Emulator" });
        select(EncounterType.PokeRadar);
        expect(new[] { "Party", "Music" });
        select(EncounterType.FriendSafari);
        expect(new[] { "Party" });
        select(EncounterType.CaveShadow);
        expect(new[] { "Party" });
        main.Ver = 2;
        select(EncounterType.DexNav);
        expect(new[] { "Party", "SearchLevel", "ActiveNav", "Exclusive" });
        Equal(false, Control<NumericUpDown>(main, "ChainLength").Bounds.IntersectsWith(Control<NumericUpDown>(main, "Potential").Bounds),
            "DexNav chain and potential no longer overlap");
        WildControl<CheckBox>(main, "TinyFinderActiveNav").Checked = true;
        expect(new[] { "Party", "Interact", "SearchLevel", "ActiveNav", "Exclusive", "Emulator" });
        WildControl<CheckBox>(main, "TinyFinderExclusive").Checked = true;
        select(EncounterType.Normal);
        Equal(false, ((TinyFinder.WildOptions)optionsProperty.GetValue(main, null)).Exclusive, "hidden DexNav selection does not affect normal wild");
        expect(new[] { "Party", "Honey" });
    }

    private static void TinyWildXYToggle()
    {
        var program = typeof(MainForm).Assembly.GetType("Pk3DSRNGTool.Program");
        using (var main = new MainForm())
        {
            program.GetField("mainform").SetValue(null, main);
            try
            {
                main.Show();
                Application.DoEvents();
                main.Ver = 1; // Y: normal XY wild encounter
                Control<TabControl>(main, "RNGMethod").SelectedIndex = 2;
                Control<ComboBox>(main, "CB_Category").SelectedIndex = 0;
                var tiny = WildControl<CheckBox>(main, "TinyFinderWildEnabled");
                var calc = Control<Button>(main, "B_Calc");
                tiny.Checked = true;
                Application.DoEvents();
                calc.PerformClick();
                Application.DoEvents();
                tiny.Checked = false;
                Application.DoEvents();
                calc.PerformClick();
                Application.DoEvents();
                var location = Control<ComboBox>(main, "MetLocation");
                if (location.Items.Count > 5)
                    location.SelectedIndex = 5;
                Application.DoEvents();
                Equal(false, tiny.Checked, "XY toggle leaves TinyMT disabled");
            }
            finally { program.GetField("mainform").SetValue(null, null); }
        }
    }

    private static void CheckWildLayout(MainForm main)
    {
        var group = Control<GroupBox>(main, "Wild_Setting");
        CheckLayoutBounds(group);
        var leaves = new List<System.Windows.Forms.Control>();
        Action<System.Windows.Forms.Control> collect = null;
        collect = parent => {
            foreach (System.Windows.Forms.Control child in parent.Controls)
            {
                if (!child.Visible) continue;
                if (child is Panel) collect(child); else leaves.Add(child);
            }
        };
        collect(group);
        for (int i = 0; i < leaves.Count; i++)
            for (int j = i + 1; j < leaves.Count; j++)
            {
                var a = leaves[i].RectangleToScreen(leaves[i].ClientRectangle);
                var b = leaves[j].RectangleToScreen(leaves[j].ClientRectangle);
                Equal(false, a.IntersectsWith(b), "inline controls do not overlap: " + leaves[i].Name + " / " + leaves[j].Name);
            }
    }

    private static void CheckLayoutBounds(System.Windows.Forms.Control parent)
    {
        foreach (System.Windows.Forms.Control child in parent.Controls)
        {
            if (!child.Visible) continue;
            Equal(true, parent.ClientRectangle.Contains(child.Bounds), "visible control fits its container: " + child.Name);
            if (child is Panel) CheckLayoutBounds(child);
        }
    }

    private static void SaveLayoutPreview(System.Windows.Forms.Control form, string name)
    {
        string directory = Environment.GetEnvironmentVariable("RNGTOOL_LAYOUT_PREVIEW");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.Size));
            bitmap.Save(Path.Combine(directory, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }
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
            Invoke(tool, "B_Create_Click");
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

    private static void NativeStationaryTiny()
    {
        var program = typeof(MainForm).Assembly.GetType("Pk3DSRNGTool.Program");
        var settingsType = typeof(MainForm).Assembly.GetType("Pk3DSRNGTool.Properties.Settings");
        var settings = settingsType.GetProperty("Default").GetValue(null, null);
        Func<string, byte> getSetting = name => (byte)settingsType.GetProperty(name).GetValue(settings, null);
        Action<string, byte> setSetting = (name, value) => settingsType.GetProperty(name).SetValue(settings, value, null);
        byte oldGameVersion = getSetting("GameVersion"), oldMethod = getSetting("Method"), oldStoredMethod = getSetting("_Method");
        byte oldCategory = getSetting("Category"), oldPoke = getSetting("Poke");
        using (var main = new MainForm())
        {
            program.GetField("mainform").SetValue(null, main);
            try
            {
                main.Show();
                Application.DoEvents();
                main.Ver = 1; // Y: XY Berry Tree / Ambush categories
                Control<TabControl>(main, "RNGMethod").SelectedIndex = 0;
                Invoke(main, "RNGMethod_Changed");
                main.GetType().GetMethod("RefreshCategory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(main, null);
                var category = Control<ComboBox>(main, "CB_Category");
                var enabled = Control<CheckBox>(main, "TinyStationaryEnabled");
                var panel = Field<Panel>(main, "tinyStationaryPanel");
                var seed = Control<TextBox>(main, "TinyStationarySeed");
                var ignore = Control<CheckBox>(main, "IgnoreFilters");
                var time = Control<NumericUpDown>(main, "TimeSpan");
                var frameMin = Control<NumericUpDown>(main, "Frame_min");
                var frameMax = Control<NumericUpDown>(main, "Frame_max");
                var calc = Control<Button>(main, "B_Calc");

                var categories = Pokemon.getCategoryList(main.Ver, 0);
                int berryIndex = Array.FindIndex(categories, c => c.Text == "Berry Tree");
                int ambushIndex = Array.FindIndex(categories, c => c.Text == "Ambush Encounters");
                category.SelectedIndex = berryIndex;
                Invoke(main, "Poke_SelectedIndexChanged");
                if (!enabled.Visible)
                    throw new Exception("Berry category state: ver=" + main.Ver + ", rng=" + Control<TabControl>(main, "RNGMethod").SelectedIndex + ", parent=" + category.Parent.Parent.Name + ", index=" + category.SelectedIndex + ", text=" + category.Text + ", berry=" + berryIndex + ", count=" + category.Items.Count);
                Equal(false, panel.Visible, "Berry options stay hidden until enabled");
                enabled.Checked = true;
                seed.Text = "12345678";
                ignore.Checked = true;
                frameMin.Value = 0; frameMax.Value = 60; time.Value = 1;
                calc.PerformClick();
                Application.DoEvents();
                Equal(true, panel.Visible, "Berry options stay on the main page");
                Equal(true, Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().Any(), "Berry TinyMT search returns frames");
                Equal(true, Field<Form>(main, "tinyFinderWindow") == null, "Berry TinyMT search does not open TinyFinder");

                category.SelectedIndex = ambushIndex;
                Invoke(main, "Poke_SelectedIndexChanged");
                Equal(true, enabled.Visible, "Ambush category shows native TinyMT switch");
                Equal(true, panel.Visible, "Ambush keeps the native options visible");
                calc.PerformClick();
                Application.DoEvents();
                Equal(true, Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().Any(), "Ambush TinyMT search returns frames");
            }
            finally
            {
                program.GetField("mainform").SetValue(null, null);
                setSetting("GameVersion", oldGameVersion); setSetting("Method", oldMethod); setSetting("_Method", oldStoredMethod);
                setSetting("Category", oldCategory); setSetting("Poke", oldPoke);
            }
        }
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
    private static void AllReaderInputs()
    {
        using (var main = new MainForm())
        {
            foreach (string prefix in new[] { "IDTiny", "EggTiny" })
            {
                var mode = Control<CheckBox>(main, prefix + "UseReaderSeed");
                var input = Control<TextBox>(main, prefix + "ReaderSeed");
                mode.Checked = true; input.Text = "0x12345678";
                var adapter = Field<Pk3DSRNGTool.Controls.TinySeedInputAdapter>(main, prefix == "IDTiny" ? "idTinyInput" : "eggTinyInput");
                Equal(true, adapter.IsValid, prefix + " accepts prefixed seed");
                Equal(true, State.SequenceEqual(adapter.State), prefix + " matches TinyFinder init(seed, 0)");
                input.Text = "00000000";
                Equal(true, adapter.IsValid, prefix + " accepts zero seed");
                Equal(true, adapter.State.Any(w => w != 0), prefix + " zero seed is initialized, not a zero state");
                input.Text = "123456789";
                Equal(false, adapter.IsValid, prefix + " rejects overflow");
                input.Text = "12345678";
                mode.Checked = false;
                Equal(true, State.SequenceEqual(adapter.State), prefix + " manual mode preserves state");
                mode.Checked = true;
                adapter.State = new uint[] { 1, 2, 3, 4 };
                Equal(false, mode.Checked, prefix + " import selects state mode");
                Equal("1,2,3,4", string.Join(",", adapter.State), prefix + " word order");
            }
        }
    }
    private static void WildCoreIntegration()
    {
        var methods = new[] { TinyFinder.Controls.EnctrKey.Wild, TinyFinder.Controls.EnctrKey.Honey, TinyFinder.Controls.EnctrKey.Horde,
            TinyFinder.Controls.EnctrKey.Fishing, TinyFinder.Controls.EnctrKey.RockSmash, TinyFinder.Controls.EnctrKey.Radar,
            TinyFinder.Controls.EnctrKey.Ambush, TinyFinder.Controls.EnctrKey.DexNavMov, TinyFinder.Controls.EnctrKey.DexNavSrch };
        for (int ver = 0; ver < 4; ver++)
            foreach (var method in methods)
            {
                if (ver < 2 && (method == TinyFinder.Controls.EnctrKey.DexNavMov || method == TinyFinder.Controls.EnctrKey.DexNavSrch) ||
                    ver >= 2 && (method == TinyFinder.Controls.EnctrKey.Radar || method == TinyFinder.Controls.EnctrKey.Ambush)) continue;
                var areas = TinyFinder.WildIntegration.GetAreas(ver, method);
                Equal(true, areas.Count > 0, "catalog " + ver + " " + method);
                foreach (var area in areas)
                {
                    var options = new TinyFinder.WildOptions { Method = method, ORAS = ver >= 2, Rate = 100, NPC = area.NPC,
                        BagAdvances = area.BagAdvances, LongBlink = area.LongBlink, PossibleHorde = area.PossibleHorde,
                        RadarGrass = area.RadarGrass, FixedSpecies = area.Species[0], SearchLevel = 100, Exclusive = area.ExclusiveSpecies.Length > 0 };
                    var prediction = TinyFinder.WildIntegration.Predict(State, options, area);
                    Equal(method == TinyFinder.Controls.EnctrKey.Horde ? 5 : 1, prediction.Species.Length, "species " + area.Name + " " + method);
                    Equal(prediction.Species.Length, prediction.Levels.Length, "levels " + area.Name + " " + method);
                    Equal(true, prediction.Species.All(s => (s & 0x7FF) >= 1 && (s & 0x7FF) <= 721), "dex numbers " + area.Name + " " + method);
                    Equal(true, prediction.Levels.All(l => l >= 1 && l <= 100), "level range " + area.Name + " " + method);
                }
            }
    }
    private static void NativeWildIntegration()
    {
        var program = typeof(MainForm).Assembly.GetType("Pk3DSRNGTool.Program");
        using (var main = new MainForm())
        {
            program.GetField("mainform").SetValue(null, main);
            try
            {
                Invoke(main, "MainForm_Load"); main.Ver = 0;
                Control<TabControl>(main, "RNGMethod").SelectedIndex = 2;
                Control<ComboBox>(main, "CB_Category").SelectedIndex = 0;
                Invoke(main, "Poke_SelectedIndexChanged");
                WildControl<CheckBox>(main, "TinyFinderWildEnabled").Checked = true;
                WildControl<TextBox>(main, "TinyFinderWildSeed").Text = "0x12345678";
                WildControl<CheckBox>(main, "TinyFinderPossibleHorde").Checked = false;
                Control<NumericUpDown>(main, "Special_th").Value = 100;
                Control<CheckBox>(main, "IgnoreFilters").Checked = true;
                Control<NumericUpDown>(main, "Frame_min").Value = 10;
                Control<NumericUpDown>(main, "Frame_max").Value = 12;
                Equal(true, Control<ComboBox>(main, "MetLocation").Items.Count > 50, "XY full ordinary encounter catalog available in existing layout");
                Invoke(main, "Search_Click");
                Equal(true, Field<Form>(main, "tinyFinderWindow") == null, "main search does not open searcher");
                var frames = Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().ToArray();
                Equal(3, frames.Length, "main search produces native MT results");
                var options = (TinyFinder.WildOptions)main.GetType().GetProperty("TinyFinderWildOptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(main, null);
                var area = Field<object>(main, "ea") as TinyFinderArea6;
                var expected = TinyFinder.WildIntegration.Predict(State, options, area.Data);
                foreach (var frame in frames)
                {
                    var result = (ResultW6)frame.GetType().GetField("rt").GetValue(frame);
                    Equal(expected.Species[0] & 0x7FF, (int)result.Species, "native species matches original TinyFinder");
                    Equal(expected.Levels[0], result.Level, "native level matches original TinyFinder");
                    Equal((byte)expected.Slot, result.Slot, "native slot matches original TinyFinder");
                    Equal(expected.Synchronized, result.Synchronize, "native sync matches original TinyFinder");
                }
                var slotFilter = Field<Pk3DSRNGTool.Controls.CheckBoxComboBox>(main, "Slot");
                Invoke(main, "Reset_Click");
                Control<CheckBox>(main, "IgnoreFilters").Checked = false;
                foreach (var item in slotFilter.CheckBoxItems) item.Checked = false;
                slotFilter.CheckBoxItems[expected.Slot].Checked = true;
                Invoke(main, "Search_Click");
                Equal(3, Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().Count(), "matching TinyFinder slot passes native filters");
                slotFilter.CheckBoxItems[expected.Slot].Checked = false;
                slotFilter.CheckBoxItems[expected.Slot == 1 ? 2 : 1].Checked = true;
                Invoke(main, "Search_Click");
                Equal(0, Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().Count(), "different TinyFinder slot is excluded by native filters");
                Control<CheckBox>(main, "IgnoreFilters").Checked = true;
                foreach (var item in slotFilter.CheckBoxItems) item.Checked = false;
                for (int ver = 0; ver < 4; ver++)
                {
                    main.Ver = ver;
                    var groups = ver < 2 ? PKMW6.Species_XY : PKMW6.Species_ORAS;
                    for (int category = 0; category < groups.Length; category++)
                    {
                        if (((PKMW6)groups[category].List[0]).Type == EncounterType.Trap) continue;
                        Control<ComboBox>(main, "CB_Category").SelectedIndex = category;
                        Invoke(main, "Poke_SelectedIndexChanged");
                        WildControl<CheckBox>(main, "TinyFinderWildEnabled").Checked = true;
                        Control<NumericUpDown>(main, "Special_th").Value = 100;
                        options = (TinyFinder.WildOptions)main.GetType().GetProperty("TinyFinderWildOptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(main, null);
                        area = Field<object>(main, "ea") as TinyFinderArea6;
                        expected = TinyFinder.WildIntegration.Predict(State, options, area == null ? null : area.Data);
                        Invoke(main, "Search_Click");
                        int count = Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().Count();
                        Equal(expected.Triggered ? expected.Species.Length * 3 : 0, count, "in-place G6 result count " + ver + " " + category);
                    }
                }
                main.Ver = 2;
                Control<ComboBox>(main, "CB_Category").SelectedIndex = Array.FindIndex(PKMW6.Species_ORAS, c => ((PKMW6)c.List[0]).Type == EncounterType.DexNav);
                Invoke(main, "Poke_SelectedIndexChanged");
                WildControl<CheckBox>(main, "TinyFinderWildEnabled").Checked = true;
                WildControl<CheckBox>(main, "TinyFinderActiveNav").Checked = true;
                Control<ComboBox>(main, "SlotSpecies").SelectedIndex = 1;
                Invoke(main, "Search_Click");
                Equal(3, Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().Count(), "active DexNav generates in the main window");
                main.Ver = 0;
                Control<ComboBox>(main, "CB_Category").SelectedIndex = Array.FindIndex(PKMW6.Species_XY, c => ((PKMW6)c.List[0]).Type == EncounterType.PokeRadar);
                Invoke(main, "Poke_SelectedIndexChanged");
                WildControl<CheckBox>(main, "TinyFinderWildEnabled").Checked = true;
                Control<NumericUpDown>(main, "ChainLength").Value = 20;
                Control<ComboBox>(main, "SlotSpecies").SelectedIndex = 1;
                Control<CheckBox>(main, "AssumeSynced").Checked = true;
                Control<ComboBox>(main, "SyncNature").SelectedIndex = 1;
                Invoke(main, "Search_Click");
                var chained = Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().Select(f => (ResultW6)f.GetType().GetField("rt").GetValue(f)).ToArray();
                Equal(3, chained.Length, "chained Radar keeps native entry prediction");
                Equal(true, chained.All(r => r.Synchronize && r.Nature == 0), "Radar patch generation does not overwrite entry sync");
                Control<CheckBox>(main, "CB_ForcedShiny").Checked = true;
                Invoke(main, "Search_Click");
                Equal(true, Field<System.Collections.IEnumerable>(main, "Frames").Cast<object>().All(f => ((ResultW6)f.GetType().GetField("rt").GetValue(f)).Shiny), "entered shiny Radar patch keeps forced shiny generation");
                main.ShowTinyFinderMT();
                using (var mt = Field<Form>(main, "tinyFinderMTWindow"))
                {
                    mt.Location = new System.Drawing.Point(-3000, -3000);
                    var categories = Field<ComboBox>(mt, "Gen6Categories");
                    Equal(6, categories.Items.Count, "all original MT search modes retained");
                    for (int i = 0; i < 6; i++) categories.SelectedIndex = i;
                    Equal(true, Field<TabControl>(mt, "TabList").TabPages.Count >= 2, "MT and RTC search tabs retained");
                    categories.SelectedIndex = 0; Field<ComboBox>(mt, "ShinyType").SelectedIndex = 0;
                    var settingsType = mt.GetType().Assembly.GetType("TinyFinder.Properties.Settings");
                    var settings = settingsType.GetProperty("Default").GetValue(null, null);
                    var cpus = settingsType.GetProperty("CPUs");
                    int savedCPUs = (int)cpus.GetValue(settings, null); cpus.SetValue(settings, 1, null);
                    try
                    {
                        Field<Button>(mt, "MT_SearchButton").PerformClick();
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        var grid = Field<DataGridView>(mt, "G6_DGV");
                        while (grid.Rows.Count < 2 && timer.ElapsedMilliseconds < 10000) { Application.DoEvents(); Thread.Sleep(1); }
                        Equal(2, grid.Rows.Count, "original MT IV search runs from native entry point");
                    }
                    finally { cpus.SetValue(settings, savedCPUs, null); }
                    mt.Close();
                    Equal(true, mt.IsDisposed, "embedded MT window closes without hidden worker lifetime");
                }
            }
            finally { program.GetField("mainform").SetValue(null, null); }
        }
    }
    private static void OriginalTinyFinderWindow()
    {
        var program = typeof(MainForm).Assembly.GetType("Pk3DSRNGTool.Program");
        using (var main = new MainForm())
        {
            program.GetField("mainform").SetValue(null, main);
            try
            {
                Invoke(main, "MainForm_Load"); main.Ver = 0;
                Control<TabControl>(main, "RNGMethod").SelectedIndex = 2;
                var timeline = Field<TinyTimelineTool>(main, "TTT");
                Control<CheckBox>(timeline, "UseReaderSeed").Checked = true;
                Control<TextBox>(timeline, "TinySeed").Text = "12345678";
                main.ShowTinyFinder();
                using (var original = Field<Form>(main, "tinyFinderWindow"))
                {
                    original.Location = new System.Drawing.Point(-3000, -3000);
                    Equal("TinyFinder.Form1", original.GetType().FullName, "searcher uses original window");
                    Equal(true, Field<CheckBox>(original, "ReaderBTN").Checked, "reader mode transferred");
                    var t3 = original.Controls.Find("t3", true)[0];
                    Equal(0x12345678U, (uint)t3.GetType().GetProperty("Value").GetValue(t3, null), "reader seed transferred");
                    Equal(true, Field<ComboBox>(original, "locationsComboBox").Items.Count > 10, "original location database");
                    Equal(5, Field<ToolStripMenuItem>(original, "guidesToolStripMenuItem").DropDownItems.Count, "all five original guides retained");
                    Equal(true, Field<ComboBox>(original, "Methods").SelectedItem.ToString() != "ID", "wild method selected");
                    Field<NumericUpDown>(original, "min").Value = 0;
                    Field<NumericUpDown>(original, "max").Value = 20;
                    Field<Button>(original, "IgnoreFiltersButton").PerformClick();
                    var grid = Field<DataGridView>(original, "Generator");
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    while (grid.Rows.Count == 0 && timer.ElapsedMilliseconds < 10000) { Application.DoEvents(); Thread.Sleep(1); }
                    Equal(true, grid.Rows.Count > 0, "original wild generator produces rows");
                    grid.CurrentCell = grid.Rows[7].Cells["G_Index"];
                    var row = grid.Rows[7].DataBoundItem;
                    var expected = new[] { "Tiny0", "Tiny1", "Tiny2", "Tiny3" }.Select(n => (uint)row.GetType().GetProperty(n).GetValue(row, null)).ToArray();
                    Equal(true, original.GetType().GetMethod("ExportSelectedResult").Invoke(original, null), "original selection exported");
                    Equal(true, expected.SequenceEqual(main.TinySeeds), "host receives original result state");
                    Equal(1, WildControl<ComboBox>(main, "TinyFinderWildSource").SelectedIndex, "searcher result fills native state input");
                    Equal(true, WildControl<CheckBox>(main, "TinyFinderWildEnabled").Checked, "searcher result enables native encounter generator");
                    Equal(true, timeline.NeedsCalibration, "original result requires event calibration");
                    original.Close();
                }
            }
            finally { program.GetField("mainform").SetValue(null, null); }
        }
    }
}
