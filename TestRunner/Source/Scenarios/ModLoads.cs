using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse.AI;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class ModLoads
    {
        const string Namespace = "SR.ModRimWorld.FactionalWar.";

        [Scenario("startup-loads-mod-content-with-no-errors", MapSize = 40, OnlyOn = "1.7")]
        static void LoadsContent(ScenarioContext s) {
            s.Check(Current.ProgramState == ProgramState.Playing, "the game should reach a map");
            s.Check(Find.CurrentMap == s.Map, "the scenario map should be the current map");

            var incidentWorkers = new Dictionary<string, string> {
                ["SrFactionWar"] = Namespace + "IncidentWorkerFactionWar",
                ["SrFactionWarShellingSiteGenerate"] = Namespace + "IncidentWorkerFactionWarShellingSiteGenerate",
                ["SrFactionWarContentionSiteGenerate"] = Namespace + "IncidentWorkerFactionWarContentionSiteGenerate",
                ["SrFactionWarTempCampSiteGenerate"] = Namespace + "IncidentWorkerFactionWarTempCampSiteGenerate",
            };
            foreach (var pair in incidentWorkers) {
                var def = DefDatabase<IncidentDef>.GetNamedSilentFail(pair.Key);
                s.Check(def != null, "the incident " + pair.Key + " should be loaded");
                s.AreEqual(pair.Value, def?.workerClass?.FullName, "the worker class of incident " + pair.Key);
            }

            var raid = DefDatabase<RaidStrategyDef>.GetNamedSilentFail("SrFactionFirst");
            s.Check(raid != null, "the raid strategy SrFactionFirst should be loaded");
            s.AreEqual(Namespace + "RaidStrategyWorkerFactionFirst", raid?.workerClass?.FullName, "the worker class of raid strategy SrFactionFirst");

            var siteParts = new[] { "SrFactionContention", "SrFactionTempCamp", "SrFactionWarShelling" };
            foreach (var name in siteParts) {
                var def = DefDatabase<SitePartDef>.GetNamedSilentFail(name);
                s.Check(def != null, "the site part " + name + " should be loaded");
                s.AreEqual(Namespace + "SitePartWorkerEmpty", def?.workerClass?.FullName, "the worker class of site part " + name);
            }

            var worldObjects = new Dictionary<string, string> {
                ["SrSiteFactionWarShelling"] = Namespace + "SiteFactionWarShelling",
                ["SrSiteFactionWarContention"] = Namespace + "SiteFactionWarContention",
                ["SrSiteFactionWarTempCamp"] = Namespace + "SiteTempCamp",
            };
            foreach (var pair in worldObjects) {
                var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail(pair.Key);
                s.Check(def != null, "the world object " + pair.Key + " should be loaded");
                s.AreEqual(pair.Value, def?.worldObjectClass?.FullName, "the class of world object " + pair.Key);
            }

            var job = DefDatabase<JobDef>.GetNamedSilentFail("SrKillMelee");
            s.Check(job != null, "the job SrKillMelee should be loaded");
            s.AreEqual(Namespace + "JobDriverKillMelee", job?.driverClass?.FullName, "the driver class of job SrKillMelee");

            var duties = new[] { "SrAssaultFactionFirst", "SrKillHostileFactionMember", "SrClearBattlefield", "SrDefend", "SrRetreat", "SrPlunderFaction" };
            s.AreEqual("", string.Join(",", duties.Where(n => DefDatabase<DutyDef>.GetNamedSilentFail(n) == null)), "the duties that did not load");

            s.Check(DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("SrTwoFactionsEdgeWalkIn") != null, "the arrival mode SrTwoFactionsEdgeWalkIn should be loaded");

            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();
        }
    }
}
