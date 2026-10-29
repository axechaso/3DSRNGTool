using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using TinyFinder;
using EnctrKey = TinyFinder.Controls.EnctrKey;
using Pk3DSRNGTool.Core;
using Pk3DSRNGTool.RNG;

namespace Pk3DSRNGTool
{
    public partial class MainForm
    {
        private readonly CheckBox tinyWildEnabled = WildCheck("TinyFinderWildEnabled", "计算 TinyMT 遭遇");
        private readonly ComboBox tinyWildSource = new ComboBox { Name = "TinyFinderWildSource", DropDownStyle = ComboBoxStyle.DropDownList, Width = 128 };
        private readonly TextBox tinyWildSeed = new TextBox { Name = "TinyFinderWildSeed", Text = "00000000", Width = 300, CharacterCasing = CharacterCasing.Upper };
        private readonly NumericUpDown tinyWildNPC = WildNumber("TinyFinderNPC", 0, 100, 0), tinyWildParty = WildNumber("TinyFinderParty", 1, 6, 6);
        private readonly NumericUpDown tinyWildBag = WildNumber("TinyFinderBag", 0, 100, 27), tinyWildHoneyDelay = WildNumber("TinyFinderHoneyDelay", 0, 10000, 112);
        private readonly NumericUpDown tinyWildBlink = WildNumber("TinyFinderBlink", 0, 10000, 48), tinyWildInteract = WildNumber("TinyFinderInteract", 0, 10000, 60);
        private readonly NumericUpDown tinyWildSearchLevel = WildNumber("TinyFinderSearchLevel", 0, 999, 0);
        private readonly CheckBox tinyWildHoney = WildCheck("TinyFinderHoney", "蜂蜜单体"), tinyWildMovingHorde = WildCheck("TinyFinderMovingHorde", "走路群战");
        private readonly CheckBox tinyWildPossibleHorde = WildCheck("TinyFinderPossibleHorde", "可能触发群战"), tinyWildRadarGrass = WildCheck("TinyFinderRadarGrass", "XY 草丛额外调用");
        private readonly CheckBox tinyWildActiveNav = WildCheck("TinyFinderActiveNav", "主动 DexNav"), tinyWildExclusive = WildCheck("TinyFinderExclusive", "DexNav 限定种");
        private readonly CheckBox tinyWildMusic = WildCheck("TinyFinderMusic", "奖励音乐"), tinyWildEmulator = WildCheck("TinyFinderEmulator", "模拟器");
        private readonly Label tinyWildSummary = new Label { Name = "TinyFinderWildSummary", AutoSize = true, Text = "TinyMT 决定遭遇；主 MT 决定性格、个体值和 PID。" };
        private int tinyWildPreviousSource;
        private WildPrediction activeWildPrediction;
        private bool tinyWildUpdating;
        private bool UseTinyFinderWild => Ver < 4 && Method == 2 && tinyWildEnabled.Checked;
        private static CheckBox WildCheck(string name, string text) => new CheckBox { Name = name, Text = text, AutoSize = true, Margin = new Padding(4, 5, 4, 0) };
        private static NumericUpDown WildNumber(string name, int min, int max, int value) => new NumericUpDown { Name = name, Minimum = min, Maximum = max, Value = value, Width = max > 999 ? 66 : 53 };
        private void InitializeTinyFinderWild()
        {
            InitializeTinyFinderWildLayoutControls();
            tinyWildSource.Items.AddRange(new[] { "PokeReader 种子", "四段状态 (3→0)", "时间线状态" });
            tinyWildSource.SelectedIndex = 0;
            tinyWildEnabled.CheckedChanged += (s, e) => {
                if (tinyWildUpdating) return;
                tinyWildUpdating = true;
                try
                {
                    if (tinyWildEnabled.Checked) CreateTimeline.Checked = false;
                    if (!tinyWildEnabled.Checked && ea is TinyFinderArea6) ea = null;
                    if (!Initializing && Method == 2) RefreshLocation();
                    UpdateTinyFinderWildControls();
                }
                finally { tinyWildUpdating = false; }
            };
            tinyWildSource.SelectedIndexChanged += (s, e) => {
                int next = tinyWildSource.SelectedIndex;
                if (next == 1)
                {
                    string text = tinyWildSeed.Text.Trim();
                    if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
                    if (tinyWildPreviousSource == 0 && text.Length <= 8 && uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint seed))
                        tinyWildSeed.Text = string.Join(",", new TinyMT(seed).status.Reverse().Select(w => w.ToString("X8")));
                    else if (tinyWildPreviousSource == 2 && TTT.HasSeed) tinyWildSeed.Text = string.Join(",", TinySeeds.Reverse().Select(w => w.ToString("X8")));
                }
                else if (next == 0 && tinyWildPreviousSource != 0) tinyWildSeed.Text = "";
                tinyWildSeed.Visible = tinyWildSeed.Enabled = next != 2;
                tinyWildSeed.Width = next == 1 ? 300 : 110;
                tinyWildPreviousSource = next;
                LayoutTinyFinderWild();
                UpdateTinyFinderWildHelp();
            };
            tinyWildEmulator.CheckedChanged += (s, e) => { ApplyTinyFinderLocationDefaults(); UpdateTinyFinderWildControls(); };
            tinyWildMovingHorde.CheckedChanged += (s, e) => UpdateTinyFinderWildControls();
            tinyWildHoney.CheckedChanged += (s, e) => { if (UseTinyFinderWild && !Initializing) RefreshLocation(); UpdateTinyFinderWildControls(); };
            tinyWildActiveNav.CheckedChanged += (s, e) => UpdateTinyFinderWildControls();
            tinyWildExclusive.CheckedChanged += (s, e) => { if (UseTinyFinderWild && IsDexNav && !Initializing) RefreshWildSpecies(); };
            CreateTimeline.CheckedChanged += (s, e) => { if (CreateTimeline.Checked) tinyWildEnabled.Checked = false; };
        }

        private EnctrKey TinyFinderWildMethod
        {
            get
            {
                switch ((FormPM as PKMW6)?.Type)
                {
                    case EncounterType.Horde: return EnctrKey.Horde;
                    case EncounterType.RockSmash: return EnctrKey.RockSmash;
                    case EncounterType.PokeRadar: return EnctrKey.Radar;
                    case EncounterType.FriendSafari: return EnctrKey.FS;
                    case EncounterType.CaveShadow: return EnctrKey.Ambush;
                    case EncounterType.Fishing: return EnctrKey.Fishing;
                    case EncounterType.DexNav: return tinyWildActiveNav.Checked ? EnctrKey.DexNavSrch : EnctrKey.DexNavMov;
                    default: return tinyWildHoney.Checked ? EnctrKey.Honey : EnctrKey.Wild;
                }
            }
        }

        private WildOptions TinyFinderWildOptions => new WildOptions {
            Method = TinyFinderWildMethod, ORAS = IsORAS, NPC = (int)tinyWildNPC.Value, PartySize = (int)tinyWildParty.Value,
            Rate = (int)Special_th.Value, BagAdvances = (int)tinyWildBag.Value, LongBlink = (int)tinyWildBlink.Value,
            InteractFrame = (int)tinyWildInteract.Value, HoneyDelay = (int)tinyWildHoneyDelay.Value, SearchLevel = (int)tinyWildSearchLevel.Value,
            PossibleHorde = tinyWildPossibleHorde.Enabled && tinyWildPossibleHorde.Checked, RadarGrass = tinyWildRadarGrass.Enabled && tinyWildRadarGrass.Checked,
            MovingHorde = IsHorde && tinyWildMovingHorde.Checked,
            BonusMusic = TinyFinderWildMethod == EnctrKey.Radar && tinyWildMusic.Checked, ShinyCharm = ShinyCharm.Checked,
            Exclusive = IsDexNav && tinyWildExclusive.Checked, Chain = (int)ChainLength.Value, SafariSlots = CB_3rdSlotUnlocked.Checked ? 3 : 2,
            FixedSpecies = SlotSpecies.SelectedValue is int ? (int)SlotSpecies.SelectedValue : 0,
            Flute = (Lead)LeadAbility.SelectedIndex == Lead.BlackFlute ? 1 : (Lead)LeadAbility.SelectedIndex == Lead.WhiteFlute ? 2 : 0,
        };

        private uint[] TinyFinderWildState()
        {
            if (tinyWildSource.SelectedIndex == 2)
            {
                if (!TTT.HasSeed) throw new ArgumentException("Enter a nonzero four-word TinyMT state.");
                return TinySeeds;
            }
            string input = tinyWildSeed.Text.Trim();
            if (tinyWildSource.SelectedIndex == 0)
            {
                if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) input = input.Substring(2);
                if (input.Length == 0 || input.Length > 8 || !uint.TryParse(input, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint seed))
                    throw new ArgumentException("Enter a Tiny u32 seed as 1 to 8 hexadecimal digits (optional 0x prefix).");
                return new TinyMT(seed).status;
            }
            var parts = input.Split(new[] { ',', ' ', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var state = new uint[4];
            if (parts.Length != 4) throw new ArgumentException("Enter a nonzero four-word TinyMT state.");
            for (int i = 0; i < 4; i++)
                if (parts[i].Length > 8 || !uint.TryParse(parts[i], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out state[3 - i]))
                    throw new ArgumentException("Enter a nonzero four-word TinyMT state.");
            return state;
        }

        private void UpdateTinyFinderWildControls()
        {
            if (tinyWildSeedRow == null) return;
            bool visible = Ver < 4 && Method == 2;
            var type = (FormPM as PKMW6)?.Type;
            bool supported = visible && type.HasValue && type != EncounterType.Trap;
            tinyWildEnabled.Visible = tinyWildHelp.Visible = supported;
            tinyWildEnabled.Enabled = supported;
            if (visible && !supported) tinyWildEnabled.Checked = false;
            bool active = supported && UseTinyFinderWild;
            tinyWildSeedRow.Visible = tinyWildParameters.Visible = active;
            // The inline seed controls replace the old Tiny Timeline button
            // while this mode is active; restore it for the legacy layout.
            B_OpenTool.Visible = !active && (gen6timeline_available || IsHorde);
            tinyWildDefaults.Enabled = active;
            bool honey = type == EncounterType.Horde && !tinyWildMovingHorde.Checked || type == EncounterType.Normal && tinyWildHoney.Checked;
            bool walking = type == EncounterType.Normal && !honey || type == EncounterType.Horde && tinyWildMovingHorde.Checked;
            var original = TinyFinder.Controls.EncounterType.GetEncounterTypes(IsORAS).FirstOrDefault(t => t.Key == TinyFinderWildMethod);
            bool interact = supported && original != null && original.ShowsTriggerMTFrame;
            bool blink = supported && (honey || interact);
            tinyWildHoney.Visible = active && type == EncounterType.Normal;
            tinyWildMovingHorde.Visible = active && type == EncounterType.Horde;
            tinyWildActiveNav.Visible = tinyWildExclusive.Visible = active && type == EncounterType.DexNav;
            tinyWildMusic.Visible = active && type == EncounterType.PokeRadar;
            tinyWildMusic.Enabled = supported && type == EncounterType.PokeRadar && ChainLength.Value > 0;
            tinyWildEmulator.Visible = active && blink;
            // Hidden automatic values remain effective; unrelated modes cannot use them.
            tinyWildPossibleHorde.Enabled = supported && type == EncounterType.Normal && !honey;
            tinyWildRadarGrass.Enabled = supported && !IsORAS && walking;
            ShowWildParameter(tinyWildParty, supported && original != null && original.ShowsParty);
            ShowWildParameter(tinyWildInteract, interact);
            ShowWildParameter(tinyWildSearchLevel, supported && type == EncounterType.DexNav);
            if (active)
            {
                ChainLength.Maximum = 999;
                L_Rate.Visible = Special_th.Visible = walking || type == EncounterType.Fishing || type == EncounterType.FriendSafari;
                L_ChainLength.Visible = ChainLength.Visible = type == EncounterType.PokeRadar || type == EncounterType.DexNav || type == EncounterType.Fishing;
            }
            LayoutTinyFinderWild();
            UpdateTinyFinderWildHelp();
        }

        private void ApplyTinyFinderLocationDefaults()
        {
            var data = (ea as TinyFinderArea6)?.Data;
            if (data == null) return;
            tinyWildNPC.Value = data.NPC; tinyWildBag.Value = data.BagAdvances;
            tinyWildBlink.Value = tinyWildEmulator.Checked ? data.EmulatorLongBlink : data.LongBlink;
            Special_th.Value = TinyFinderWildMethod == EnctrKey.Fishing ? SuctionCups ? 98 : 49 : data.Rate;
            tinyWildPossibleHorde.Checked = data.PossibleHorde; tinyWildRadarGrass.Checked = data.RadarGrass;
            tinyWildHoneyDelay.Value = tinyWildEmulator.Checked ? IsORAS ? 118 : 114 : data.BagAdvances == 3 ? IsORAS ? 120 : 110 : IsORAS ? 126 : 112;
        }

        private void Search6_TinyFinderWild()
        {
            try
            {
                var options = TinyFinderWildOptions;
                bool radarChain = options.Method == EnctrKey.Radar && options.Chain > 0;
                if ((options.Method == EnctrKey.DexNavSrch || radarChain) && options.FixedSpecies == 0)
                    throw new ArgumentException("请先选择主动 DexNav 搜索或雷达连锁的宝可梦。");
                var lead = (Lead)LeadAbility.SelectedIndex;
                if (lead == Lead.Static || lead == Lead.MagnetPull || lead == Lead.CuteCharmF || lead == Lead.CuteCharmM || lead == Lead.CompoundEyes || lead == Lead.PressureHustleSpirit)
                    throw new ArgumentException("TinyFinder 的遭遇计算未包含此队首特性。请改用原时间线，或选择同步、吸盘、笛子等适用设置。");
                uint[] state = TinyFinderWildState();
                activeWildPrediction = WildIntegration.Predict(state, options, (ea as TinyFinderArea6)?.Data);
                int min = (int)Frame_min.Value, max = (int)Frame_max.Value;
                if (AroundTarget.Checked) { min = Math.Max(0, (int)TargetFrame.Value - 100); max = (int)TargetFrame.Value + 100; }
                var rng = new MersenneTwister(Seed.Value);
                for (int i = 0; i < min; i++) rng.Next();
                getsetting(rng);
                tinyWildSummary.Text = radarChain ? "连锁草丛见搜索器；进草同步使用“假定同步成功”。" : activeWildPrediction.Triggered ? "槽位 " + activeWildPrediction.Slot + "；同步" + (activeWildPrediction.Synchronized ? "成功" : "失败") +
                    (string.IsNullOrEmpty(activeWildPrediction.EggMove) ? "" : "；遗传招式 " + activeWildPrediction.EggMove) : "当前 TinyMT 状态未触发遭遇。";
                if (!activeWildPrediction.Triggered) return;
                if (IsHorde && options.FixedSpecies != 0 && activeWildPrediction.Species[0] != options.FixedSpecies && !IgnoreFilters.Checked) return;
                for (int i = min; i <= max; i++, RNGPool.AddNext(rng))
                {
                    var rows = activeWildPrediction.IsHorde ? RNGPool.GenerateHorde6() : new[] { RNGPool.Generate6() };
                    foreach (var result in rows)
                        if (filter.CheckResult(result)) Frames.Add(new Frame(result, frame: i, time: i - min) { _tinystate = new PRNGState(state) });
                    if (Frames.Count >= MAX_RESULTS_NUM) break;
                }
            }
            catch (ArgumentException ex) { FormUtil.Error(ex.Message); }
            finally { activeWildPrediction = null; }
        }

        private Wild6 GetTinyFinderWildGenerator()
        {
            var p = activeWildPrediction;
            bool radarChain = TinyFinderWildMethod == EnctrKey.Radar && ChainLength.Value > 0;
            var result = new Wild6 {
                // A chained Radar row describes generated patches, not the later entry encounter.
                // Keep the host's manual entry sync/shiny settings for that encounter.
                IndexedEncounter = radarChain ? null : p, Wildtype = (FormPM as PKMW6)?.Type ?? EncounterType.Normal, IsORAS = IsORAS,
                SpecForm = new[] { 0 }.Concat(p.Species).ToArray(), SlotLevel = new[] { (byte)0 }.Concat(p.Levels).ToArray(),
                Synchro_Stat = (byte)(SyncNature.SelectedIndex - 1), TSV = (int)TSV.Value, TRV = (byte)TRV.Value,
                ShinyCharm = ShinyCharm.Checked, HA = TinyFinderWildMethod == EnctrKey.FS && CB_HAUnlocked.Checked,
                DexNavHA = IsDexNav && p.HiddenAbility > 0, IsForcedShiny6 = IsDexNav ? p.Shiny : radarChain && CB_ForcedShiny.Checked,
                _ivcnt = IsDexNav ? p.Potential : TinyFinderWildMethod == EnctrKey.FS ? 2 : TinyFinderWildMethod == EnctrKey.Radar ? Math.Min(3, (int)ChainLength.Value / 20) : -1,
                _PIDroll_count = TinyFinderWildMethod == EnctrKey.FS ? 4 : TinyFinderWildMethod == EnctrKey.Fishing ? Math.Min(40, 2 * (int)ChainLength.Value) : 0,
                BlankGenderRatio = GenderRatio.SelectedValue is int ? (int)GenderRatio.SelectedValue : 0x7F,
                IsShinyLocked = TinyFinderWildMethod == EnctrKey.Radar && ChainLength.Value > 0 && !CB_ForcedShiny.Checked,
            };
            result.Markslots(); return result;
        }
    }
}
