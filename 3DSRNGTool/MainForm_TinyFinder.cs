using System;
using System.Linq;
using System.Windows.Forms;
using Pk3DSRNGTool.Controls;
using Pk3DSRNGTool.Core;

namespace Pk3DSRNGTool
{
    public partial class MainForm
    {
        private TinyFinder.Form1 tinyFinderWindow;
        private TinyFinder.Subforms.MT.MTForm tinyFinderMTWindow;
        private TinySeedInputAdapter idTinyInput, eggTinyInput;
        private uint[] IDTinyState => idTinyInput.State;

        private void InitializeTinyInputs()
        {
            idTinyInput = new TinySeedInputAdapter(RNGPanel, "IDTiny", new[] { ID_Tiny0, ID_Tiny1, ID_Tiny2, ID_Tiny3 }, label21, label22, label23, label24);
            eggTinyInput = new TinySeedInputAdapter(TinyMT_Status, "EggTiny", new[] { St0, St1, St2, St3 }, label8, label11, label13, label16);
            eggTinyInput.StateChanged += Status_ValueChanged;
            InitializeTinyFinderWild();
            InitializeTinyStationary();
        }

        public void ShowTinyFinder()
        {
            uint? seed = Gen6 && Method == 4 ? idTinyInput.ReaderSeed : TTT.ReaderSeed;
            uint[] state = TinyIndexInputState;
            if (UseTinyFinderWild)
            {
                try { state = TinyFinderWildState(); }
                catch (ArgumentException ex) { FormUtil.Error(ex.Message); return; }
                seed = tinyWildSource.SelectedIndex == 0 ? (uint?)uint.Parse(tinyWildSeed.Text.Trim().Replace("0x", "").Replace("0X", ""), System.Globalization.NumberStyles.HexNumber) : null;
            }
            if (tinyFinderWindow == null || tinyFinderWindow.IsDisposed)
            {
                string terrain = IsHorde ? tinyWildMovingHorde.Checked ? "Moving" : "Honey" : (ea as TinyFinderArea6)?.Data.Terrain;
                tinyFinderWindow = TinyFinder.Form1.CreateEmbedded(Ver < 4 ? Ver : 0, state, seed,
                    Gen6 && Method == 4 ? TinyFinder.Controls.EnctrKey.ID : TinyFinderWildMethod, (ea as TinyFinderArea6)?.Data.Name, terrain);
                tinyFinderWindow.ResultSelected += ApplyOriginalTinyFinderResult;
            }
            tinyFinderWindow.Show(this); tinyFinderWindow.Focus();
        }

        public void ShowTinyFinderMT()
        {
            if (tinyFinderMTWindow == null || tinyFinderMTWindow.IsDisposed)
            {
                tinyFinderMTWindow = TinyFinder.Subforms.MT.MTForm.CreateEmbedded(Seed.Value, (int)Frame_min.Value,
                    (int)Frame_max.Value, (int)TSV.Value, (int)TRV.Value, !IsORAS);
                tinyFinderMTWindow.SeedSelected += seed => Seed.Value = seed;
            }
            tinyFinderMTWindow.Show(this); tinyFinderMTWindow.Focus();
        }

        private void ApplyOriginalTinyFinderResult(object sender, TinyFinder.EmbeddedResultEventArgs result)
        {
            Ver = result.GameVersion;
            TTT.ImportIndexState(result.State);
            indexedHorde = null;
            if (result.Method == "ID") { RNGMethod.SelectedIndex = 4; idTinyInput.State = result.State; }
            else if (result.Options != null)
            {
                var options = result.Options;
                RNGMethod.SelectedIndex = 2;
                var type = options.Method == TinyFinder.Controls.EnctrKey.Horde ? EncounterType.Horde :
                    options.Method == TinyFinder.Controls.EnctrKey.Fishing ? EncounterType.Fishing :
                    options.Method == TinyFinder.Controls.EnctrKey.RockSmash ? EncounterType.RockSmash :
                    options.Method == TinyFinder.Controls.EnctrKey.Radar ? EncounterType.PokeRadar :
                    options.Method == TinyFinder.Controls.EnctrKey.FS ? EncounterType.FriendSafari :
                    options.Method == TinyFinder.Controls.EnctrKey.Ambush ? EncounterType.CaveShadow :
                    options.Method == TinyFinder.Controls.EnctrKey.DexNavMov || options.Method == TinyFinder.Controls.EnctrKey.DexNavSrch ? EncounterType.DexNav : EncounterType.Normal;
                var categories = Ver < 2 ? PKMW6.Species_XY : PKMW6.Species_ORAS;
                CB_Category.SelectedIndex = Array.FindIndex(categories, c => ((PKMW6)c.List[0]).Type == type);
                tinyWildHoney.Checked = options.Method == TinyFinder.Controls.EnctrKey.Honey;
                tinyWildMovingHorde.Checked = options.MovingHorde; tinyWildActiveNav.Checked = options.Method == TinyFinder.Controls.EnctrKey.DexNavSrch;
                tinyWildEnabled.Checked = true;
                if (type == EncounterType.Fishing && result.Area != null) Poke.SelectedIndex = result.Area.Terrain == "Old Rod" ? 0 : result.Area.Terrain == "Good Rod" ? 1 : 2;
                RefreshLocation();
                if (result.Area != null)
                {
                    var area = (LocationTable6.TableNow ?? new EncounterArea[0]).OfType<TinyFinderArea6>()
                        .FirstOrDefault(a => a.Data.Name == result.Area.Name && a.Data.Terrain == result.Area.Terrain);
                    if (area != null) MetLocation.SelectedValue = area.Locationidx;
                }
                tinyWildNPC.Value = options.NPC; tinyWildParty.Value = options.PartySize; tinyWildBag.Value = options.BagAdvances;
                tinyWildBlink.Value = options.LongBlink; tinyWildHoneyDelay.Value = options.HoneyDelay; tinyWildInteract.Value = options.InteractFrame;
                tinyWildSearchLevel.Value = options.SearchLevel; Special_th.Value = options.Rate; ChainLength.Value = options.Chain;
                tinyWildPossibleHorde.Checked = options.PossibleHorde; tinyWildRadarGrass.Checked = options.RadarGrass;
                tinyWildExclusive.Checked = options.Exclusive; tinyWildMusic.Checked = options.BonusMusic;
                if (options.FixedSpecies != 0) SlotSpecies.SelectedValue = options.FixedSpecies;
                tinyWildSource.SelectedIndex = 1;
                tinyWildSeed.Text = string.Join(",", result.State.Reverse().Select(w => w.ToString("X8")));
                AssumeSynced.Checked = result.Synchronized;
            }
            if (IsDexNav && result.GameVersion >= 2)
            {
                CB_NavHA.Checked = result.HiddenAbility;
                Potential.Value = result.Potential;
                CB_ForcedShiny.Checked = result.Shiny;
            }
        }
    }
}
