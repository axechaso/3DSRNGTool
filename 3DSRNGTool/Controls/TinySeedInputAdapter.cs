using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Pk3DSRNGTool.RNG;

namespace Pk3DSRNGTool.Controls
{
    // Adds a reader seed alternative while preserving the original four-word input and its callers.
    public sealed class TinySeedInputAdapter
    {
        private readonly HexMaskedTextBox[] words;
        private readonly Control[] labels;
        private readonly Label seedLabel;
        private bool updating;
        public CheckBox UseReaderSeed { get; private set; }
        public TextBox Seed { get; private set; }
        public event EventHandler StateChanged;

        public TinySeedInputAdapter(Control parent, string name, HexMaskedTextBox[] stateWords, params Control[] stateLabels)
        {
            words = stateWords; labels = stateLabels;
            UseReaderSeed = new CheckBox { Name = name + "UseReaderSeed", Text = "PokeReader", AutoSize = true,
                Font = new Font(parent.Font.FontFamily, 8), Location = new Point(Math.Max(70, parent.Width - 115), 0) };
            Seed = new TextBox { Name = name + "ReaderSeed", Font = words[3].Font, Bounds = words[3].Bounds,
                CharacterCasing = CharacterCasing.Upper, TextAlign = HorizontalAlignment.Right, Visible = false };
            seedLabel = new Label { Name = name + "ReaderLabel", Text = "Tiny u32", AutoSize = true,
                Location = new Point(Math.Max(4, words[3].Left - 68), words[3].Top + 3), Visible = false };
            parent.Controls.Add(UseReaderSeed); parent.Controls.Add(Seed); parent.Controls.Add(seedLabel);
            UseReaderSeed.BringToFront();
            UseReaderSeed.CheckedChanged += (s, e) => {
                if (!UseReaderSeed.Checked && !updating && TryGetSeed(out uint seed)) SetWords(new TinyMT(seed).status);
                foreach (var control in words.Cast<Control>().Concat(labels)) control.Visible = !UseReaderSeed.Checked;
                Seed.Visible = seedLabel.Visible = UseReaderSeed.Checked;
                StateChanged?.Invoke(this, EventArgs.Empty);
            };
            Seed.TextChanged += (s, e) => StateChanged?.Invoke(this, EventArgs.Empty);
            foreach (var word in words) word.TextChanged += (s, e) => {
                if (updating) return;
                updating = true; UseReaderSeed.Checked = false; updating = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
            };
        }
        public bool TryGetSeed(out uint seed)
        {
            string text = Seed.Text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            seed = 0;
            return text.Length > 0 && text.Length <= 8 && uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out seed);
        }
        public uint? ReaderSeed => UseReaderSeed.Checked && TryGetSeed(out uint seed) ? (uint?)seed : null;
        public bool IsValid => !UseReaderSeed.Checked || TryGetSeed(out _);
        public uint[] State
        {
            get => UseReaderSeed.Checked ? ReaderSeed.HasValue ? new TinyMT(ReaderSeed.Value).status : new uint[4] : words.Select(w => w.Value).ToArray();
            set { if (value == null || value.Length != 4) return; SetWords(value); updating = true; UseReaderSeed.Checked = false; updating = false; }
        }
        private void SetWords(uint[] state)
        {
            updating = true;
            for (int i = 0; i < 4; i++) words[i].Value = state[i];
            updating = false;
        }
    }
}
