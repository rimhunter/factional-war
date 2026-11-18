using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimhunterTestRunner.Scenarios
{
    static class DlcHelpers
    {
        public sealed class FactionScope : System.IDisposable {
            readonly List<KeyValuePair<Faction, bool?>> saved = new List<KeyValuePair<Faction, bool?>>();
            readonly List<Faction> added = new List<Faction>();

            public FactionScope(params string[] onlyDefNames) {
                foreach (var faction in Find.FactionManager.AllFactionsVisible.ToList()) {
                    if (faction.IsPlayer || onlyDefNames.Contains(faction.def.defName)) continue;
                    saved.Add(new KeyValuePair<Faction, bool?>(faction, faction.hidden));
                    faction.hidden = true;
                }
            }

            public Faction Add(string defName) {
                var existing = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.def.defName == defName);
                if (existing != null) return existing;
                var faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(DefDatabase<FactionDef>.GetNamed(defName)));
                Find.FactionManager.Add(faction);
                added.Add(faction);
                return faction;
            }

            public void Dispose() {
                foreach (var entry in saved) entry.Key.hidden = entry.Value;
                foreach (var faction in added) faction.hidden = true;
            }
        }

        public static Faction FactionOf(string defName) =>
            Find.FactionManager.AllFactionsListForReading.First(f => f.def.defName == defName);

        public static List<Pawn> Spawned(Map map, Faction faction) => map.mapPawns.SpawnedPawnsInFaction(faction).ToList();
    }
}
