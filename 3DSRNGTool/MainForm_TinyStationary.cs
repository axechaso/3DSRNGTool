using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Pk3DSRNGTool.RNG;

namespace Pk3DSRNGTool
{
    public partial class MainForm
    {
        private readonly CheckBox tinyStationaryEnabled = new CheckBox
        {
            Name = "TinyStationaryEnabled",
            Text = "TinyMT 定点",
            AutoSize = true,
        };
        private readonly LinkLabel tinyStationaryHelp = new LinkLabel
        {
            Name = "TinyStationaryHelp",
            Text = "?",
            AutoSize = true,
            TabStop = true,
        };
        private readonly ComboBox tinyStationarySource = new ComboBox
        {
            Name = "TinyStationarySource",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 126,
        };
        private readonly TextBox tinyStationarySeed = new TextBox
        {
            Name = "TinyStationarySeed",
            Text = "00000000",
            Width = 300,
            CharacterCasing = CharacterCasing.Upper,
        };
        private readonly ComboBox tinyStationaryCallType = new ComboBox
        {
            Name = "TinyStationaryCallType",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 125,
        };
        private readonly NumericUpDown tinyStationaryFrame = new NumericUpDown
        {
            Name = "TinyStationaryFrame",
            Minimum = 0,
            Maximum = FuncUtil.MAXFRAME,
            Value = 0,
            Width = 72,
        };
        private readonly Label tinyStationarySeedLabel = new Label
        {
            Name = "TinyStationarySeedLabel",
            Text = "种子/状态",
            AutoSize = true,
        };
        private readonly Label tinyStationaryCallLabel = new Label
        {
            Name = "TinyStationaryCallLabel",
            Text = "首次调用",
            AutoSize = true,
        };
        private readonly Label tinyStationaryFrameLabel = new Label
        {
            Name = "TinyStationaryFrameLabel",
            Text = "帧",
            AutoSize = true,
        };
        private readonly Panel tinyStationaryPanel = new Panel
        {
            Name = "TinyStationaryPanel",
            BorderStyle = BorderStyle.None,
            Size = new Size(516, 72),
        };
        private ToolTip tinyStationaryTips;
        private int tinyStationaryPreviousSource;
        private bool tinyStationaryUpdating;

        private bool IsTinyStationaryCategory
        {
            get
            {
                if (!Gen6 || Method != 0 || CB_Category.SelectedIndex < 0)
                    return false;
                var categories = Pokemon.getCategoryList(Ver, Method);
                if (CB_Category.SelectedIndex >= categories.Length)
                    return false;
                string text = categories[CB_Category.SelectedIndex].Text;
                // Category names are translated with the rest of the main
                // window, so the native panel must recognize both the source
                // names and the Chinese labels shown by the current UI.
                return text.IndexOf("Berry Tree", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("果树", StringComparison.Ordinal) >= 0
                    || text.IndexOf("Ambush Encounters", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("摇草", StringComparison.Ordinal) >= 0;
            }
        }

        private bool UseTinyStationary => IsTinyStationaryCategory && tinyStationaryEnabled.Checked;

        private void InitializeTinyStationary()
        {
            tinyStationaryTips = new ToolTip(components)
            {
                AutoPopDelay = 15000,
                InitialDelay = 400,
                ReshowDelay = 100,
                ShowAlways = true,
            };
            tinyStationarySource.Items.AddRange(new object[] { "PokeReader 种子", "四段状态 (3→0)" });
            tinyStationarySource.SelectedIndex = 0;
            tinyStationaryCallType.Items.AddRange(new object[] { "眨眼 (+2)", "眨眼 (+1)", "短眨眼", "发呆" });
            tinyStationaryCallType.SelectedIndex = 0;

            tinyStationaryPanel.Location = new Point(8, 126);
            // Keep the same two compact rows as the inline wild controls.
            // The source and state fields use the full row; no extra group
            // box or secondary window is needed here.
            tinyStationarySeedLabel.Visible = false;
            tinyStationarySource.Location = new Point(0, 0);
            tinyStationarySeed.Location = new Point(137, 0);
            tinyStationaryCallLabel.Location = new Point(0, 38);
            tinyStationaryCallType.Location = new Point(65, 33);
            tinyStationaryFrameLabel.Location = new Point(198, 38);
            tinyStationaryFrame.Location = new Point(220, 33);

            tinyStationaryPanel.Controls.AddRange(new Control[]
            {
                tinyStationarySeedLabel, tinyStationarySource, tinyStationarySeed,
                tinyStationaryCallLabel, tinyStationaryCallType, tinyStationaryFrameLabel,
                tinyStationaryFrame,
            });
            Sta_Setting.Controls.AddRange(new Control[] { tinyStationaryPanel, tinyStationaryEnabled, tinyStationaryHelp });

            tinyStationaryEnabled.CheckedChanged += (s, e) =>
            {
                if (tinyStationaryEnabled.Checked)
                    CreateTimeline.Checked = false;
                UpdateTinyStationaryControls();
            };
            tinyStationaryHelp.LinkClicked += (s, e) =>
                tinyStationaryTips.Show(TinyStationaryHelpText(), tinyStationaryHelp, 0, tinyStationaryHelp.Height + 4, 15000);
            tinyStationarySource.SelectedIndexChanged += (s, e) =>
            {
                int next = tinyStationarySource.SelectedIndex;
                if (next == 1)
                {
                    string text = tinyStationarySeed.Text.Trim();
                    if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
                    if (tinyStationaryPreviousSource == 0 && text.Length <= 8 &&
                        uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint seed))
                        tinyStationarySeed.Text = string.Join(",", new TinyMT(seed).status.Reverse().Select(w => w.ToString("X8")));
                }
                else if (next == 0 && tinyStationaryPreviousSource == 1)
                {
                    tinyStationarySeed.Text = "";
                }
                tinyStationarySeed.Width = next == 1 ? 300 : 110;
                tinyStationaryPreviousSource = next;
                UpdateTinyStationaryHelp();
            };
            tinyStationarySeed.TextChanged += (s, e) => UpdateTinyStationaryHelp();
            tinyStationaryCallType.SelectedIndexChanged += (s, e) => UpdateTinyStationaryHelp();

            // These handlers run after the designer's handlers, so the panel
            // follows the final category and species selection.
            CB_Category.SelectedIndexChanged += (s, e) => UpdateTinyStationaryControls();
            Poke.SelectedIndexChanged += (s, e) => UpdateTinyStationaryControls();
            RNGMethod.SelectedIndexChanged += (s, e) => UpdateTinyStationaryControls();
            Gameversion.SelectedIndexChanged += (s, e) => UpdateTinyStationaryControls();
            CreateTimeline.CheckedChanged += (s, e) => UpdateTinyStationaryControls();
            LayoutTinyStationaryHeader();
            UpdateTinyStationaryControls();
        }

        private void LayoutTinyStationaryHeader()
        {
            int width = Sta_Setting.ClientSize.Width;
            if (width <= 0)
                width = Sta_Setting.Width - Sta_Setting.Padding.Horizontal;
            int right = Math.Max(8, width - 8);
            int helpWidth = Math.Max(12, tinyStationaryHelp.PreferredSize.Width);
            int helpLeft = Math.Max(0, right - helpWidth);
            int toggleLeft = Math.Max(0, helpLeft - tinyStationaryEnabled.PreferredSize.Width - 10);
            tinyStationaryEnabled.Location = new Point(toggleLeft, 4);
            tinyStationaryHelp.Location = new Point(helpLeft, 5);
        }

        private string TinyStationaryHelpText()
        {
            string category = IsTinyStationaryCategory ? Pokemon.getCategoryList(Ver, Method)[CB_Category.SelectedIndex].Text : "当前类别";
            return category + "使用主界面内置的 TinyMT 时间线。\n"
                + "PokeReader 选种子；四段状态按 3、2、1、0 输入。\n"
                + "首次调用帧和调用类型用于生成后续 TinyMT 状态，结果中的性格、个体值和 PID 仍由主 MT 计算。";
        }

        private void UpdateTinyStationaryHelp()
        {
            if (tinyStationaryTips == null) return;
            tinyStationaryTips.SetToolTip(tinyStationaryEnabled, "只在果树和摇草类别显示。启用后直接在主窗口计算，不会打开 TinyFinder。\n点击 ? 查看输入说明。");
            tinyStationaryTips.SetToolTip(tinyStationaryHelp, TinyStationaryHelpText());
            tinyStationaryTips.SetToolTip(tinyStationarySource, "种子会自动初始化 TinyMT；状态按 3、2、1、0 输入。\n状态示例：12345678,9ABCDEF0,00000001,00000002");
            tinyStationaryTips.SetToolTip(tinyStationaryCallType, "游戏在当前帧发生的第一种 TinyMT 调用；之后的眨眼/发呆会自动推进。");
            tinyStationaryTips.SetToolTip(tinyStationaryFrame, "第一种 TinyMT 调用发生的帧。通常从 0 开始，若有校准值再改成对应帧。");
        }

        private void UpdateTinyStationaryControls()
        {
            if (tinyStationaryPanel == null || tinyStationaryUpdating)
                return;
            tinyStationaryUpdating = true;
            try
            {
                LayoutTinyStationaryHeader();
                bool relevant = IsTinyStationaryCategory;
                if (!relevant && tinyStationaryEnabled.Checked)
                    tinyStationaryEnabled.Checked = false;
                if (relevant && CreateTimeline.Checked)
                    CreateTimeline.Checked = false;

                tinyStationaryEnabled.Visible = tinyStationaryHelp.Visible = relevant;
                tinyStationaryPanel.Visible = relevant && tinyStationaryEnabled.Checked;
                tinyStationaryPanel.Enabled = true;
                if (relevant)
                {
                    // Keep the old pop-up timeline out of these two categories;
                    // all of their TinyMT input is now on this page.
                    B_OpenTool.Visible = false;
                    AssumeSynced.Visible = false;
                }
                else if (Method == 0)
                {
                    B_OpenTool.Visible = gen6timeline_available || IsHorde;
                    AssumeSynced.Visible = gen6timeline_available || IsHorde;
                }
                tinyStationaryEnabled.BringToFront();
                tinyStationaryHelp.BringToFront();
                tinyStationaryPanel.BringToFront();
                UpdateTinyStationaryHelp();
            }
            finally
            {
                tinyStationaryUpdating = false;
            }
        }

        private uint[] TinyStationaryState()
        {
            string input = tinyStationarySeed.Text.Trim();
            if (tinyStationarySource.SelectedIndex == 0)
            {
                if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) input = input.Substring(2);
                if (input.Length == 0 || input.Length > 8 ||
                    !uint.TryParse(input, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint seed))
                    throw new ArgumentException("请输入 1～8 位十六进制 Tiny u32 种子，可带 0x 前缀。");
                return new TinyMT(seed).status;
            }

            var parts = input.Split(new[] { ',', ' ', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
                throw new ArgumentException("请输入四段 TinyMT 状态，顺序为 3、2、1、0。");
            var state = new uint[4];
            bool any = false;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 8 || !uint.TryParse(parts[i], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out state[3 - i]))
                    throw new ArgumentException("请输入四段 TinyMT 状态，顺序为 3、2、1、0。");
                any |= state[3 - i] != 0;
            }
            if (!any)
                throw new ArgumentException("TinyMT 状态不能全部为 0。");
            return state;
        }

        private TinyTimeline GetTinyStationaryTimeline()
        {
            var p = FormPM as PKM6;
            if (p == null)
                throw new ArgumentException("当前定点类别不支持 TinyMT。");
            var line = new TinyTimeline(TinyStationaryState())
            {
                Startingframe = (int)tinyStationaryFrame.Value,
                Maxframe = (int)TargetFrame.Value + 5000,
                Method = (byte)(p.InstantSync ? 0 : 1),
                P1 = 6,
                P2 = 0,
                Delay = Math.Max(0, (int)Timedelay.Value),
                CryFrame = -1,
                IsORAS = IsORAS,
            };
            line.Add((int)tinyStationaryFrame.Value, tinyStationaryCallType.SelectedIndex);
            return line;
        }
    }
}
