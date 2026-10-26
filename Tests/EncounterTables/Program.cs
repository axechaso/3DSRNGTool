using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Pk3DSRNGTool;
using Pk3DSRNGTool.Core;
using Pk3DSRNGTool.RNG;

internal static class Program
{
    private static int failures;
    private static int assertions;

    [STAThread]
    private static int Main()
    {
        foreach (int version in new[] { 7, 8 })
        {
            Check("Akala Outskirts day, version " + version, () => AssertSlots(94, 0, version, false,
                new[] { 735, 177, 759, 278, 278, 278, 299, 299, 759, 759 }));
            Check("Akala Outskirts night, version " + version, () => AssertSlots(94, 0, version, true,
                new[] { 2068, 278, 759, 278, 278, 278, 299, 299, 759, 759 }));
            Check("Route 11 day, version " + version, () => AssertSlots(108, 0, version, false,
                new[] { 775, 732, 47, 775, 674, 674, 166, 166, 166, 166 }));
            Check("Route 11 night, version " + version, () => AssertSlots(108, 0, version, true,
                new[] { 775, 732, 756, 775, 674, 674, 168, 168, 168, 168 }));

            int flower = version == 7 ? 546 : 548;
            Check("Poni Meadow day, version " + version, () => AssertSlots(156, 0, version, false,
                new[] { 743, flower, 6814, 6814, 6885, 6885, 743, flower, flower, flower }));
            Check("Poni Meadow night, version " + version, () => AssertSlots(156, 0, version, true,
                new[] { 743, flower, 6814, 6814, 6885, 6885, 200, flower, flower, flower }));

            foreach (bool night in new[] { false, true })
            {
                Check("Melemele Meadow forms, version " + version + ", night=" + night,
                    () => AssertSlots(40, 0, version, night,
                        new[] { 742, flower, 2717, 2717, 2789, 2789, 10, 11, 11, 12 }));
                Check("Route 6 Oricorio form, version " + version + ", night=" + night,
                    () => AssertSlots(54, 2, version, night, night
                        ? new[] { 506, 4837, 506, 736, 2067, 731, 731, 731, 133, 133 }
                        : new[] { 506, 4837, 174, 736, 734, 731, 174, 731, 133, 133 }));
                Check("Poni Wilds Gastrodon form, version " + version + ", night=" + night,
                    () => AssertSlots(158, 0, version, night,
                        new[] { 676, 279, 686, 279, 210, 210, 2471, 686, 686, 686 }));
                Check("Poni Breaker Coast Gastrodon form, version " + version + ", night=" + night,
                    () => AssertSlots(162, 0, version, night,
                        new[] { 73, 2471, 279, 279, 457, 457, 73, 457, 131, 131 }));
            }

            // Reverse-time tables must retain the two distinct Cleffa slots.
            Check("Mount Hokulani day, version " + version, () => AssertSlots(136, 1, version, false,
                new[] { 605, 774, 774, 605, 374, 374, 774, 132, 132, 132 }));
            Check("Mount Hokulani night, version " + version, () => AssertSlots(136, 1, version, true,
                new[] { 605, 774, 774, 173, 374, 374, 173, 132, 132, 132 }));

            int ape = version == 7 ? 766 : 765;
            Check("Lush Jungle night, version " + version, () => AssertSlots(90, 1, version, true,
                new[] { 753, 163, 438, 11, 762, 755, 764, ape, 127, 732 }));

            foreach (bool night in new[] { false, true })
            {
                int dragon = version == 7 ? 359 : 780;
                Check("Mount Lanakila cave, version " + version + ", night=" + night,
                    () => AssertSlots(146, 3, version, night,
                        new[] { 42, 362, 42, 362, 359, dragon, 215, 215, 215, 215 }));
            }
        }

        Check("All SM and USUM table variants", AssertAllTables);
        Check("All USUM normal encounters against extracted game data", AssertExtractedTables);
        Check("Static uses the local Oricorio form", AssertStaticForms);
        Check("USUM flower SOS forms", AssertFlowerAllies);
        Check("Main window uses version levels and form-aware slots", AssertMainWindow);
        Console.WriteLine("{0} assertions; {1} failed cases.", assertions, failures);
        return failures == 0 ? 0 : 1;
    }

    private static void Check(string name, Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("FAIL: {0}: {1}", name, ex.Message);
        }
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static int[] Expand(EncounterArea7 area, int version, bool night)
    {
        int[] species = area.getSpecies(version, night);
        return EncounterArea7.SlotType[species[0]].Select(i => species[i]).ToArray();
    }

    private static void AssertSlots(int location, int index, int version, bool night, int[] expected)
    {
        var area = LocationTable7.USUMTable.Single(a => a.Location == location && a.idx == index);
        int[] actual = Expand(area, version, night);
        Assert(actual.SequenceEqual(expected), "Expected " + string.Join(",", expected) + "; got " + string.Join(",", actual));

        // Exercise the real generator as well as the table used by the species filter.
        var wild = new Wild7
        {
            SpecForm = new[] { 0 }.Concat(actual).ToArray(),
            SlotSplitter = WildRNG.SlotDistribution[area.SlotDistributionIndex],
            Levelmin = area.GetLevelMin(version),
            Levelmax = area.GetLevelMax(version),
            Synchro_Stat = 25,
        };
        wild.Markslots();
        Type pool = typeof(Wild7).Assembly.GetType("Pk3DSRNGTool.Core.RNGPool", true);
        pool.GetMethod("CreateBuffer").Invoke(null, new object[] { new SFMT(0x12345678), 5000, true });
        MethodInfo rewind = pool.GetMethod("Rewind");
        for (int frame = 0; frame < 128; frame++)
        {
            rewind.Invoke(null, new object[] { frame });
            var result = (ResultW7)wild.Generate();
            Assert(result.Species == (expected[result.Slot - 1] & 0x7FF), "Generator species differs from the expected slot.");
            if (result.Species != 774)
                Assert(result.Forme == (expected[result.Slot - 1] >> 11), "Generator lost the encounter form.");
        }
    }

    public sealed class ExtractedTable
    {
        public int Version { get; set; }
        public int Location { get; set; }
        public int Index { get; set; }
        public bool Night { get; set; }
        public int SourceMap { get; set; }
        public int SourceTable { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }
        public int[] Species { get; set; }
        public int[] Rates { get; set; }
    }

    private static void AssertExtractedTables()
    {
        string json = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "USUM-normal-encounters.json"));
        var fixtures = new JavaScriptSerializer().Deserialize<ExtractedTable[]>(json);
        var expectedKeys = new HashSet<string>();
        foreach (var fixture in fixtures)
        {
            string key = fixture.Version + ":" + fixture.Location + ":" + fixture.Index + ":" + fixture.Night;
            Assert(expectedKeys.Add(key), "Duplicate fixture " + key);
            Check("Extracted map " + fixture.SourceMap + ", table " + fixture.SourceTable + ", " + key, () =>
            {
                var area = LocationTable7.USUMTable.Single(a => a.Location == fixture.Location && a.idx == fixture.Index);
                var species = Expand(area, fixture.Version, fixture.Night);
                var frequencies = new Dictionary<int, int>();
                for (int roll = 0; roll < 100; roll++)
                {
                    int encounter = species[WildRNG.getSlot(roll, area.SlotDistributionIndex) - 1];
                    if (!frequencies.ContainsKey(encounter)) frequencies[encounter] = 0;
                    frequencies[encounter]++;
                }
                Assert(fixture.Species.Length == fixture.Rates.Length && fixture.Rates.Sum() == 100, "Invalid fixture.");
                Assert(frequencies.Count == fixture.Species.Length, "Encounter species/forms differ from game data.");
                for (int i = 0; i < fixture.Species.Length; i++)
                    Assert(frequencies.ContainsKey(fixture.Species[i]) && frequencies[fixture.Species[i]] == fixture.Rates[i],
                        "Wrong probability for species/form " + fixture.Species[i]);
                Assert(area.GetLevelMin(fixture.Version) == fixture.LevelMin && area.GetLevelMax(fixture.Version) == fixture.LevelMax, "Wrong level range.");
            });
        }
        int areaCount = LocationTable7.USUMTable.Count(a => a.Location != 0);
        Assert(fixtures.Length == areaCount * 4, "Missing USUM version/time fixtures.");
        foreach (var area in LocationTable7.USUMTable.Where(a => a.Location != 0))
            foreach (int version in new[] { 7, 8 })
                foreach (bool night in new[] { false, true })
                    Assert(expectedKeys.Contains(version + ":" + area.Location + ":" + area.idx + ":" + night), "Area missing from game-data fixtures.");
    }

    private static void AssertStaticForms()
    {
        var area = LocationTable7.USUMTable.Single(a => a.Location == 40);
        var expected = Expand(area, 7, false);
        var wild = new Wild7
        {
            SpecForm = new[] { 0 }.Concat(expected).ToArray(),
            SlotSplitter = WildRNG.SlotDistribution[area.SlotDistributionIndex],
            Levelmin = area.LevelMin, Levelmax = area.LevelMax,
            Synchro_Stat = 25, Static = true,
        };
        wild.Markslots();
        Assert(wild.StaticMagnetSlot.SequenceEqual(new byte[] { 5, 6 }), "Static must attract Pom-Pom Oricorio's two electric slots.");
        Type pool = typeof(Wild7).Assembly.GetType("Pk3DSRNGTool.Core.RNGPool", true);
        pool.GetMethod("CreateBuffer").Invoke(null, new object[] { new SFMT(0x12345678), 5000, true });
        MethodInfo rewind = pool.GetMethod("Rewind");
        int attracted = 0;
        for (int frame = 0; frame < 128; frame++)
        {
            rewind.Invoke(null, new object[] { frame });
            ulong leadRoll = (ulong)pool.GetProperty("getrand64").GetValue(null);
            rewind.Invoke(null, new object[] { frame });
            var result = (ResultW7)wild.Generate();
            if (leadRoll % 100 >= 50)
            {
                attracted++;
                Assert(result.Species == 741 && result.Forme == 1, "Static selected the wrong species/form.");
            }
        }
        Assert(attracted > 0 && attracted < 128, "Both lead ability branches must be exercised.");
        foreach (var other in LocationTable7.USUMTable.Where(a => a.Location == 54 && a.idx == 2 || a.Location == 156))
        {
            var local = new Wild7 { SpecForm = new[] { 0 }.Concat(Expand(other, 7, false)).ToArray(), Static = true };
            local.Markslots();
            Assert(!local.Static && local.StaticMagnetSlot.Length == 0, "Static must not attract non-electric Oricorio forms.");
        }
    }

    private static void AssertFlowerAllies()
    {
        foreach (int version in new[] { 7, 8 })
            foreach (bool night in new[] { false, true })
            {
                Assert(SOSAllies.getAllies(40, 2717, version, night).SequenceEqual(new[] { 8861, 8861, 8861, 4765, 4765, 2717, 2717 }), "Wrong Flabebe SOS colors.");
                Assert(SOSAllies.getAllies(128, 670, version, night).SequenceEqual(new[] { 8862, 8862, 8862, 4766, 4766, 670, 670 }), "Wrong red Floette SOS colors.");
                Assert(SOSAllies.getAllies(156, 6814, version, night).SequenceEqual(new[] { 8862, 8862, 8862, 4766, 4766, 6814, 6814 }), "Wrong blue Floette SOS colors.");
            }
    }

    private static void AssertMainWindow()
    {
        Type program = typeof(MainForm).Assembly.GetType("Pk3DSRNGTool.Program", true);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using (var main = new MainForm())
        {
            program.GetField("mainform").SetValue(null, main);
            try
            {
                typeof(MainForm).GetMethod("MainForm_Load", flags).Invoke(main, new object[] { null, EventArgs.Empty });
                main.Ver = 7;
                main.Controls.Find("RNGMethod", true).Cast<TabControl>().Single().SelectedIndex = 2;
                main.Controls.Find("CB_Category", true).Cast<ComboBox>().Single().SelectedIndex = 0;
                typeof(MainForm).GetMethod("Poke_SelectedIndexChanged", flags).Invoke(main, new object[] { null, EventArgs.Empty });
                var location = main.Controls.Find("MetLocation", true).Cast<ComboBox>().Single();
                var levelMin = main.Controls.Find("Lv_min", true).Cast<NumericUpDown>().Single();
                var levelMax = main.Controls.Find("Lv_max", true).Cast<NumericUpDown>().Single();
                var lead = main.Controls.Find("LeadAbility", true).Cast<ComboBox>().Single();
                foreach (int version in new[] { 7, 8 })
                {
                    main.Ver = version;
                    foreach (var area in new[] { new[] { 268, 6, 9 }, new[] { 64, 22, 25 }, new[] { 134, 25, 28 }, new[] { 40, version == 8 ? 10 : 9, 12 } })
                    {
                        location.SelectedValue = area[0];
                        Assert(levelMin.Value == area[1] && levelMax.Value == area[2], "Main window loaded the wrong levels.");
                    }
                    lead.SelectedIndex = (int)Lead.Static;
                    var wild = (Wild7)typeof(MainForm).GetMethod("getWildSetting", flags).Invoke(main, null);
                    Assert(wild.StaticMagnetSlot.SequenceEqual(new byte[] { 5, 6 }), "Main search lost Oricorio's electric form.");
                    Assert(wild.SpecForm[5] == 2789 && wild.SpecForm[6] == 2789, "Main search received the wrong form.");
                }
            }
            finally { program.GetField("mainform").SetValue(null, null); }
        }
        // A version-dependent level does not require version-exclusive species.
        var levelOnly = new EncounterArea_SM { LevelMin = 27, lvldiff = -1, Species = new[] { 0, 10 } };
        Assert(!levelOnly.VersionDifference && levelOnly.GetLevelMin(6) == 26 && levelOnly.GetLevelMax(6) == 29,
            "Level differences must not depend on the version-exclusive species flag.");
    }

    private static void AssertAllTables()
    {
        foreach (bool ultra in new[] { false, true })
        {
            var table = ultra ? LocationTable7.USUMTable : LocationTable7.SMTable;
            foreach (var area in table.Where(a => a.Location != 0))
            {
                int[] original = (int[])area.Species.Clone();
                foreach (int version in ultra ? new[] { 7, 8 } : new[] { 5, 6 })
                {
                    int[] day = Expand(area, version, false);
                    int[] night = Expand(area, version, true);
                    Assert(day.Length == 10 && night.Length == 10, "Each normal encounter table needs ten slots.");
                    Assert(day.Concat(night).All(s => (s & 0x7FF) > 0 && (s & 0x7FF) <= 807), "Invalid encounter species.");
                    Assert(area.DayNightDifference == !day.SequenceEqual(night), "Incorrect day/night refresh flag at location " + area.Locationidx);
                    Assert(area.Species.SequenceEqual(original), "Reading a variant mutated the source table.");
                    Assert(day.SequenceEqual(Expand(area, version, false)), "Day slots changed after reading the night slots.");
                }
            }
        }
    }
}
