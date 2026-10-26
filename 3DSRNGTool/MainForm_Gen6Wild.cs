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
        private readonly CheckBox tinyWildManual = WildCheck("TinyFinderManualCalibration", "手动调整地点参数");
        private readonly Label tinyWildSummary = new Label { Name = "TinyFinderWildSummary", AutoSize = true, Text = "TinyMT 决定遭遇；主 MT 决定性格、个体值和 PID。" };
        private Form tinyWildOptionsWindow;
        private Button tinyWildOptionsButton;
        private FlowLayoutPanel tinyWildParameters, tinyWildFlags, tinyWildCalibration;
        private int tinyWildPreviousSource;
        private WildPrediction activeWildPrediction;
        private bool UseTinyFinderWild => Ver < 4 && Method == 2 && tinyWildEnabled.Checked;
        private static CheckBox WildCheck(string name, string text) => new CheckBox { Name = name, Text = text, AutoSize = true, Margin = new Padding(4, 5, 4, 0) };
        private static NumericUpDown WildNumber(string name, int min, int max, int value) => new NumericUpDown { Name = name, Minimum = min, Maximum = max, Value = value, Width = max > 999 ? 66 : 53 };
        private static Label WildLabel(string text) => new Label { Text = text, AutoSize = true, Margin = new Padding(4, 6, 2, 0) };
        private static Control WildParameter(string text, NumericUpDown input)
        {
            var pair = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false, Margin = new Padding(0, 0, 16, 4) };
            input.Width = 72;
            var label = WildLabel(text);
            label.Name = input.Name + "Label";
            pair.Controls.Add(label);
            pair.Controls.Add(input);
            return pair;
        }

        private static void ShowWildParameter(NumericUpDown input, bool visible)
        {
            input.Parent.Visible = visible;
            input.Enabled = visible;
        }

        private void InitializeTinyFinderWild()
        {
            // Keep the original three settings groups and full-height results table.
            // Extra TinyMT parameters live in an owned window, never in the tab's layout.
            tinyWildOptionsButton = new Button { Name = "TinyFinderWildOptionsButton", Text = "TinyMT 遭遇设置…",
                Location = new Point(L_Lead.Left, B_OpenTool.Top),
                Size = new Size(AssumeSynced.Left - L_Lead.Left - 8, B_OpenTool.Height), Visible = false };
            tinyWildOptionsButton.Click += ShowTinyFinderWildOptions;
            Wild_Setting.Controls.Add(tinyWildOptionsButton);

            tinyWildOptionsWindow = new Form { Name = "TinyFinderWildOptions", Text = "TinyMT 遭遇设置 · G6",
                Font = Font, AutoScaleMode = AutoScaleMode.Font, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, Padding = new Padding(12), Owner = this };
            components.Add(tinyWildOptionsWindow);
            tinyWildOptionsWindow.FormClosing += (s, e) => {
                if (e.CloseReason != CloseReason.UserClosing) return;
                e.Cancel = true;
                tinyWildOptionsWindow.Hide();
            };
            var rows = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(0) };
            Func<Control[], FlowLayoutPanel> row = controls => {
                var panel = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
                panel.Controls.AddRange(controls); rows.Controls.Add(panel); return panel;
            };
            tinyWildSource.Items.AddRange(new[] { "PokeReader 种子", "四段状态 (3→0)", "时间线状态" }); tinyWildSource.SelectedIndex = 0;
            var searcher = new Button { Name = "TinyFinderSearcher", Text = "遭遇搜索器…", AutoSize = true };
            searcher.Click += (s, e) => ShowTinyFinder();
            row(new Control[] { tinyWildEnabled, searcher });
            tinyWildSeed.Width = 110;
            row(new Control[] { tinyWildSource, tinyWildSeed });
            tinyWildParameters = row(new[] { WildParameter("队伍数", tinyWildParty),
                WildParameter("交互 MT 帧", tinyWildInteract), WildParameter("搜索等级", tinyWildSearchLevel) });
            tinyWildFlags = row(new Control[] { tinyWildHoney, tinyWildMovingHorde, tinyWildActiveNav,
                tinyWildExclusive, tinyWildMusic, tinyWildEmulator });
            row(new Control[] { tinyWildManual });
            tinyWildCalibration = row(new[] { WildParameter("NPC 调用", tinyWildNPC), WildParameter("背包调用", tinyWildBag),
                WildParameter("长眨眼帧", tinyWildBlink), WildParameter("蜂蜜延迟", tinyWildHoneyDelay), tinyWildPossibleHorde, tinyWildRadarGrass });
            foreach (var panel in new[] { tinyWildParameters, tinyWildFlags, tinyWildCalibration })
            {
                panel.WrapContents = true;
                panel.MaximumSize = new Size(470, 0);
            }
            tinyWildSummary.MaximumSize = new Size(470, 0);
            row(new Control[] { tinyWildSummary });
            row(new Control[] { WildLabel("设置即时生效；关闭此窗口后，在主界面点击“计算”。") });
            var close = new Button { Name = "CloseTinyFinderWildOptions", Text = "关闭", AutoSize = true };
            close.Click += (s, e) => tinyWildOptionsWindow.Hide();
            tinyWildOptionsWindow.CancelButton = close;
            row(new Control[] { close });
            tinyWildOptionsWindow.Controls.Add(rows);
            tinyWildEnabled.CheckedChanged += (s, e) => {
                if (tinyWildEnabled.Checked) CreateTimeline.Checked = false;
                if (!Initializing && Method == 2) RefreshLocation();
                UpdateTinyFinderWildControls();
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
            };
            tinyWildManual.CheckedChanged += (s, e) => UpdateTinyFinderWildControls();
            tinyWildEmulator.CheckedChanged += (s, e) => { ApplyTinyFinderLocationDefaults(); UpdateTinyFinderWildControls(); };
            tinyWildMovingHorde.CheckedChanged += (s, e) => UpdateTinyFinderWildControls();
            tinyWildHoney.CheckedChanged += (s, e) => { if (UseTinyFinderWild && !Initializing) RefreshLocation(); UpdateTinyFinderWildControls(); };
            tinyWildActiveNav.CheckedChanged += (s, e) => UpdateTinyFinderWildControls();
            tinyWildExclusive.CheckedChanged += (s, e) => { if (UseTinyFinderWild && IsDexNav && !Initializing) RefreshWildSpecies(); };
            CreateTimeline.CheckedChanged += (s, e) => { if (CreateTimeline.Checked) tinyWildEnabled.Checked = false; };
        }

        private void ShowTinyFinderWildOptions(object sender, EventArgs e)
        {
            UpdateTinyFinderWildControls();
            if (!tinyWildEnabled.Enabled) return;
            if (!tinyWildOptionsWindow.Visible) tinyWildOptionsWindow.Show(this);
            tinyWildOptionsWindow.Activate();
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
            if (tinyWildOptionsWindow == null) return;
            bool visible = Ver < 4 && Method == 2;
            tinyWildOptionsButton.Text = UseTinyFinderWild ? "TinyMT 设置（已启用）" : "TinyMT 遭遇设置…";
            var type = (FormPM as PKMW6)?.Type;
            bool supported = visible && type.HasValue && type != EncounterType.Trap;
            tinyWildOptionsButton.Visible = supported;
            if (!supported) tinyWildOptionsWindow.Hide();
            tinyWildEnabled.Enabled = supported;
            if (visible && !supported) tinyWildEnabled.Checked = false;
            tinyWildParameters.Enabled = tinyWildFlags.Enabled = tinyWildCalibration.Enabled = UseTinyFinderWild;
            tinyWildManual.Enabled = UseTinyFinderWild;
            tinyWildHoney.Visible = tinyWildHoney.Enabled = supported && type == EncounterType.Normal;
            tinyWildMovingHorde.Visible = tinyWildMovingHorde.Enabled = supported && type == EncounterType.Horde;
            bool honey = type == EncounterType.Horde && !tinyWildMovingHorde.Checked || type == EncounterType.Normal && tinyWildHoney.Checked;
            bool walking = type == EncounterType.Normal && !honey || type == EncounterType.Horde && tinyWildMovingHorde.Checked;
            var original = TinyFinder.Controls.EncounterType.GetEncounterTypes(IsORAS).FirstOrDefault(t => t.Key == TinyFinderWildMethod);
            bool interact = supported && original != null && original.ShowsTriggerMTFrame;
            bool blink = supported && (honey || interact);
            bool calibration = supported && (walking || blink || type == EncounterType.DexNav);
            tinyWildManual.Visible = calibration;
            tinyWildCalibration.Visible = calibration && tinyWildManual.Checked;
            tinyWildPossibleHorde.Visible = tinyWildPossibleHorde.Enabled = supported && type == EncounterType.Normal && !honey;
            tinyWildRadarGrass.Visible = tinyWildRadarGrass.Enabled = supported && !IsORAS && walking;
            tinyWildActiveNav.Visible = tinyWildActiveNav.Enabled = supported && type == EncounterType.DexNav;
            tinyWildExclusive.Visible = tinyWildExclusive.Enabled = supported && type == EncounterType.DexNav;
            tinyWildMusic.Visible = supported && type == EncounterType.PokeRadar;
            tinyWildMusic.Enabled = supported && type == EncounterType.PokeRadar && ChainLength.Value > 0;
            tinyWildEmulator.Visible = tinyWildEmulator.Enabled = blink;
            ShowWildParameter(tinyWildParty, supported && original != null && original.ShowsParty);
            ShowWildParameter(tinyWildInteract, interact);
            ShowWildParameter(tinyWildSearchLevel, supported && type == EncounterType.DexNav);
            ShowWildParameter(tinyWildNPC, supported && (walking || type == EncounterType.Fishing || type == EncounterType.RockSmash || type == EncounterType.DexNav));
            ShowWildParameter(tinyWildBag, supported && honey);
            ShowWildParameter(tinyWildHoneyDelay, supported && honey);
            ShowWildParameter(tinyWildBlink, blink);
            if (UseTinyFinderWild)
            {
                ChainLength.Maximum = 999;
                L_Rate.Visible = Special_th.Visible = walking || type == EncounterType.Fishing || type == EncounterType.FriendSafari;
                L_ChainLength.Visible = ChainLength.Visible = type == EncounterType.PokeRadar || type == EncounterType.DexNav || type == EncounterType.Fishing;
            }
            // DexNav has both potential and chain; they must not occupy the same row.
            ChainLength.Location = new Point(Special_th.Left, IsDexNav ? Special_th.Top : Potential.Top);
            L_ChainLength.Location = new Point(L_Potential.Left, IsDexNav ? L_Rate.Top : L_Potential.Top);
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
