using System.Linq;

namespace Pk3DSRNGTool
{
    public sealed class TinyFinderArea6 : EncounterArea6
    {
        public TinyFinder.WildAreaData Data { get; private set; }
        private readonly int key;
        public TinyFinderArea6(TinyFinder.WildAreaData data, int id)
        {
            Data = data; key = 0x100000 + id; Species = data.Species.Select(TinyFinder.WildIntegration.ToSpeciesForm).ToArray();
        }
        public override int Locationidx => key;
        public override int[] getSpecies(int ver, bool night) => Species;
    }
}
