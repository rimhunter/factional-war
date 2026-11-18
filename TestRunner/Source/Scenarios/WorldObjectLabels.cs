using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class WorldObjectLabels
    {
        // The stray text in WorldObjects.xml is dropped by the Def loader, so nothing here can show it; the regression is owned by the unit test ModFolderTests.World_object_defs_hold_no_text_outside_their_elements.
        [Scenario("world-object-labels-and-descriptions-hold-no-stray-text", MapSize = 40, OnlyOn = "1.7")]
        static void LabelsAreClean(ScenarioContext s) {
            var expected = new Dictionary<string, (string Part, string Label, string Description)> {
                ["SrSiteFactionWarShelling"] = ("SrFactionWarShelling", "Faction Bombardment", "Two unknown factions are engaged in a fierce shelling battle here."),
                ["SrSiteFactionWarContention"] = ("SrFactionContention", "Faction Contention", "A batch of precious resources landed nearby, causing two factions to compete."),
                ["SrSiteFactionWarTempCamp"] = ("SrFactionTempCamp", "Faction Defense", "A camp at war."),
            };
            foreach (var pair in expected) {
                var objectDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail(pair.Key);
                s.Check(objectDef != null, "the world object " + pair.Key + " should be loaded");
                if (objectDef == null) continue;
                var partDef = DefDatabase<SitePartDef>.GetNamed(pair.Value.Part);
                if (!TileFinder.TryFindNewSiteTile(out var tile, 2, 7)) throw new ScenarioFailure("no tile found for the site of " + pair.Key);
                var site = (Site)WorldObjectMaker.MakeWorldObject(objectDef);
                site.Tile = tile;
                SiteMakerHelper.GenerateDefaultParams(1000f, tile, null, new[] { partDef }, out var parts);
                foreach (var part in parts) site.AddPart(new SitePart(site, part.def, part.parms));
                Find.WorldObjects.Add(site);
                s.AreEqual(pair.Value.Label, partDef.label, "the label of the site part " + pair.Value.Part);
                s.AreEqual(pair.Value.Label, site.Label, "the label of the world object " + pair.Key + " on the map");
                s.AreEqual(pair.Value.Description, partDef.description, "the description of the site part " + pair.Value.Part);
                site.Destroy();
            }
            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();
        }
    }
}
