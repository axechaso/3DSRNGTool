using System;
using System.Collections.Generic;
using System.Linq;
using TinyFinder.Controls;
using TinyFinder.Main;

namespace TinyFinder
{
    // Public data/calculation boundary for hosts. No Form1 or UI automation is involved.
    public sealed class WildAreaData
    {
        public string Name { get; internal set; }
        public string Terrain { get; internal set; }
        public int Map { get; internal set; }
        public int[] Species { get; internal set; }
        public int[] Levels { get; internal set; }
        public int[] ExclusiveSpecies { get; internal set; }
        public int ExclusiveLevel { get; internal set; }
        public int NPC { get; internal set; }
        public int Rate { get; internal set; }
        public int BagAdvances { get; internal set; }
        public int LongBlink { get; internal set; }
        public int EmulatorLongBlink { get; internal set; }
        public bool PossibleHorde { get; internal set; }
        public bool RadarGrass { get; internal set; }
        public bool Surf => Terrain == "Water";
    }

    public sealed class WildOptions
    {
        public EnctrKey Method;
        public bool ORAS, MovingHorde, PossibleHorde, RadarGrass, BonusMusic, ShinyCharm, Exclusive;
        public int NPC, PartySize = 6, Rate = 13, BagAdvances = 27, LongBlink = 48;
        public int InteractFrame = 60, HoneyDelay = 112, Chain, SearchLevel, SafariSlots = 3;
        public int FixedSpecies, Flute;
    }

    public sealed class WildPrediction
    {
        public bool Synchronized { get; internal set; }
        public bool Triggered { get; internal set; }
        public int Slot { get; internal set; }
        public int[] Species { get; internal set; }
        public byte[] Levels { get; internal set; }
        public byte[] Items { get; internal set; }
        public int HiddenAbility { get; internal set; }
        public int Potential { get; internal set; }
        public bool Shiny { get; internal set; }
        public int Delay { get; internal set; }
        public string EggMove { get; internal set; }
        public string Details { get; internal set; }
        public bool IsHorde => Species.Length == 5;
    }

    public static class WildIntegration
    {
        private static readonly object prepareLock = new object(); // Upstream PrepareRow uses static scratch fields.
        public static int[] GetSafariSpecies() => Species.getFSList().Select(n => ToSpeciesForm(n)).ToArray();
        // TinyFinder appends these forms after dex 721; the host packs form in bits 11+.
        private static readonly int[] appendedForms = { 422 | 1 << 11, 423 | 1 << 11, 550 | 1 << 11,
            669 | 1 << 11, 669 | 2 << 11, 669 | 3 << 11, 669 | 4 << 11,
            670 | 1 << 11, 670 | 2 << 11, 670 | 3 << 11, 670 | 4 << 11,
            710 | 1 << 11, 710 | 2 << 11, 710 | 3 << 11 };
        public static int ToSpeciesForm(int number) => number >= 723 && number < 723 + appendedForms.Length ? appendedForms[number - 723] : number;
        private static int ToFinderNumber(int speciesForm)
        {
            int index = Array.IndexOf(appendedForms, speciesForm);
            return index < 0 ? speciesForm : index + 723;
        }

        public static List<WildAreaData> GetAreas(int gameVersion, EnctrKey method)
        {
            if (gameVersion < 0 || gameVersion > 3) throw new ArgumentOutOfRangeException(nameof(gameVersion));
            var result = new List<WildAreaData>();
            var locations = gameVersion < 2 ? TableXY.EncounterTable(gameVersion == 0) : TableORAS.EncounterTable(gameVersion == 2);
            foreach (var loc in locations)
            {
                if (method == EnctrKey.Honey && !loc.HasHoneyWild() || method == EnctrKey.Radar && !loc.HasRadar()) continue;
                Action<string, ushort[], int[]> add = (terrain, species, levels) => {
                    if (species == null || levels == null || species.Length == 0) return;
                    result.Add(new WildAreaData {
                        Name = loc.Name, Terrain = terrain, Map = loc.Map,
                        Species = species.Select(n => (int)n).ToArray(), Levels = (int[])levels.Clone(),
                        ExclusiveSpecies = loc.DexNavTable == null ? new int[0] : loc.DexNavTable.Select(n => (int)n).ToArray(),
                        ExclusiveLevel = loc.DexNavLevel, NPC = loc.NPC, Rate = terrain == "Water" ? 7 : loc.Enc_Ratio == 0 ? 13 : loc.Enc_Ratio,
                        BagAdvances = loc.Bag_Advances == 0 ? gameVersion < 2 ? 27 : 15 : loc.Bag_Advances,
                        LongBlink = loc.FirstLongBlinkRand, EmulatorLongBlink = loc.FirstLongBlinkRand_Emu,
                        PossibleHorde = gameVersion < 2 ? terrain != "Water" && terrain != "Swamp" && terrain != "Riding" && terrain != "Long Grass" && loc.HasMovingHordes() : terrain == "Long Grass",
                        RadarGrass = gameVersion < 2 && terrain != "Water" && terrain != "Long Grass" && terrain != "Swamp" && terrain != "Riding" && loc.HasRadar(),
                    });
                };
                switch (method)
                {
                    case EnctrKey.Fishing:
                        add("Old Rod", loc.OldTable, loc.OldLevel); add("Good Rod", loc.GoodTable, loc.GoodLevel); add("Super Rod", loc.SuperTable, loc.SuperLevel); break;
                    case EnctrKey.RockSmash: add("Rock Smash", loc.SmashTable, loc.SmashLevel); break;
                    case EnctrKey.Ambush: add("Cave Shadows", loc.AmbushTable, loc.AmbushLevel); break;
                    case EnctrKey.Horde:
                        if (loc.HordeTable1 != null && loc.HordeTable2 != null && loc.HordeTable3 != null)
                            add("Horde", loc.HordeTable1.Concat(loc.HordeTable2).Concat(loc.HordeTable3).ToArray(), loc.HordeLevel);
                        break;
                    default:
                        bool honeyLand = method != EnctrKey.Honey || loc.HordeTable1 == null && loc.RideTable == null;
                        if (honeyLand) add("Grass", loc.GrassTable, loc.WildLevel);
                        if (method != EnctrKey.Radar)
                        {
                            if (honeyLand) { add("Long Grass", loc.LongTable, loc.WildLevel); add("Cave", loc.CaveTable, loc.WildLevel); }
                            add("Water", loc.SurfTable, loc.SurfLevel);
                        }
                        if (method != EnctrKey.DexNavMov && method != EnctrKey.DexNavSrch)
                        {
                            if (honeyLand) { add("Red Flowers", loc.RedTable, loc.RedLevel); add("Yellow Flowers", loc.YellowTable, loc.YellowLevel); add("Purple Flowers", loc.PurpleTable, loc.PurpleLevel); }
                            if (method != EnctrKey.Radar) { add("Swamp", loc.SwampTable, loc.SwampLevel); add("Riding", loc.RideTable, loc.WildLevel); }
                        }
                        break;
                }
            }
            return result;
        }

        public static WildPrediction Predict(uint[] state, WildOptions options, WildAreaData area)
        {
            if (state == null || state.Length != 4 || (state[0] & 0x7fffffff) == 0 && state[1] == 0 && state[2] == 0 && state[3] == 0)
                throw new ArgumentException("Enter a nonzero four-word TinyMT state.");
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Method != EnctrKey.FS && area == null) throw new ArgumentException("Select a TinyFinder encounter location.");
            if (options.Method == EnctrKey.ID || !Enum.IsDefined(typeof(EnctrKey), options.Method) || options.PartySize < 1 || options.PartySize > 6 ||
                options.Rate < 0 || options.Rate > 100 || options.NPC < 0 || options.NPC > 100 || options.BagAdvances < 0 || options.BagAdvances > 100 ||
                options.LongBlink < 0 || options.LongBlink > 10000 || options.HoneyDelay < 0 || options.HoneyDelay > 10000 ||
                options.InteractFrame < 0 || options.InteractFrame > 10000 || options.SearchLevel < 0 || options.SearchLevel > 999 || options.Chain < 0 || options.Chain > 999 || options.SafariSlots < 2 || options.SafariSlots > 3)
                throw new ArgumentException("Invalid TinyMT search settings.");
            var s = new UISettings {
                encounterKey = options.Method, oras = options.ORAS, moving = options.MovingHorde,
                surfing = area != null && area.Surf, possibleHorde = options.PossibleHorde, radarGrass = options.RadarGrass,
                bonusMusic = options.BonusMusic, charm = options.ShinyCharm, exclusives = options.Exclusive,
                calibration = options.NPC, party = options.PartySize, ratio = options.Rate,
                advances = options.PartySize * 3 + (options.Method == EnctrKey.Fishing || options.Method == EnctrKey.RockSmash ? options.NPC : options.BagAdvances - 1),
                longBlinkRand = options.LongBlink, interactMTFrame = options.InteractFrame, honeyDelay = options.HoneyDelay,
                chain = options.Chain, searchLevel = (ushort)options.SearchLevel, Grade = DexNav.GetGrade((ushort)options.SearchLevel),
                TargetValue = DexNav.GetTargetValue((ushort)options.SearchLevel), DNsearching = options.Method == EnctrKey.DexNavSrch,
                FixedSpecies = (ushort)ToFinderNumber(options.FixedSpecies), DexNumberFS = (ushort)ToFinderNumber(options.FixedSpecies), fluteOption = options.Flute,
                currentSlots = area == null ? new ushort[options.SafariSlots] : area.Species.Select(n => (ushort)n).ToArray(),
                currentLevels = area == null ? Enumerable.Repeat(30, options.SafariSlots).ToArray() : area.Levels,
                specialSlots = area == null ? new ushort[0] : area.ExclusiveSpecies.Select(n => (ushort)n).ToArray(),
                dexNavLevel = area == null ? 0 : area.ExclusiveLevel,
                maxEggRand = area != null && (area.Terrain == "Long Grass" || area.Terrain == "Grass" && area.Rate != 1) ? 2 : 10,
                // Calculate all outcomes before the host applies its own encounter and IV filters.
                triggerOnly = false,
            };
            s.sType = options.Method == EnctrKey.Fishing ? 3 : options.Method == EnctrKey.RockSmash || s.surfing ? 4 :
                options.Method == EnctrKey.FS ? options.SafariSlots - 1 : 0;
            if (options.Method == EnctrKey.DexNavMov || options.Method == EnctrKey.DexNavSrch) s.sType = s.surfing ? 1 : 0;
            if (options.Method == EnctrKey.Radar) s.advances = options.PartySize * 3 + 27;
            Index row = options.Method == EnctrKey.Horde ? (Index)new Horde(state, s) : options.Method == EnctrKey.Radar ? new Radar(state, s) :
                options.Method == EnctrKey.DexNavMov || options.Method == EnctrKey.DexNavSrch ? (Index)new DexNav(state, s) : new Wild(state, s);
            bool noTriggerCheck = options.Method == EnctrKey.Honey || options.Method == EnctrKey.Horde && !options.MovingHorde ||
                options.Method == EnctrKey.Ambush || options.Method == EnctrKey.Radar || options.Method == EnctrKey.DexNavSrch;
            bool radarChain = options.Method == EnctrKey.Radar && options.Chain > 0;
            if (!radarChain)
                lock (prepareLock) PrepareRow.Prepare(row, s);
            int[] speciesResult;
            if (options.Method == EnctrKey.Horde) speciesResult = area.Species.Skip((row.slot - 1) * 5).Take(5).ToArray();
            else if (options.Method == EnctrKey.FS || s.DNsearching || radarChain) speciesResult = new[] { ToFinderNumber(options.FixedSpecies) };
            else speciesResult = new[] { (row.EnctrType == 2 ? s.specialSlots : s.currentSlots)[row.slot - 1] }.Select(n => (int)n).ToArray();
            return new WildPrediction {
                Synchronized = row.Sync, Triggered = noTriggerCheck || row.trigger, Slot = row.slot,
                Species = speciesResult.Select(ToSpeciesForm).ToArray(), Levels = radarChain ? new[] { (byte)s.currentLevels[0] } : row.Level.Split(',').Select(n => (byte)Math.Max(1, Math.Min(100, int.Parse(n.Trim())))).ToArray(),
                Items = options.Method == EnctrKey.Horde ? row.itemSlots.Select(n => (byte)(n < 2 ? n : 3)).ToArray() : new[] { (byte)(row.itemSlot < 2 ? row.itemSlot : 3) },
                HiddenAbility = options.Method == EnctrKey.Horde ? row.HordeHA : row.DexNavHA ? 1 : 0,
                Potential = row.potential, Shiny = row.shiny, Delay = row.ActualDelay, EggMove = row.eggMove,
                Details = row is Radar ? string.Join(Environment.NewLine, ((Radar)row).Overview) : row.timeline,
            };
        }
    }
}
