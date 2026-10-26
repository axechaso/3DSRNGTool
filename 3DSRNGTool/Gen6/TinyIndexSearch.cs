using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Pk3DSRNGTool.Core;
using Pk3DSRNGTool.RNG;

namespace Pk3DSRNGTool
{
    public enum TinyIndexMethod
    {
        Wild, FriendSafari, HordeHoney, HordeStep, Honey, Fishing, RockSmash,
        Radar, Ambush, ID, DexNavStep, DexNavSearch,
    }

    public sealed class TinyIndexSettings
    {
        public TinyIndexMethod Method;
        public bool IsORAS;
        public bool Surf, PossibleHorde, RadarGrass, BonusMusic, ShinyCharm, CompoundEyes, DexNavExclusive;
        public int PartySize = 6, SafariSlots = 3, EncounterRate = 30, NPCAdvances;
        public int BagAdvances = 15, HoneyDelay = 120, LongBlinkFrame = 48, InteractFrame = 60;
        public int ChainLength, SearchLevel;

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(TinyIndexMethod), Method) || PartySize < 1 || PartySize > 6 ||
                SafariSlots < 2 || SafariSlots > 3 || EncounterRate < 0 || EncounterRate > 100 ||
                NPCAdvances < 0 || NPCAdvances > 100 || BagAdvances < 0 || BagAdvances > 100 ||
                HoneyDelay < 0 || HoneyDelay > 10000 || LongBlinkFrame < 0 || LongBlinkFrame > 10000 ||
                InteractFrame < 0 || InteractFrame > 10000 || ChainLength < 0 || ChainLength > 999 ||
                SearchLevel < 0 || SearchLevel > 999)
                throw new ArgumentException("Invalid TinyMT search settings.");
            if (!IsORAS && (Method == TinyIndexMethod.DexNavStep || Method == TinyIndexMethod.DexNavSearch))
                throw new ArgumentException("DexNav requires Omega Ruby / Alpha Sapphire.");
            if (IsORAS && (Method == TinyIndexMethod.FriendSafari || Method == TinyIndexMethod.Radar || Method == TinyIndexMethod.Ambush))
                throw new ArgumentException("This encounter method requires X / Y.");
        }
    }

    public sealed class TinyIndexFilter
    {
        public bool? Sync;
        public int Slot;
        public bool TriggerOnly = true, HiddenAbilityOnly, ShinyOnly;
        public int MinimumPotential;
        public int? TID, TSV;

        public bool Matches(TinyIndexResult row) =>
            (!Sync.HasValue || Sync.Value == row.Synchronized) &&
            (Slot == 0 || Slot == row.Slot) && (!TriggerOnly || row.Triggered) &&
            (!HiddenAbilityOnly || row.HiddenAbility > 0) && (!ShinyOnly || row.SpecialShiny) &&
            row.Potential >= MinimumPotential && (!TID.HasValue || TID == row.TID) &&
            (!TSV.HasValue || TSV == row.TSV);
    }

    public sealed class TinyIndexResult
    {
        public TinyIndexMethod Method { get; internal set; }
        public bool IsORAS { get; internal set; }
        public Horde HordeData { get; internal set; }
        public int Index { get; set; }
        public uint[] State { get; internal set; }
        public string Status => string.Join(",", State.Reverse().Select(v => v.ToString("X8")));
        public uint Random { get; internal set; }
        public string RandHex => Random.ToString("X8");
        public bool Synchronized { get; internal set; }
        public bool Triggered { get; internal set; } = true;
        public int Slot { get; internal set; }
        public int EncounterRoll { get; internal set; }
        public int HiddenAbility { get; internal set; }
        public int Potential { get; internal set; }
        public bool EggMove { get; internal set; }
        public bool SpecialShiny { get; internal set; }
        public int Delay { get; internal set; }
        public int TID { get; internal set; }
        public int SID { get; internal set; }
        public int TSV => (TID ^ SID) >> 4;
        public int TRV => (TID ^ SID) & 15;
        public int[] Items { get; internal set; } = new int[0];
        public int[] Flutes { get; internal set; } = new int[0];
        public string ItemText => string.Join(",", Items.Select(i => Method == TinyIndexMethod.DexNavStep || Method == TinyIndexMethod.DexNavSearch
            ? i < 2 ? "物品" + (i + 1) : "-" : i == 0 ? "50%" : i == 1 ? "5%" : "-"));
        public string FluteText => string.Join(",", Flutes);
        public string Details { get; internal set; }
    }

    // Index zero is the supplied state BEFORE the next RNG call. Uses the existing
    // TinyMT implementation; encounter call ordering is documented in Tests/Gen6/README.md.
    public static class TinyIndexSearch
    {
        public static List<TinyIndexResult> Search(uint[] state, int min, int max, TinyIndexSettings settings,
            TinyIndexFilter filter, int limit = 10000, CancellationToken cancel = default(CancellationToken),
            IProgress<int> progress = null)
        {
            ValidateState(state);
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            if (min < 0 || max < min || max > 10000000 || limit < 1)
                throw new ArgumentException("Index range must be between 0 and 10000000.");
            var rng = new TinyMT(state);
            var rows = new List<TinyIndexResult>();
            for (int i = 0; i < min; i++)
            {
                if ((i & 1023) == 0) cancel.ThrowIfCancellationRequested();
                rng.Next();
            }
            for (int index = min; index <= max; index++, rng.Next())
            {
                cancel.ThrowIfCancellationRequested();
                var row = new Prediction(rng.status, settings).Run();
                row.Index = index;
                if (filter == null || filter.Matches(row)) rows.Add(row);
                if (rows.Count >= limit) break;
                if ((index & 1023) == 0) progress?.Report((int)(100L * (index - min) / (max - min + 1L)));
            }
            progress?.Report(100);
            return rows;
        }

        public static uint SeedFromRTC(DateTime rtc)
        {
            // Treat the entered clock as UTC. Host timezone/DST must not change the seed.
            long ticks = rtc.Ticks - new DateTime(2000, 1, 1).Ticks;
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(rtc));
            return unchecked((uint)(ticks / TimeSpan.TicksPerMillisecond));
        }

        private static void ValidateState(uint[] state)
        {
            if (state == null || state.Length != 4 || (state[0] & 0x7FFFFFFF) == 0 && state[1] == 0 && state[2] == 0 && state[3] == 0)
                throw new ArgumentException("Enter a nonzero four-word TinyMT state.");
        }

        public static TinyIndexResult Predict(uint[] state, TinyIndexSettings settings)
        {
            ValidateState(state);
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            return new Prediction(state, settings).Run();
        }

        private sealed class Prediction
        {
            private readonly TinyMT rng;
            private readonly TinyIndexSettings s;
            private readonly TinyIndexResult r;
            private byte lead;
            private int Rand(int range) => (int)(rng.Nextuint() * (ulong)range >> 32);
            private void Advance(int count) { for (int i = 0; i < count; i++) rng.Next(); }
            private int Flute() => WildRNG.getFluteBoost((ulong)Rand(100));
            private int Item() { int n = Rand(100); return n < 50 ? 0 : n < 55 ? 1 : 2; }
            private int Slot(byte type) => WildRNG.getSlot(Rand(100), type);

            internal Prediction(uint[] state, TinyIndexSettings settings)
            {
                rng = new TinyMT(state);
                s = settings;
                r = new TinyIndexResult { State = (uint[])state.Clone(), Random = rng.temper(), Method = s.Method, IsORAS = s.IsORAS };
            }

            internal TinyIndexResult Run()
            {
                switch (s.Method)
                {
                    case TinyIndexMethod.ID:
                        uint id = rng.Nextuint();
                        r.Random = id; r.TID = (ushort)id; r.SID = (ushort)(id >> 16);
                        break;
                    case TinyIndexMethod.Wild:
                    case TinyIndexMethod.FriendSafari:
                        Normal(); FinishWild(); break;
                    case TinyIndexMethod.Fishing:
                        Fishing(); Normal(); FinishWild(); break;
                    case TinyIndexMethod.RockSmash:
                        RockSmash(); r.EncounterRoll = Rand(3); r.Triggered = r.EncounterRoll == 0;
                        r.Synchronized = Rand(100) < 50; FinishWild(); break;
                    case TinyIndexMethod.HordeStep:
                        Advance(s.NPCAdvances);
                        int roll = Rand(100);
                        lead = (byte)Rand(100); r.Synchronized = lead < 50;
                        r.EncounterRoll = Rand(100);
                        r.Triggered = roll < 5 && r.EncounterRoll < s.EncounterRate;
                        if (s.RadarGrass) Advance(1);
                        FinishHorde(); break;
                    case TinyIndexMethod.HordeHoney:
                    case TinyIndexMethod.Honey:
                        Advance(s.PartySize * 3 + s.BagAdvances - 1);
                        int blink = s.LongBlinkFrame - (s.IsORAS ? 4 : 0);
                        r.Delay = s.LongBlinkFrame + s.HoneyDelay;
                        Blinks(ref blink, r.Delay, true);
                        lead = (byte)Rand(100); r.Synchronized = lead < 50;
                        if (s.Method == TinyIndexMethod.HordeHoney) FinishHorde(); else FinishWild();
                        break;
                    case TinyIndexMethod.Ambush:
                        r.Slot = Slot(2); r.Synchronized = Rand(100) < 50;
                        Advance(1); r.Items = new[] { Item() }; break;
                    case TinyIndexMethod.Radar: Radar(); break;
                    case TinyIndexMethod.DexNavStep:
                    case TinyIndexMethod.DexNavSearch: DexNav(); break;
                }
                return r;
            }

            private void Normal()
            {
                Advance(s.NPCAdvances);
                int roll = Rand(100);
                if (s.PossibleHorde) Advance(1);
                r.Synchronized = (rng.temper() * 100ul >> 32) < 50;
                r.EncounterRoll = Rand(100);
                r.Triggered = r.EncounterRoll < s.EncounterRate && (!s.PossibleHorde || roll >= 5);
                if (s.RadarGrass) Advance(1);
            }

            private void FinishWild()
            {
                byte type = s.Method == TinyIndexMethod.FriendSafari ? (byte)(52 + s.SafariSlots) :
                    s.Method == TinyIndexMethod.Fishing ? (byte)3 : s.Method == TinyIndexMethod.RockSmash || s.Surf ? (byte)4 : (byte)2;
                r.Slot = Slot(type);
                if (s.IsORAS) r.Flutes = new[] { Flute() };
                Advance(1); r.Items = new[] { Item() };
            }

            private void FinishHorde()
            {
                r.Slot = Slot(3);
                if (Rand(100) < 20) r.HiddenAbility = Rand(5) + 1;
                if (s.IsORAS) r.Flutes = Enumerable.Range(0, 5).Select(i => Flute()).ToArray();
                Advance(1);
                byte[] items = Enumerable.Range(0, 5).Select(i => (byte)Rand(100)).ToArray();
                r.Items = items.Select(i => i < 50 ? 0 : i < 55 ? 1 : 2).ToArray();
                r.HordeData = new Horde(lead, (byte)r.Slot, (byte)r.HiddenAbility,
                    s.IsORAS ? r.Flutes.Select(i => (byte)i).ToArray() : new byte[5], items);
            }

            private void Blinks(ref int frame, int end, bool shortBlink)
            {
                while (frame < end)
                {
                    if (shortBlink || rng.Nextuint() > 0x55555555)
                    {
                        frame += TinyStatus.getcooldown1(rng.Nextuint());
                        shortBlink = false;
                    }
                    else
                    {
                        frame += TinyStatus.getcooldown2(rng.Nextuint());
                        shortBlink = true;
                    }
                }
                lastShort = shortBlink;
            }

            private void Fishing()
            {
                int blink = s.LongBlinkFrame + TinyStatus.getcooldown1(rng.temper()) - (s.IsORAS ? 16 : 0);
                Advance(s.PartySize * 3 + s.NPCAdvances);
                int delayRollFrame = s.InteractFrame + 136;
                Blinks(ref blink, delayRollFrame, false);
                int end = delayRollFrame + 58 + Rand(7) * 30;
                // The first simulation ends at a cycle boundary. A short cycle is
                // followed by a forced long one; preserve that phase across waits.
                ContinueBlinks(ref blink, end);
                r.Delay = end - s.InteractFrame;
            }

            private bool lastShort;
            private void ContinueBlinks(ref int frame, int end) => BlinksKeepingPhase(ref frame, end, lastShort);

            private void BlinksKeepingPhase(ref int frame, int end, bool shortBlink)
            {
                Blinks(ref frame, end, shortBlink);
            }

            private void RockSmash()
            {
                Advance(s.PartySize * 3 + s.NPCAdvances);
                int blink = s.LongBlinkFrame + TinyStatus.getcooldown1(rng.temper()) - (s.IsORAS ? 16 : 0);
                BlinksKeepingPhase(ref blink, s.InteractFrame + 18, false); Advance(3);
                ContinueBlinks(ref blink, s.InteractFrame + 66); Advance(1);
                ContinueBlinks(ref blink, s.InteractFrame + 276);
                r.Delay = 276;
            }

            private void Radar()
            {
                PokeRadar radar;
                if (s.ChainLength == 0)
                {
                    r.Synchronized = Rand(100) < 50;
                    r.Slot = Slot(2); Advance(1); r.Items = new[] { Item() };
                    radar = new PokeRadar(rng.status, 0, 0, false, currentMusic: true);
                }
                else
                {
                    // Bag use consumes 27 calls. PokeRadar includes party memory calls.
                    Advance(27);
                    radar = new PokeRadar(rng.status, s.PartySize, s.ChainLength, s.BonusMusic);
                }
                r.SpecialShiny = radar.Shiny;
                r.Details = radar.Music + ": " + string.Join(" / ", radar.Overview);
            }

            private static readonly int[] Grades = { 5, 10, 25, 50, 100 };
            private static readonly int[] HARates = { 0, 0, 5, 15, 20, 25 };
            private static readonly int[] IVRates = { 0,0,0, 10,0,0, 15,10,0, 20,15,5, 15,20,5, 10,25,10 };
            private static readonly int[] EggRates = { 20, 50, 55, 60, 65, 80 };
            private static readonly int[] HeldRates = { 40,10, 40,10, 45,15, 50,20, 50,20, 50,30 };

            private void DexNav()
            {
                bool active = s.Method == TinyIndexMethod.DexNavSearch;
                if (active)
                {
                    int blink = s.LongBlinkFrame + TinyStatus.getcooldown1(rng.temper()) - 4;
                    Advance(1); r.Delay = 138 + 40 * Rand(3);
                    Blinks(ref blink, s.InteractFrame + r.Delay, false);
                }
                else
                {
                    Advance(1 + s.NPCAdvances);
                    r.Triggered = Rand(100) < 50;
                }
                int x, y;
                switch (Rand(4))
                {
                    case 0: x = -9 + Rand(18); y = -9 + Rand(3); break;
                    case 1: x = -9 + Rand(3); y = -7 + Rand(14); break;
                    case 2: x = 7 + Rand(3); y = -7 + Rand(14); break;
                    default: x = -9 + Rand(18); y = 7 + Rand(3); break;
                }
                int type = (Rand(100) < 30 || active) && s.DexNavExclusive ? 2 : s.Surf ? 1 : 0;
                bool boost = s.ChainLength > 0 && (s.ChainLength + 1) % 5 == 0 || Rand(100) < 4;
                r.Synchronized = Rand(100) < 50;
                if (!active)
                {
                    int slot;
                    for (slot = (type == 0 ? 12 : type == 1 ? 5 : 3); slot > 0; slot--)
                        if (Rand(100) < 30) break;
                    r.Slot = Math.Max(1, slot);
                }
                Advance(1); // Level roll
                r.Flutes = new[] { Flute() };
                int grade = Grades.Count(g => s.SearchLevel >= g);
                r.HiddenAbility = Rand(100) < HARates[grade] ? 1 : 0;
                int potential;
                for (potential = 2; potential >= 0; potential--)
                    if (Rand(100) < IVRates[grade * 3 + potential]) break;
                r.Potential = Math.Min(3, potential + (boost ? 2 : 1));
                r.EggMove = Rand(100) < EggRates[grade] || boost;
                int item = Rand(100), held;
                for (held = 0; held < 2; held++)
                {
                    item -= HeldRates[grade * 2 + held] + (s.CompoundEyes ? 5 : 0);
                    if (item < 0) break;
                }
                r.Items = new[] { held == 2 ? 3 : held };
                int checks = (s.ShinyCharm ? 3 : 1) + (boost ? 4 : 0) + (s.ChainLength == 49 ? 5 : s.ChainLength == 99 ? 10 : 0);
                int target = s.SearchLevel > 200 ? s.SearchLevel + 600 : s.SearchLevel > 100 ? s.SearchLevel * 2 + 400 : s.SearchLevel * 6;
                for (int i = 0; i < checks; i++)
                    if (Rand(10000) < target * 0.01) r.SpecialShiny = true;
                r.Details = $"({x},{-y}) Lv+{s.ChainLength / 5 + (boost ? 10 : 0)}";
            }
        }
    }
}
