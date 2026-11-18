using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimhunterTestRunner.Scenarios
{
    sealed class PlayerSideRun
    {
        public (string Incident, string WorldObject) Kind;
        public WorldObject Site;
        public Map Map;
        public Caravan Caravan;
        public Pawn Colonist;
        public Faction A;
        public Faction B;
        public Dictionary<Faction, List<Pawn>> Sides;
        public Dictionary<Faction, (int Goodwill, FactionRelationKind Kind)> Snapshot;
    }

    static class PlayerSideHelpers
    {
        public static readonly (string Incident, string WorldObject)[] SiteKinds = {
            ("SrFactionWarContentionSiteGenerate", "SrSiteFactionWarContention"),
            ("SrFactionWarShellingSiteGenerate", "SrSiteFactionWarShelling"),
            ("SrFactionWarTempCampSiteGenerate", "SrSiteFactionWarTempCamp"),
        };

        public static Dictionary<Faction, (int Goodwill, FactionRelationKind Kind)> SnapshotRelations() =>
            Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.def.permanentEnemy)
                .ToDictionary(f => f, f => (f.PlayerGoodwill, f.PlayerRelationKind));

        public static void RestoreRelations(Dictionary<Faction, (int Goodwill, FactionRelationKind Kind)> snapshot) {
            foreach (var kv in snapshot) {
                kv.Key.TryAffectGoodwillWith(Faction.OfPlayer, kv.Value.Goodwill - kv.Key.PlayerGoodwill, false, false);
                kv.Key.RelationWith(Faction.OfPlayer).kind = kv.Value.Kind;
                Faction.OfPlayer.RelationWith(kv.Key).kind = kv.Value.Kind;
            }
        }

        public static void SetGoodwill(Faction faction, int value) {
            faction.TryAffectGoodwillWith(Faction.OfPlayer, value - faction.PlayerGoodwill, false, false);
        }

        public static bool Hostile(Faction faction) => faction.HostileTo(Faction.OfPlayer);

        public static PlayerSideRun Start(ScenarioContext s, (string Incident, string WorldObject) kind, bool everyoneFriendly) {
            var run = new PlayerSideRun { Kind = kind, Snapshot = SnapshotRelations() };
            var ok = false;
            try {
                for (var attempt = 0; attempt < 6; attempt++) {
                    run.Site = BattleHelpers.FireSiteIncident(s, kind.Incident, kind.WorldObject);
                    s.Check(run.Site != null, kind.WorldObject + ": the site should exist");
                    if (run.Site == null) return null;
                    if (everyoneFriendly)
                        foreach (var f in run.Snapshot.Keys) SetGoodwill(f, 100);
                    run.Map = BattleHelpers.ArriveByCaravan(s, run.Site, out run.Caravan);
                    Current.Game.CurrentMap = run.Map;
                    run.Colonist = run.Map.mapPawns.FreeColonistsSpawned.Single();
                    run.Sides = BattleHelpers.Sides(run.Map);
                    s.AreEqual(2, run.Sides.Count, kind.WorldObject + ": two sides should be on the map; found: " + BattleHelpers.Describe(run.Sides));
                    if (run.Sides.Count != 2) break;
                    var order = run.Sides.Keys.OrderBy(f => f.def.permanentEnemy ? 1 : 0).ToList();
                    run.A = order[0];
                    run.B = order[1];
                    if (!run.A.def.permanentEnemy) break;
                    BattleHelpers.Cleanup(s, run.Site, run.Caravan);
                    run.Site = null;
                    run.Caravan = null;
                    RestoreRelations(run.Snapshot);
                    s.Check(attempt < 5, kind.WorldObject + ": six sites in a row paired two factions that can never be at peace with the player");
                }
                ok = true;
                return run;
            } finally {
                if (!ok) {
                    if (run.Site != null) BattleHelpers.Cleanup(s, run.Site, run.Caravan);
                    RestoreRelations(run.Snapshot);
                }
            }
        }

        public static void ChooseSides(ScenarioContext s, PlayerSideRun run, int friendlyGoodwill) {
            s.Check(!run.A.def.permanentEnemy, run.Kind.WorldObject + ": one side should be able to be friendly to the player; sides: " + BattleHelpers.Describe(run.Sides));
            SetGoodwill(run.A, friendlyGoodwill);
            if (!run.B.def.permanentEnemy) SetGoodwill(run.B, -100);
            s.Check(!Hostile(run.A) && Hostile(run.B), "setup: " + run.A.Name + " should be friendly and " + run.B.Name + " hostile to the player; the game says " + run.A.PlayerRelationKind + " and " + run.B.PlayerRelationKind);
        }

        public static void Finish(ScenarioContext s, PlayerSideRun run) {
            if (run == null) return;
            BattleHelpers.Cleanup(s, run.Site, run.Caravan);
            RestoreRelations(run.Snapshot);
        }

        public static bool Targets(Pawn attacker, Pawn victim) {
            if (attacker.mindState.enemyTarget == victim) return true;
            var job = attacker.CurJob;
            if (job != null && (job.targetA.Thing == victim || job.targetB.Thing == victim)) return true;
            return attacker.stances.curStance is Stance_Warmup warmup && warmup.focusTarg.Thing == victim;
        }

        public static int StandingOf(Map map, Faction faction) =>
            map.mapPawns.SpawnedPawnsInFaction(faction).Count(BattleHelpers.Standing);

        public static void Slay(Pawn pawn, Pawn instigator) {
            var dinfo = new DamageInfo(DamageDefOf.Bullet, 200f, 1f, -1f, instigator);
            pawn.TakeDamage(dinfo);
            if (!pawn.Dead) pawn.Kill(dinfo);
        }

        public static void KillHostile(Map map, Pawn instigator) {
            foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && !p.Faction.IsPlayer && p.HostileTo(Faction.OfPlayer)).ToList())
                Slay(pawn, instigator);
        }

        public sealed class Watch {
            public readonly List<string> Attacks = new List<string>();
            public float Closest = float.MaxValue;
        }

        public static void Observe(Watch watch, IEnumerable<Pawn> watchers, IEnumerable<Pawn> victims, int tick) {
            var targets = victims.Where(v => v.Spawned && !v.Dead).ToList();
            foreach (var pawn in watchers.Where(p => p.Spawned && !p.Dead)) {
                foreach (var victim in targets) {
                    var distance = pawn.Position.DistanceTo(victim.Position);
                    if (distance < watch.Closest) watch.Closest = distance;
                    if (Targets(pawn, victim)) watch.Attacks.Add(pawn.Faction.Name + "/" + pawn.LabelShort + "->" + victim.LabelShort + "@" + tick);
                }
            }
        }

        public static void PlaceNear(Pawn colonist, Pawn anchor, Map map, float distance) {
            var cell = GenRadial.RadialCellsAround(anchor.Position, distance + 4f, true)
                .First(c => c.DistanceTo(anchor.Position) >= distance && c.InBounds(map) && c.Standable(map) && c.Walkable(map) && map.reachability.CanReach(anchor.Position, c, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)));
            colonist.DeSpawn();
            GenSpawn.Spawn(colonist, cell, map);
            colonist.drafter.Drafted = true;
        }

        public static Watch HostileControlTargets(ScenarioContext s, Map map, Faction faction, IList<Pawn> victims, int maxTicks) {
            SetGoodwill(faction, -100);
            var prod = map.mapPawns.SpawnedPawnsInFaction(faction).Where(p => p.Spawned && p.RaceProps.Humanlike).OrderBy(p => p.Position.DistanceTo(victims[0].Position)).First();
            prod.TakeDamage(new DamageInfo(DamageDefOf.Cut, 1f, 10f, -1f, victims[0]));
            var watch = new Watch();
            var ticks = 0;
            while (ticks < maxTicks && watch.Attacks.Count == 0) {
                s.Tick(10);
                ticks += 10;
                Observe(watch, map.mapPawns.SpawnedPawnsInFaction(faction), victims, ticks);
            }
            SetGoodwill(faction, 100);
            foreach (var pawn in map.mapPawns.SpawnedPawnsInFaction(faction)) {
                pawn.mindState.enemyTarget = null;
                pawn.jobs.StopAll();
                pawn.stances.CancelBusyStanceHard();
            }
            return watch;
        }

        public static void KillSide(Map map, Faction faction, Pawn instigator) {
            foreach (var pawn in map.mapPawns.SpawnedPawnsInFaction(faction).ToList())
                Slay(pawn, instigator);
        }

        public static Pawn SpawnControlPawn(ScenarioContext s, Faction faction, PawnKindDef kind = null) {
            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind ?? faction.RandomPawnKind(), faction));
            GenSpawn.Spawn(pawn, s.Cell(0, 0), s.Map);
            return pawn;
        }

        public static Command ReformCommand(WorldObject site) {
            var comp = ((MapParent)site).AllComps.OfType<FormCaravanComp>().Single();
            var label = "CommandReformCaravan".Translate().ToString();
            return comp.GetGizmos().OfType<Command>().SingleOrDefault(c => c.defaultLabel == label);
        }
    }
}
