using System;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using TinyFinder.Controls;

namespace TinyFinder
{
    // Host integration is kept separate from the unmodified upstream window and generators.
    public sealed class EmbeddedResultEventArgs : EventArgs
    {
        public uint[] State { get; internal set; }
        public int GameVersion { get; internal set; }
        public string Method { get; internal set; }
        public int Index { get; internal set; }
        public int TID { get; internal set; }
        public int SID { get; internal set; }
        public int Potential { get; internal set; }
        public bool HiddenAbility { get; internal set; }
        public bool Shiny { get; internal set; }
        public bool Synchronized { get; internal set; }
        public WildOptions Options { get; internal set; }
        public WildAreaData Area { get; internal set; }
    }

    public partial class Form1
    {
        public event EventHandler<EmbeddedResultEventArgs> ResultSelected;
        private uint[] hostSearchState;
        private int hostSearchVersion;
        private WildAreaData hostSearchArea;

        public static Form1 CreateEmbedded(int gameVersion, uint[] state, uint? readerSeed = null, EnctrKey method = EnctrKey.Wild, string areaName = null, string terrain = null)
        {
            var form = new Form1();
            Program.form = form;
            // Run after upstream Form1_Load, which configures the original databases and UI.
            form.Load += (s, e) => {
                form.SetEmbeddedInput(gameVersion, state, readerSeed);
                var choice = form.EncounterTypeList.FirstOrDefault(t => t.Key == method);
                if (choice != null) form.Methods.SelectedItem = choice;
                if (areaName != null && form.locationsComboBox.Items.Contains(areaName)) form.locationsComboBox.SelectedItem = areaName;
                if (terrain != null && form.EncounterSettings.Items.Contains(terrain)) form.EncounterSettings.SelectedItem = terrain;
            };
            foreach (var button in new[] { form.MainButton, form.IgnoreFiltersButton })
            {
                button.Click -= form.MainButton_Click;
                button.Click += (s, e) => {
                    form.hostSearchVersion = form.GameVersion.SelectedIndex;
                    form.hostSearchState = form.ReaderBTN.Checked ? new TinyMT().init(form.t3.Value, 0) : new[] { form.t0.Value, form.t1.Value, form.t2.Value, form.t3.Value };
                    var key = form.SelectedEncounter.Key;
                    string place = key == EnctrKey.ID || key == EnctrKey.FS ? null : form.CurrentLocation.Name;
                    string kind = key == EnctrKey.Horde ? "Horde" : key == EnctrKey.RockSmash ? "Rock Smash" : key == EnctrKey.Ambush ? "Cave Shadows" : Convert.ToString(form.EncounterSettings.SelectedItem);
                    form.hostSearchArea = place == null ? null : WildIntegration.GetAreas(form.hostSearchVersion, key).FirstOrDefault(a => a.Name == place && a.Terrain == kind);
                    form.MainButton_Click(s, e);
                };
            }
            var send = new ToolStripMenuItem("回填到 3DSRNGTool");
            send.Click += (s, e) => form.ExportSelectedResult();
            form.OptionsMenu.Items.Add(new ToolStripSeparator());
            form.OptionsMenu.Items.Add(send);
            form.FormClosing += (s, e) =>
            {
                StopThreads(form.jobs);
                form.Working = false;
                if (form.DateSearcher && !form.Calibrated)
                {
                    // Let upstream's calibration DoEvents loop unwind before disposing controls.
                    form.Calibrated = true; e.Cancel = true;
                    form.BeginInvoke(new Action(form.Close)); return;
                }
                // Upstream's auxiliary windows normally outlive their close buttons.
                if (form.mersenne != null) { form.mersenne.StopEmbeddedSearch(); form.mersenne.Dispose(); }
                if (form.ntrhelper != null)
                {
                    if (NTRHelper.ntrclient != null && NTRHelper.ntrclient.Connected) NTRHelper.ntrclient.disconnect();
                    form.ntrhelper.Dispose();
                }
            };
            return form;
        }

        internal static void StopThreads(Thread[] threads)
        {
            if (threads == null) return;
            foreach (var thread in threads)
                if (thread != null && thread != Thread.CurrentThread && thread.IsAlive) thread.Abort();
        }

        public void SetEmbeddedInput(int gameVersion, uint[] state, uint? readerSeed = null)
        {
            GameVersion.SelectedIndex = Math.Max(0, Math.Min(3, gameVersion));
            Methods.SelectedItem = EncounterTypeList.First(t => t.Key == EnctrKey.Wild);
            ReaderBTN.Checked = readerSeed.HasValue || state == null || state.All(w => w == 0);
            if (ReaderBTN.Checked) t3.Value = readerSeed ?? 0;
            else { t0.Value = state[0]; t1.Value = state[1]; t2.Value = state[2]; t3.Value = state[3]; }
        }

        public bool ExportSelectedResult()
        {
            var grid = SearchGen.SelectedIndex == 0 ? Searcher : Generator;
            var row = grid.CurrentRow == null ? null : grid.CurrentRow.DataBoundItem as Index;
            if (row == null) return false;
            var state = new[] { row.Tiny0, row.Tiny1, row.Tiny2, row.Tiny3 };
            if (SearchGen.SelectedIndex == 0 || hostSearchState != null)
            {
                var baseState = SearchGen.SelectedIndex == 0 ? state : hostSearchState;
                var rng = new TinyMT();
                int advances = checked((int)row.IndexValue) - (EnctrTypeChosen == EnctrKey.DexNavSrch ? 1 : 0);
                state = (uint[])baseState.Clone();
                for (int i = 0; i < advances; i++) rng.nextState(state);
            }
            WildOptions options = null;
            if (settings != null && EnctrTypeChosen != EnctrKey.ID)
            {
                options = new WildOptions {
                    Method = settings.encounterKey, ORAS = settings.oras, MovingHorde = settings.moving,
                    NPC = settings.calibration, PartySize = settings.party == 0 ? (int)party.Value : settings.party,
                    Rate = settings.ratio, BagAdvances = getBagAdvances(), LongBlink = settings.longBlinkRand,
                    InteractFrame = settings.interactMTFrame, HoneyDelay = settings.honeyDelay, Chain = settings.chain,
                    SearchLevel = settings.searchLevel, SafariSlots = settings.sType + 1,
                    PossibleHorde = settings.possibleHorde, RadarGrass = settings.radarGrass, BonusMusic = settings.bonusMusic,
                    ShinyCharm = settings.charm, Exclusive = settings.exclusives, Flute = settings.fluteOption,
                    FixedSpecies = WildIntegration.ToSpeciesForm(EnctrTypeChosen == EnctrKey.FS ? settings.DexNumberFS : settings.FixedSpecies),
                };
            }
            ResultSelected?.Invoke(this, new EmbeddedResultEventArgs {
                State = state, Options = options, Area = hostSearchArea,
                GameVersion = hostSearchState == null ? GameVersion.SelectedIndex : hostSearchVersion, Method = EnctrTypeChosen.ToString(),
                Index = checked((int)row.IndexValue), TID = row.trainerID, SID = row.secretID,
                Potential = row.potential, HiddenAbility = row.DexNavHA, Shiny = row.shiny, Synchronized = row.Sync,
            });
            return true;
        }
    }
}
