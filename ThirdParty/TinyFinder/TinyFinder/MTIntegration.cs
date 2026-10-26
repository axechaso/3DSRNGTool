using System;
using System.Globalization;
using System.Windows.Forms;

namespace TinyFinder.Subforms.MT
{
    public partial class MTForm
    {
        public event Action<uint> SeedSelected;

        public static MTForm CreateEmbedded(uint seed, int min, int max, int tsv, int trv, bool xy)
        {
            var form = new MTForm();
            if (Properties.Settings.Default.CPUs < 1) Properties.Settings.Default.CPUs = Math.Max(1, Environment.ProcessorCount);
            // The original main application hides this window; a host owns its lifetime.
            form.FormClosing -= form.MTForm_FormClosing;
            form.FormClosing += (s, e) => form.StopEmbeddedSearch();
            form.Load += (s, e) => {
                form.SetGame(xy);
                form.Start_Seed.Value = seed;
                form.End_Seed.Value = seed == uint.MaxValue ? seed : seed + 1;
                form.MinFrame.Value = Math.Max(0, min); form.MaxFrame.Value = Math.Max(min + 1, max);
                form.CurrentTSV.Value = (uint)tsv; form.CurrentTRV.Value = (uint)trv;
            };
            foreach (var grid in new[] { form.G6_DGV, form.EC_DGV, form.HORDE_DGV, form.Seed_DGV })
            {
                var menu = new ContextMenuStrip();
                var send = menu.Items.Add("回填主种子到 3DSRNGTool");
                send.Click += (s, e) => {
                    if (grid.CurrentRow == null) return;
                    int seedColumn = grid == form.Seed_DGV ? 1 : 0;
                    var value = grid.CurrentRow.Cells[seedColumn].Value;
                    uint result;
                    if (value is uint) result = (uint)value;
                    else if (!uint.TryParse(Convert.ToString(value), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result)) return;
                    form.SeedSelected?.Invoke(result);
                };
                grid.ContextMenuStrip = menu;
            }
            return form;
        }

        public void StopEmbeddedSearch() => TinyFinder.Form1.StopThreads(jobs);
    }
}
