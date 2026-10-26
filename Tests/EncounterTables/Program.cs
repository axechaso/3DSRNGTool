using System;
using System.Linq;
using System.Reflection;
using Pk3DSRNGTool;
using Pk3DSRNGTool.Core;
using Pk3DSRNGTool.RNG;

internal static class Program
{
    private static int failures;
    private static int assertions;

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
                new[] { 743, flower, 670, 670, 741, 741, 743, flower, flower, flower }));
            Check("Poni Meadow night, version " + version, () => AssertSlots(156, 0, version, true,
                new[] { 743, flower, 670, 670, 741, 741, 200, flower, flower, flower }));

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
            SlotSplitter = WildRNG.SlotDistribution[0],
            Levelmin = area.LevelMin,
            Levelmax = area.LevelMax,
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
        }
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
