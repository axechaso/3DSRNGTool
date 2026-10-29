using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Pk3DSRNGTool
{
    public partial class MainForm
    {
        private FlowLayoutPanel tinyWildSeedRow, tinyWildParameters;
        private Panel tinyWildDefaults;
        private LinkLabel tinyWildHelp;
        private ToolTip tinyWildTips;
        private Dictionary<Control, Rectangle> tinyWildOriginalBounds;
        private Dictionary<Control, bool> tinyWildOriginalAutoSize;
        private Dictionary<Control, AnchorStyles> tinyWildOriginalAnchors;
        private bool tinyWildCompactLayout;

        private Control WildParameter(string text, NumericUpDown input)
        {
            var pair = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false, Margin = new Padding(0) };
            input.Width = 52;
            input.Margin = new Padding(3, 0, 8, 0);
            var label = new Label { Name = input.Name + "Label", Text = text, AutoSize = true, Margin = new Padding(0, 3, 3, 0) };
            pair.Controls.Add(label);
            pair.Controls.Add(input);
            return pair;
        }

        private static void ShowWildParameter(NumericUpDown input, bool visible)
        {
            input.Parent.Visible = visible;
            input.Enabled = visible;
        }

        private void InitializeTinyFinderWildLayoutControls()
        {
            tinyWildEnabled.Visible = false;
            tinyWildEnabled.Text = "TinyMT 遭遇";
            tinyWildHelp = new LinkLabel { Name = "TinyFinderHelp", Text = "?", AutoSize = true, Visible = false, TabStop = true };
            tinyWildTips = new ToolTip(components) { AutoPopDelay = 15000, InitialDelay = 400, ReshowDelay = 100, ShowAlways = true };
            tinyWildHelp.LinkClicked += (s, e) => tinyWildTips.Show(TinyFinderWildHelpText(), tinyWildHelp, 0, tinyWildHelp.Height + 4, 15000);
            tinyWildSeedRow = new FlowLayoutPanel { Name = "TinyFinderSeedRow", WrapContents = false, Visible = false, Margin = new Padding(0) };
            tinyWildSource.Margin = tinyWildSeed.Margin = new Padding(0, 0, 8, 0);
            tinyWildSeed.Font = Seed.Font;
            tinyWildSeedRow.Controls.AddRange(new Control[] { tinyWildSource, tinyWildSeed });
            tinyWildParameters = new FlowLayoutPanel { Name = "TinyFinderParameters", WrapContents = false, Visible = false, Margin = new Padding(0) };
            tinyWildParameters.Controls.AddRange(new[] { WildParameter("队伍数", tinyWildParty),
                WildParameter("交互帧", tinyWildInteract), WildParameter("搜索等级", tinyWildSearchLevel) });

            // These values come from the selected location or a TinyFinder result.
            // Keep them out of the settings surface, as in the original TinyFinder.
            tinyWildDefaults = new Panel { Name = "TinyFinderLocationDefaults", Visible = false };
            tinyWildDefaults.Controls.AddRange(new Control[] { tinyWildNPC, tinyWildBag, tinyWildBlink, tinyWildHoneyDelay,
                tinyWildPossibleHorde, tinyWildRadarGrass, tinyWildSummary });
            Wild_Setting.Controls.AddRange(new Control[] { tinyWildEnabled, tinyWildHelp, tinyWildSeedRow,
                tinyWildParameters, tinyWildDefaults, tinyWildHoney, tinyWildMovingHorde, tinyWildActiveNav,
                tinyWildExclusive, tinyWildMusic, tinyWildEmulator });
            foreach (var flag in new[] { tinyWildHoney, tinyWildMovingHorde, tinyWildActiveNav, tinyWildExclusive, tinyWildMusic, tinyWildEmulator }) flag.Visible = false;
            tinyWildSummary.TextChanged += (s, e) => UpdateTinyFinderWildHelp();
            L_HordeInfo.TextChanged += (s, e) => UpdateTinyFinderWildHelp();
            tinyWildTips.SetToolTip(tinyWildParty, "当前队伍中的宝可梦数量。");
            tinyWildTips.SetToolTip(tinyWildInteract, "执行钓鱼、碎岩或主动 DexNav 时的主 MT 交互帧。");
            tinyWildTips.SetToolTip(tinyWildSearchLevel, "当前宝可梦的 DexNav 搜索等级。");
            tinyWildTips.SetToolTip(tinyWildHoney, "普通野生使用蜂蜜单体遭遇；关闭时按走路遭遇计算。");
            tinyWildTips.SetToolTip(tinyWildMovingHorde, "勾选为走路群战；未勾选为蜂蜜群战。");
            tinyWildTips.SetToolTip(tinyWildActiveNav, "勾选为主动 DexNav 搜索；未勾选为走路触发。");
            tinyWildTips.SetToolTip(tinyWildExclusive, "将 DexNav 限定种加入遭遇选项。");
            tinyWildTips.SetToolTip(tinyWildEmulator, "使用模拟器对应的眨眼与蜂蜜延迟参数。");
            tinyWildTips.SetToolTip(tinyWildSource, "选择 PokeReader 的 Tiny u32 种子、四段状态，或时间线中的状态。");
        }

        private void CaptureTinyFinderWildLayout()
        {
            if (tinyWildOriginalBounds != null) return;
            var controls = new Control[] { EnctrPanel, MetLocation, L_Location, SlotSpecies, L_Slots,
                LeadAbility, L_Lead, Special_th, L_Rate, Potential, L_Potential, ChainLength, L_ChainLength,
                HA_MainSlot, L_HA, CB_ForcedShiny, CB_NavHA, CB_NavUnown, CB_HAUnlocked, CB_3rdSlotUnlocked,
                AssumeSynced };
            tinyWildOriginalBounds = controls.Concat(EnctrPanel.Controls.Cast<Control>()).ToDictionary(c => c, c => c.Bounds);
            tinyWildOriginalAutoSize = new[] { L_Lead, L_Rate, L_HA, L_Potential, L_ChainLength }
                .ToDictionary(c => (Control)c, c => c.AutoSize);
            tinyWildOriginalAnchors = controls.ToDictionary(c => c, c => c.Anchor);
            UpdateTinyFinderWildControls();
        }

        private void LayoutTinyFinderWild()
        {
            tinyWildHelp.Location = new Point(Wild_Setting.ClientSize.Width - tinyWildHelp.PreferredSize.Width - 8, 0);
            tinyWildEnabled.Location = new Point(tinyWildHelp.Left - tinyWildEnabled.PreferredSize.Width - 10, 0);
            if (tinyWildOriginalBounds == null) return;
            if (!UseTinyFinderWild)
            {
                if (tinyWildCompactLayout)
                {
                    foreach (var pair in tinyWildOriginalBounds) pair.Key.Bounds = pair.Value;
                    foreach (var pair in tinyWildOriginalAutoSize) pair.Key.AutoSize = pair.Value;
                    foreach (var pair in tinyWildOriginalAnchors) pair.Key.Anchor = pair.Value;
                    L_HordeInfo.Visible = IsHorde;
                    tinyWildCompactLayout = false;
                }
                return;
            }
            tinyWildCompactLayout = true;
            Func<int, int> y = value => (int)Math.Round(value * Wild_Setting.ClientSize.Height / 315.0);
            int left = L_Lead.Left, right = Wild_Setting.ClientSize.Width - left;
            // Keep the TinyMT toggle and help link in their own top row. The
            // original encounter panel starts a little lower so its border
            // never collides with those controls when the form is compact.
            EnctrPanel.Top = y(28);
            EnctrPanel.Height = y(76);
            SyncNature.Top = GenderRatio.Top = y(44);
            L_SyncNature.Top = L_GenderRatio.Top = y(48);
            MetLocation.Top = SlotSpecies.Top = y(104);
            L_Location.Top = L_Slots.Top = y(104) + 3;
            tinyWildSeedRow.SetBounds(left, y(136), right - left, y(28));
            int seedWidth = tinyWildSeedRow.ClientSize.Width - tinyWildSource.Width
                - tinyWildSource.Margin.Horizontal - tinyWildSeed.Margin.Horizontal - 4;
            tinyWildSeed.Width = Math.Max(90, seedWidth);
            tinyWildParameters.SetBounds(left, y(168), right - left, y(28));

            var type = (FormPM as PKMW6)?.Type;
            bool walking = type == EncounterType.Normal && !tinyWildHoney.Checked || IsHorde && tinyWildMovingHorde.Checked;
            bool narrow = Wild_Setting.ClientSize.Width < 480;
            int[] columns = narrow ? new[] { left, left + 190 } : new[] { left, left + 190, left + 330 };
            Action<Control, Control, int, int, int> pairAt = (label, input, column, rowTop, labelWidth) => {
                label.AutoSize = false;
                input.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                label.SetBounds(columns[column], rowTop + 3, labelWidth, label.Height);
                input.SetBounds(columns[column] + labelWidth + 4, rowTop, input.Width, input.Height);
            };
            var pairs = new List<Tuple<Control, Control, int>>();
            pairs.Add(Tuple.Create((Control)L_Lead, (Control)LeadAbility, 44));
            if (walking || type == EncounterType.Fishing || type == EncounterType.FriendSafari)
                pairs.Add(Tuple.Create((Control)L_Rate, (Control)Special_th, 60));
            if (IsHorde) pairs.Add(Tuple.Create((Control)L_HA, (Control)HA_MainSlot, 62));
            if (IsDexNav) pairs.Add(Tuple.Create((Control)L_Potential, (Control)Potential, 52));
            if (type == EncounterType.PokeRadar || type == EncounterType.Fishing || type == EncounterType.RockSmash || IsDexNav)
                pairs.Add(Tuple.Create((Control)L_ChainLength, (Control)ChainLength, 70));
            int pairTop = y(200), pairRowHeight = y(28);
            for (int i = 0; i < pairs.Count; i++)
            {
                var pair = pairs[i];
                int column = narrow ? i % 2 : i;
                int row = narrow ? i / 2 : 0;
                pairAt(pair.Item1, pair.Item2, column, pairTop + row * pairRowHeight, pair.Item3);
            }

            var flags = new List<Control>();
            if (IsDexNav) flags.AddRange(new Control[] { CB_ForcedShiny, CB_NavHA, CB_NavUnown });
            else if (type == EncounterType.PokeRadar) flags.Add(CB_ForcedShiny);
            if (type == EncounterType.FriendSafari) flags.AddRange(new Control[] { CB_HAUnlocked, CB_3rdSlotUnlocked });
            if (type == EncounterType.Normal) flags.Add(tinyWildHoney);
            if (IsHorde) flags.Add(tinyWildMovingHorde);
            if (IsDexNav) flags.AddRange(new Control[] { tinyWildActiveNav, tinyWildExclusive });
            if (type == EncounterType.PokeRadar) flags.Add(tinyWildMusic);
            bool honey = IsHorde && !tinyWildMovingHorde.Checked || type == EncounterType.Normal && tinyWildHoney.Checked;
            if (honey || type == EncounterType.Fishing || type == EncounterType.RockSmash || IsDexNav && tinyWildActiveNav.Checked) flags.Add(tinyWildEmulator);
            if (AssumeSynced.Visible) flags.Add(AssumeSynced);
            int x = left;
            int top = pairTop + (narrow ? (int)Math.Ceiling(pairs.Count / 2.0) : 1) * pairRowHeight + 2, rowHeight = 0;
            foreach (var flag in flags)
            {
                int width = flag.PreferredSize.Width;
                if (x > left && x + width > right) { x = left; top += rowHeight + 4; rowHeight = 0; }
                flag.Location = new Point(x, top);
                x += width + 10;
                rowHeight = Math.Max(rowHeight, flag.Height);
            }
            // The original controls remain direct children of Wild_Setting.
            // Bring the compact rows above the legacy controls so a stale
            // z-order cannot paint over the newly placed lead row.
            foreach (var pair in pairs)
            {
                pair.Item1.BringToFront();
                pair.Item2.BringToFront();
            }
            L_HordeInfo.Visible = false;
        }

        private string TinyFinderWildHelpText()
        {
            string text = "启用 TinyMT 遭遇后，在左侧填写种子和当前类型的参数，再点击右侧“计算”。\n"
                + "TinyMT 决定遭遇；主 MT 决定性格、个体值和 PID。\nNPC、背包与眨眼参数随地点自动设置。";
            if (UseTinyFinderWild)
                text += "\n\n" + CB_Category.Text + "：" + tinyWildSummary.Text + "\nNPC 调用：" + tinyWildNPC.Value
                    + "；背包调用：" + tinyWildBag.Value + "；长眨眼帧：" + tinyWildBlink.Value;
            if (IsHorde && !UseTinyFinderWild && !string.IsNullOrWhiteSpace(L_HordeInfo.Text)) text += "\n" + L_HordeInfo.Text.Trim();
            return text;
        }

        private void UpdateTinyFinderWildHelp()
        {
            if (tinyWildTips == null) return;
            tinyWildTips.SetToolTip(tinyWildHelp, TinyFinderWildHelpText());
            tinyWildTips.SetToolTip(tinyWildEnabled, "只显示当前遭遇类型需要的选项。启用后与原 TinyMT 时间线互斥。");
            tinyWildTips.SetToolTip(tinyWildSeed, tinyWildSource.SelectedIndex == 1
                ? "四段十六进制状态，按 3、2、1、0 顺序输入，用逗号或空格分隔。"
                : "PokeReader 的 Tiny u32 种子：1～8 位十六进制，可带 0x 前缀。");
        }
    }
}
