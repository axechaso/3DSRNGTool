using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Pk3DSRNGTool.Controls;
using Pk3DSRNGTool.RNG;

namespace Pk3DSRNGTool
{
    public sealed class TinyIndexTool : Form
    {
        private readonly MainForm main;
        private readonly HexMaskedTextBox[] words = Enumerable.Range(0, 4).Select(i => new HexMaskedTextBox { Width = 85 }).ToArray();
        private readonly HexMaskedTextBox seed = new HexMaskedTextBox { Width = 85 };
        private readonly ComboBox source = Combo("四段状态 / State", "初始种子 / Seed", "模拟器 RTC / UTC");
        private readonly DateTimePicker rtc = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", Width = 175, Value = new DateTime(2026, 1, 1) };
        private readonly NumericUpDown boot = Number(0, 100, 0);
        private readonly ComboBox method = Combo("普通野生 / Wild", "朋友原野 / Friend Safari", "蜂蜜群战 / Horde", "走路群战 / Step Horde",
            "蜂蜜单体 / Honey", "钓鱼 / Fishing", "碎岩 / Rock Smash", "宝可追踪 / Radar", "洞穴落影 / Ambush", "训练家 ID / ID", "DexNav 走路", "DexNav 主动搜索");
        private readonly CheckBox oras = Check("OR / AS"), surf = Check("冲浪 / Surf"), possibleHorde = Check("可能群战"), radarGrass = Check("XY 高草额外调用");
        private readonly NumericUpDown min = Number(0, 10000000, 0), max = Number(0, 10000000, 10000), limit = Number(1, 100000, 10000);
        private readonly NumericUpDown party = Number(1, 6, 6), safari = Number(2, 3, 3), rate = Number(0, 100, 30), npc = Number(0, 100, 0);
        private readonly NumericUpDown bag = Number(0, 100, 15), honeyDelay = Number(0, 10000, 120), blink = Number(0, 10000, 48), interact = Number(0, 10000, 60);
        private readonly NumericUpDown chain = Number(0, 999, 0), level = Number(0, 999, 0), slot = Number(0, 12, 0), potential = Number(0, 3, 0);
        private readonly NumericUpDown tid = Number(0, 65535, 0), tsv = Number(0, 4095, 0);
        private readonly CheckBox music = Check("奖励音乐"), charm = Check("闪耀护符"), eyes = Check("复眼"), exclusive = Check("DexNav 限定种");
        private readonly ComboBox sync = Combo("同步不限", "同步成功", "同步失败");
        private readonly CheckBox triggered = Check("仅成功遭遇", true), ha = Check("仅隐藏特性"), shiny = Check("DexNav 强闪 / 雷达闪草"), useTID = Check("TID"), useTSV = Check("TSV");
        private readonly Button search = new Button { Text = "搜索 / Search", AutoSize = true }, cancel = new Button { Text = "停止 / Stop", AutoSize = true, Enabled = false };
        private readonly Label status = new Label { AutoSize = true, Padding = new Padding(4), Text = "输入状态或种子，再选择遭遇方式。" };
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false, ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText };
        private readonly FlowLayoutPanel inputs = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        private CancellationTokenSource cancellation;

        public TinyIndexTool(MainForm owner)
        {
            main = owner;
            Text = "G6 TinyMT 索引搜索 · TinyFinder";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1120, 730); MinimumSize = new Size(950, 650);
            Font = new Font("Microsoft YaHei UI", 9);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 330));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout); layout.Controls.Add(inputs, 0, 0); layout.Controls.Add(grid, 0, 1);
            Row(source, Label("状态顺序 3 → 0"), words[3], words[2], words[1], words[0], Button("读取原工具", ReadCurrent));
            Row(Label("32 位种子"), seed, Label("RTC (UTC)"), rtc, Label("初始偏移"), boot, Label("RTC 模式通常为 1；ID 为 2"));
            Row(Label("方式"), method, oras, surf, possibleHorde, radarGrass);
            Row(Label("索引起点"), min, Label("终点"), max, Label("最多结果"), limit, Label("队伍数"), party, Label("FS 槽位数"), safari);
            Row(Label("遭遇率 %"), rate, Label("NPC 调用数"), npc, Label("背包调用数"), bag, Label("蜂蜜延迟"), honeyDelay);
            Row(Label("长眨眼帧"), blink, Label("交互 MT 帧"), interact, Label("连锁数"), chain, Label("搜索等级"), level);
            Row(music, charm, eyes, exclusive, sync, Label("槽位 (0=不限)"), slot);
            Row(triggered, ha, shiny, Label("最低潜力"), potential, useTID, tid, useTSV, tsv);
            Row(search, cancel, Button("复制选中状态", CopyState), Button("回填 TinyMT 状态", ApplyState));
            layout.Controls.Add(status, 0, 2);
            layout.Controls.Add(Label("索引从输入状态起算，0 为下一次调用前；索引不等于主 MT 帧。回填后使用时间线须重新校准。背包、NPC 和眨眼参数须按游戏场景设置。"), 0, 3);
            Column("Index", "索引", 65); Column("Synchronized", "同步", 50); Column("Triggered", "遭遇", 50); Column("Slot", "槽位", 50);
            Column("HiddenAbility", "HA 位置", 65); Column("Potential", "潜力", 50); Column("EggMove", "遗传招式", 65); Column("SpecialShiny", "强闪/闪草", 75);
            Column("ItemText", "携带物率", 100); Column("FluteText", "笛子等级", 75); Column("Delay", "延迟", 50);
            Column("TID", "TID", 60); Column("SID", "SID", 60); Column("TSV", "TSV", 55); Column("TRV", "TRV", 45);
            Column("RandHex", "随机数", 85); Column("Status", "TinyMT (3 → 0)", 295); Column("Details", "详情", 400);
            search.Click += Search;
            cancel.Click += (s, e) => cancellation?.Cancel();
            oras.CheckedChanged += (s, e) => { bag.Value = oras.Checked ? 15 : 27; honeyDelay.Value = oras.Checked ? 120 : 110; };
            source.SelectedIndexChanged += (s, e) => UpdateSource();
            method.SelectedIndexChanged += (s, e) =>
            {
                var selected = (TinyIndexMethod)method.SelectedIndex;
                if (selected == TinyIndexMethod.FriendSafari) rate.Value = 13;
                if (selected == TinyIndexMethod.Fishing) rate.Value = 98;
                UpdateColumns();
            };
            chain.ValueChanged += (s, e) => UpdateColumns();
            FormClosing += (s, e) => { if (cancellation != null) { cancellation.Cancel(); e.Cancel = true; } };
            oras.Checked = main.IsORAS;
            if (!oras.Checked) { bag.Value = 27; honeyDelay.Value = 110; }
            ReadCurrent(null, EventArgs.Empty); UpdateSource(); UpdateColumns();
        }

        private static NumericUpDown Number(int min, int max, int value) => new NumericUpDown { Minimum = min, Maximum = max, Value = value, Width = max >= 100000 ? 95 : 65 };
        private static Label Label(string text) => new Label { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 0) };
        private static CheckBox Check(string text, bool value = false) => new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(3, 6, 3, 0) };
        private static ComboBox Combo(params string[] names)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            box.Items.AddRange(names); box.SelectedIndex = 0; return box;
        }
        private static Button Button(string text, EventHandler handler)
        {
            var button = new Button { Text = text, AutoSize = true }; button.Click += handler; return button;
        }
        private void Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            row.Controls.AddRange(controls); inputs.Controls.Add(row);
        }
        private void Column(string property, string title, int width) => grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = property, Name = property, HeaderText = title, Width = width });

        private void UpdateSource()
        {
            foreach (var word in words) word.Enabled = source.SelectedIndex == 0;
            seed.Enabled = source.SelectedIndex == 1; rtc.Enabled = source.SelectedIndex == 2;
            boot.Enabled = source.SelectedIndex != 0;
        }
        private void UpdateColumns()
        {
            var m = (TinyIndexMethod)method.SelectedIndex;
            bool id = m == TinyIndexMethod.ID;
            bool dex = m == TinyIndexMethod.DexNavStep || m == TinyIndexMethod.DexNavSearch;
            bool radarChain = m == TinyIndexMethod.Radar && chain.Value > 0;
            bool honey = m == TinyIndexMethod.Honey || m == TinyIndexMethod.HordeHoney;
            foreach (string name in new[] { "TID", "SID", "TSV", "TRV" }) grid.Columns[name].Visible = id;
            grid.Columns["Synchronized"].Visible = !id && !radarChain;
            grid.Columns["Slot"].Visible = !id && !radarChain && m != TinyIndexMethod.DexNavSearch;
            useTID.Enabled = useTSV.Enabled = tid.Enabled = tsv.Enabled = id;
            sync.Enabled = !id && !radarChain;
            slot.Enabled = !id && !radarChain && m != TinyIndexMethod.DexNavSearch;
            triggered.Enabled = !id && !honey && m != TinyIndexMethod.Radar && m != TinyIndexMethod.DexNavSearch && m != TinyIndexMethod.Ambush;
            party.Enabled = honey || m == TinyIndexMethod.Fishing || m == TinyIndexMethod.RockSmash || m == TinyIndexMethod.Radar;
            safari.Enabled = m == TinyIndexMethod.FriendSafari;
            rate.Enabled = m == TinyIndexMethod.Wild || m == TinyIndexMethod.FriendSafari || m == TinyIndexMethod.HordeStep || m == TinyIndexMethod.Fishing;
            npc.Enabled = rate.Enabled && m != TinyIndexMethod.FriendSafari || m == TinyIndexMethod.RockSmash || m == TinyIndexMethod.DexNavStep;
            bag.Enabled = honeyDelay.Enabled = honey;
            blink.Enabled = honey || m == TinyIndexMethod.Fishing || m == TinyIndexMethod.RockSmash || m == TinyIndexMethod.DexNavSearch;
            interact.Enabled = blink.Enabled && !honey;
            chain.Enabled = dex || m == TinyIndexMethod.Radar;
            level.Enabled = charm.Enabled = eyes.Enabled = exclusive.Enabled = potential.Enabled = dex;
            ha.Enabled = dex || m == TinyIndexMethod.HordeHoney || m == TinyIndexMethod.HordeStep;
            shiny.Enabled = dex || m == TinyIndexMethod.Radar;
            music.Enabled = m == TinyIndexMethod.Radar;
            possibleHorde.Enabled = m == TinyIndexMethod.Wild;
            radarGrass.Enabled = m == TinyIndexMethod.Wild || m == TinyIndexMethod.HordeStep;
            surf.Enabled = m == TinyIndexMethod.Wild || m == TinyIndexMethod.Honey || dex;
        }
        private void ReadCurrent(object sender, EventArgs e)
        {
            uint[] state = main.TinyIndexInputState;
            for (int i = 0; i < 4; i++) words[i].Value = state[i];
            source.SelectedIndex = 0;
        }
        private uint[] InputState()
        {
            if (source.SelectedIndex == 0) return words.Select(w => w.Value).ToArray();
            uint value = source.SelectedIndex == 1 ? seed.Value : TinyIndexSearch.SeedFromRTC(rtc.Value);
            var rng = new TinyMT(value);
            for (int i = 0; i < boot.Value; i++) rng.Next();
            return (uint[])rng.status.Clone();
        }
        private TinyIndexResult Selected => grid.CurrentRow?.DataBoundItem as TinyIndexResult;
        private void SetSearching(bool busy)
        {
            foreach (Control row in inputs.Controls)
                foreach (Control control in row.Controls) control.Enabled = !busy || control == cancel;
            search.Enabled = !busy; cancel.Enabled = busy;
            if (!busy) { UpdateSource(); UpdateColumns(); }
        }
        private void CopyState(object sender, EventArgs e) { if (Selected != null) Clipboard.SetText(Selected.Status); }
        private void ApplyState(object sender, EventArgs e)
        {
            if (Selected == null) return;
            main.ApplyTinyIndexState(Selected);
            status.Text = $"已回填索引 {Selected.Index} 的 TinyMT 状态。使用时间线前请重新校准。";
        }

        private async void Search(object sender, EventArgs e)
        {
            var settings = new TinyIndexSettings {
                Method = (TinyIndexMethod)method.SelectedIndex, IsORAS = oras.Checked, Surf = surf.Enabled && surf.Checked,
                PossibleHorde = possibleHorde.Enabled && possibleHorde.Checked, RadarGrass = radarGrass.Enabled && radarGrass.Checked, PartySize = (int)party.Value,
                SafariSlots = (int)safari.Value, EncounterRate = (int)rate.Value, NPCAdvances = (int)npc.Value,
                BagAdvances = (int)bag.Value, HoneyDelay = (int)honeyDelay.Value, LongBlinkFrame = (int)blink.Value,
                InteractFrame = (int)interact.Value, ChainLength = (int)chain.Value, SearchLevel = (int)level.Value,
                BonusMusic = music.Checked, ShinyCharm = charm.Checked, CompoundEyes = eyes.Checked, DexNavExclusive = exclusive.Checked,
            };
            bool id = settings.Method == TinyIndexMethod.ID;
            var filter = new TinyIndexFilter {
                Sync = !sync.Enabled || sync.SelectedIndex == 0 ? (bool?)null : sync.SelectedIndex == 1,
                Slot = slot.Enabled ? (int)slot.Value : 0, TriggerOnly = triggered.Enabled && triggered.Checked,
                HiddenAbilityOnly = ha.Enabled && ha.Checked, ShinyOnly = shiny.Enabled && shiny.Checked,
                MinimumPotential = potential.Enabled ? (int)potential.Value : 0,
                TID = id && useTID.Checked ? (int?)tid.Value : null, TSV = id && useTSV.Checked ? (int?)tsv.Value : null,
            };
            try
            {
                var state = InputState(); int first = (int)min.Value, last = (int)max.Value, count = (int)limit.Value;
                cancellation = new CancellationTokenSource(); var token = cancellation.Token;
                SetSearching(true);
                status.Text = "搜索中…";
                var progress = new Progress<int>(p => status.Text = $"搜索中… {p}%");
                List<TinyIndexResult> rows = await Task.Run(() => TinyIndexSearch.Search(state, first, last, settings, filter, count, token, progress));
                grid.DataSource = rows;
                status.Text = $"找到 {rows.Count} 条结果" + (rows.Count == count ? "，已达到结果上限。" : "。");
            }
            catch (OperationCanceledException) { status.Text = "搜索已停止。"; }
            catch (ArgumentException ex) { status.Text = ex.Message; }
            finally { cancellation?.Dispose(); cancellation = null; SetSearching(false); }
        }
    }
}
