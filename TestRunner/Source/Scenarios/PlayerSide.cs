using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimhunterTestRunner.Scenarios
{
    static class PlayerSide
    {
        static readonly (string Incident, string WorldObject)[] Kinds = PlayerSideHelpers.SiteKinds;

        static void Bystander(ScenarioContext s, (string Incident, string WorldObject) kind) {
            var proven = new List<string>();
            var controlRun = PlayerSideHelpers.Start(s, kind, true);
            try {
                if (controlRun == null || controlRun.Sides.Count != 2) return;
                var friendly = controlRun.Sides.Keys.Where(f => !PlayerSideHelpers.Hostile(f)).ToList();
                s.Check(friendly.Count > 0, "setup: at least one side should be at peace with the player; the game says " + string.Join(", ", controlRun.Sides.Keys.Select(f => f.Name + " " + f.PlayerRelationKind)));
                foreach (var side in friendly) {
                    PlayerSideHelpers.PlaceNear(controlRun.Colonist, controlRun.Sides[side].First(p => p.Spawned), controlRun.Map, 12f);
                    var control = PlayerSideHelpers.HostileControlTargets(s, controlRun.Map, side, new[] { controlRun.Colonist }, 1500);
                    if (control.Attacks.Count > 0) proven.Add(side.def.defName);
                    if (!controlRun.Colonist.Spawned || controlRun.Colonist.Dead) break;
                }
            } finally {
                PlayerSideHelpers.Finish(s, controlRun);
            }
            s.Check(proven.Count > 0, kind.WorldObject + ": control: on a battle of this kind, with a side made hostile and its nearest pawn hit by the player's pawn standing 12 cells away, a pawn should target the player's pawn within 1500 ticks; otherwise the peaceful run below proves nothing");

            var run = PlayerSideHelpers.Start(s, kind, true);
            try {
                if (run == null || run.Sides.Count != 2) return;
                var friendly = run.Sides.Keys.Where(f => !PlayerSideHelpers.Hostile(f)).ToList();
                s.Check(friendly.Count > 0, "setup: at least one side should be at peace with the player; the game says " + string.Join(", ", run.Sides.Keys.Select(f => f.Name + " " + f.PlayerRelationKind)));
                if (friendly.Count == 0) return;
                PlayerSideHelpers.PlaceNear(run.Colonist, run.Sides[friendly[0]].First(p => p.Spawned), run.Map, 12f);
                var victims = new[] { run.Colonist };
                var start = run.Sides.Keys.ToDictionary(f => f, f => (f.PlayerGoodwill, f.PlayerRelationKind));
                var watches = friendly.ToDictionary(f => f, f => new PlayerSideHelpers.Watch());
                var ticks = 0;
                while (ticks < 20000 && (ticks < 1500 || BattleHelpers.Hurt(run.Map) == 0)) {
                    s.Tick(10);
                    ticks += 10;
                    foreach (var side in friendly)
                        PlayerSideHelpers.Observe(watches[side], run.Map.mapPawns.SpawnedPawnsInFaction(side), victims, ticks);
                }
                s.Check(BattleHelpers.Hurt(run.Map) > 0, kind.WorldObject + ": the battle should go on while the player does nothing; nobody was hurt after " + ticks + " ticks");
                s.Metric(kind.WorldObject + " closest peaceful pawn to the player's pawn", watches.Values.Min(w => w.Closest));
                foreach (var side in friendly)
                    s.AreEqual(0, watches[side].Attacks.Count, kind.WorldObject + ": " + side.Name + " is at peace with the player and should not target the player's pawn (closest it came: " + watches[side].Closest + " cells); " + string.Join(", ", watches[side].Attacks.Take(5)));
                foreach (var f in run.Sides.Keys) {
                    s.AreEqual(start[f].PlayerGoodwill, f.PlayerGoodwill, kind.WorldObject + ": the player's goodwill with " + f.Name + " should be what the game reported before");
                    s.AreEqual(start[f].PlayerRelationKind, f.PlayerRelationKind, kind.WorldObject + ": the player's relation with " + f.Name + " should be what the game reported before");
                }
            } finally {
                PlayerSideHelpers.Finish(s, run);
            }
        }

        [Scenario("player-side-bystander-contention-nobody-at-peace-attacks-and-relations-stay", MapSize = 120, MaxSeconds = 120, OnlyOn = "1.7")]
        static void BystanderContention(ScenarioContext s) => Bystander(s, Kinds[0]);

        [Scenario("player-side-bystander-shelling-nobody-at-peace-attacks-and-relations-stay", MapSize = 120, MaxSeconds = 120, OnlyOn = "1.7")]
        static void BystanderShelling(ScenarioContext s) => Bystander(s, Kinds[1]);

        [Scenario("player-side-bystander-camp-nobody-at-peace-attacks-and-relations-stay", MapSize = 120, MaxSeconds = 120, OnlyOn = "1.7")]
        static void BystanderCamp(ScenarioContext s) => Bystander(s, Kinds[2]);

        [Scenario("player-side-helping-a-side-pays-what-the-game-reports-and-the-other-side-is-unchanged", MapSize = 120, MaxSeconds = 60, OnlyOn = "1.7")]
        static void HelpOneSide(ScenarioContext s) {
            var run = PlayerSideHelpers.Start(s, Kinds[1], false);
            try {
                if (run == null || run.Sides.Count != 2) return;
                PlayerSideHelpers.ChooseSides(s, run, 50);
                var kill = new DamageInfo(DamageDefOf.Bullet, 200f, 1f, -1f, run.Colonist);

                var controlBefore = run.B.PlayerGoodwill;
                var control = PlayerSideHelpers.SpawnControlPawn(s, run.B);
                control.TakeDamage(kill);
                var controlDelta = run.B.PlayerGoodwill - controlBefore;
                control.Corpse?.Destroy();

                var aControlBefore = run.A.PlayerGoodwill;
                var aControl = PlayerSideHelpers.SpawnControlPawn(s, run.A);
                aControl.TakeDamage(kill);
                s.Check(run.A.PlayerGoodwill < aControlBefore, "control: killing a pawn of the friendly side should cost goodwill, or this setup does not measure goodwill; it went from " + aControlBefore + " to " + run.A.PlayerGoodwill);
                aControl.Corpse?.Destroy();
                PlayerSideHelpers.SetGoodwill(run.A, 50);

                var friendly = run.Sides[run.A].First(p => p.RaceProps.Humanlike);
                HealthUtility.DamageUntilDowned(friendly, false);
                TendUtility.DoTend(run.Colonist, friendly, null);
                var gain = run.A.GetGoodwillGainForExit(friendly, false, out var healthy, out var inMentalState);
                s.Check(gain > 0 && healthy, "the game should value a tended, healthy friendly pawn leaving at more than 0 goodwill; it says " + gain + ", healthy " + healthy);

                var aBefore = run.A.PlayerGoodwill;
                var bBefore = run.B.PlayerGoodwill;
                PlayerSideHelpers.KillSide(run.Map, run.B, run.Colonist);
                s.AreEqual(aBefore, run.A.PlayerGoodwill, "killing the hostile side should not change the player's goodwill with " + run.A.Name);
                s.AreEqual(bBefore + controlDelta, run.B.PlayerGoodwill, "killing the hostile side should change the player's goodwill with " + run.B.Name + " as much as killing one of its pawns outside the battle did");

                var left = s.RunUntil(() => friendly.IsWorldPawn(), 6000);
                s.Check(left && !friendly.Dead, "the tended friendly pawn " + friendly.LabelShort + " should be carried off the map alive by its side after the battle (it left the map into the world)");
                s.AreEqual(aBefore + gain, run.A.PlayerGoodwill, "the player's goodwill with " + run.A.Name + " after its tended pawn left should rise by what the game said it was worth");
                s.Check(!PlayerSideHelpers.Hostile(run.A), run.A.Name + " should still be at peace with the player");
            } finally {
                PlayerSideHelpers.Finish(s, run);
            }
        }

        [Scenario("player-side-harming-a-friendly-side-costs-what-the-game-charges-outside-a-battle", MapSize = 120, MaxSeconds = 40, OnlyOn = "1.7")]
        static void HarmFriendlySide(ScenarioContext s) {
            var run = PlayerSideHelpers.Start(s, Kinds[1], false);
            try {
                if (run == null || run.Sides.Count != 2) return;
                PlayerSideHelpers.ChooseSides(s, run, 50);
                var hit = new DamageInfo(DamageDefOf.Cut, 5f, 10f, -1f, run.Colonist);

                var victim = run.Sides[run.A].First(p => p.RaceProps.Humanlike);
                var control = PlayerSideHelpers.SpawnControlPawn(s, run.A, victim.kindDef);
                var controlDeltas = new List<int>();
                for (var i = 0; i < 2; i++) {
                    var before = run.A.PlayerGoodwill;
                    control.TakeDamage(hit);
                    controlDeltas.Add(run.A.PlayerGoodwill - before);
                }
                s.Check(controlDeltas.All(d => d < 0), "the game should charge goodwill for hurting a friendly pawn outside the battle; deltas " + string.Join(", ", controlDeltas));
                control.Destroy();
                PlayerSideHelpers.SetGoodwill(run.A, 50);

                var bBefore = run.B.PlayerGoodwill;
                var battleDeltas = new List<int>();
                for (var i = 0; i < 2; i++) {
                    var before = run.A.PlayerGoodwill;
                    victim.TakeDamage(hit);
                    battleDeltas.Add(run.A.PlayerGoodwill - before);
                }
                s.AreEqual(string.Join(", ", controlDeltas), string.Join(", ", battleDeltas), "two hits on a pawn of the friendly side in the battle should cost the same goodwill, hit by hit, as on a pawn of its kind outside it");
                s.AreEqual(bBefore, run.B.PlayerGoodwill, "hurting a pawn of the friendly side should not change the goodwill with the other side");
                s.Check(!PlayerSideHelpers.Hostile(run.A), run.A.Name + " should still be at peace after two small hits; the game says " + run.A.PlayerRelationKind);

                var attackers = new List<string>();
                for (var t = 0; t < 300; t += 10) {
                    s.Tick(10);
                    foreach (var pawn in run.Sides[run.A].Where(p => p.Spawned && !p.Dead && !p.Downed))
                        if (PlayerSideHelpers.Targets(pawn, run.Colonist)) attackers.Add(pawn.LabelShort + "@" + t);
                }
                s.AreEqual(0, attackers.Count, "the friendly side should not turn on the player after one small hit; targeted by " + string.Join(", ", attackers.Take(5)));
            } finally {
                PlayerSideHelpers.Finish(s, run);
            }
        }

        [Scenario("player-side-after-the-battle-corpses-and-dropped-weapons-of-both-sides-stay-and-the-player-can-take-them", MapSize = 120, MaxSeconds = 80, OnlyOn = "1.7")]
        static void LootTheField(ScenarioContext s) {
            var run = PlayerSideHelpers.Start(s, Kinds[1], false);
            try {
                if (run == null || run.Sides.Count != 2) return;
                PlayerSideHelpers.ChooseSides(s, run, 50);
                var fallenA = run.Sides[run.A].Where(p => p.RaceProps.Humanlike && p.equipment.Primary != null).Take(3).ToList();
                var fallenB = run.Sides[run.B].Where(p => p.RaceProps.Humanlike && p.equipment.Primary != null).Take(3).ToList();
                s.AreEqual(3, fallenA.Count, "setup: three armed pawns of " + run.A.Name);
                s.AreEqual(3, fallenB.Count, "setup: three armed pawns of " + run.B.Name);
                var weapons = fallenA.Concat(fallenB).ToDictionary(p => p, p => p.equipment.Primary);
                var fallen = fallenA.Concat(fallenB).ToList();

                PlayerSideHelpers.KillSide(run.Map, run.A, null);
                PlayerSideHelpers.KillSide(run.Map, run.B, run.Colonist);
                s.AreEqual(0, run.Map.mapPawns.AllPawnsSpawned.Count(p => p.Faction != null && !p.Faction.IsPlayer && p.RaceProps.Humanlike), "both sides are dead, so no winner can carry trophies off and the field stays as it is");

                var missingCorpses = fallen.Where(p => p.Corpse == null || !p.Corpse.Spawned).Select(p => p.Faction.Name + "/" + p.LabelShort).ToList();
                s.AreEqual(0, missingCorpses.Count, "the corpses of both sides should lie on the map; missing: " + string.Join(", ", missingCorpses));
                var missingWeapons = fallen.Where(p => !weapons[p].Spawned).Select(p => p.Faction.Name + "/" + p.LabelShort + "'s " + weapons[p].LabelCap).ToList();
                s.AreEqual(0, missingWeapons.Count, "the weapons dropped by both sides should lie on the map; missing: " + string.Join(", ", missingWeapons));
                var allowed = fallen.Where(p => weapons[p].Spawned && !weapons[p].IsForbidden(Faction.OfPlayer)).Select(p => weapons[p].LabelCap.ToString()).ToList();
                s.AreEqual(0, allowed.Count, "the game forbids what a pawn drops when it dies, so the player has to allow the weapons first (vanilla, the mod does not change it); not forbidden: " + string.Join(", ", allowed));
                var openCorpses = fallen.Where(p => p.Corpse != null && p.Corpse.Spawned && !p.Corpse.IsForbidden(Faction.OfPlayer)).Select(p => p.LabelShort).ToList();
                s.AreEqual(0, openCorpses.Count, "the game forbids the corpses of the fallen too; not forbidden: " + string.Join(", ", openCorpses));

                s.Check(run.Colonist.Spawned && !run.Colonist.Dead && !run.Colonist.Downed, "setup: the player's pawn should be up and on the map; dead " + run.Colonist.Dead + " downed " + run.Colonist.Downed + " spawned " + run.Colonist.Spawned);
                float Distance(Thing thing) => thing.PositionHeld.DistanceTo(run.Colonist.Position);
                foreach (var pawn in new[] { fallenA.OrderBy(p => Distance(weapons[p])).First(), fallenB.OrderBy(p => Distance(weapons[p])).First() }) {
                    var weapon = weapons[pawn];
                    ForbidUtility.SetForbidden(weapon, false, false);
                    var take = JobMaker.MakeJob(JobDefOf.TakeInventory, weapon);
                    take.count = 1;
                    run.Colonist.jobs.TryTakeOrderedJob(take);
                    var taken = s.RunUntil(() => run.Colonist.inventory.innerContainer.Contains(weapon), 9000);
                    s.Check(taken, "the player's pawn should be able to take " + pawn.Faction.Name + "/" + pawn.LabelShort + "'s " + weapon.LabelCap + " once it is allowed; it is " + (weapon.Spawned ? "at " + weapon.Position + ", colonist at " + run.Colonist.Position + " doing " + run.Colonist.CurJob?.def.defName + ", forbidden " + weapon.IsForbidden(Faction.OfPlayer) : "not on the map"));
                }

                var corpses = new[] { fallenA.Select(p => p.Corpse).Where(c => c != null && c.Spawned).OrderBy(Distance).FirstOrDefault(), fallenB.Select(p => p.Corpse).Where(c => c != null && c.Spawned).OrderBy(Distance).FirstOrDefault() };
                foreach (var corpse in corpses) {
                    s.Check(corpse != null && corpse.Spawned, "setup: a corpse of each side should lie on the map");
                    if (corpse == null || !corpse.Spawned) continue;
                    var target = GenRadial.RadialCellsAround(corpse.Position, 8f, false).First(c => c.DistanceTo(corpse.Position) >= 4f && c.InBounds(run.Map) && c.Standable(run.Map) && c.GetFirstThing<Corpse>(run.Map) == null);
                    ForbidUtility.SetForbidden(corpse, false, false);
                    var haul = JobMaker.MakeJob(JobDefOf.HaulToCell, corpse, target);
                    haul.count = 1;
                    run.Colonist.jobs.TryTakeOrderedJob(haul);
                    var start = corpse.Position;
                    var hauled = s.RunUntil(() => corpse.Spawned && corpse.Position.DistanceTo(start) >= 2f && run.Colonist.carryTracker.CarriedThing == null, 9000);
                    s.Check(hauled, "the player's pawn should be able to haul the corpse of " + corpse.InnerPawn.Faction.Name + "/" + corpse.InnerPawn.LabelShort + " from " + start + " toward " + target + "; it lies at " + corpse.PositionHeld + ", forbidden " + corpse.IsForbidden(Faction.OfPlayer));
                }
            } finally {
                PlayerSideHelpers.Finish(s, run);
            }
        }

        static Quest StartQuest(string name, string signalPart, QuestEndOutcome outcome, WorldObject site) {
            var quest = Quest.MakeRaw();
            quest.name = name;
            quest.root = DefDatabase<QuestScriptDef>.GetNamed("Util_Raid");
            var tag = "Quest" + quest.id + ".FactionalWarSite";
            quest.AddPart(new QuestPart_QuestEnd { inSignal = tag + "." + signalPart, outcome = outcome, sendLetter = false, playSound = false });
            quest.SetInitiallyAccepted();
            Find.QuestManager.Add(quest);
            QuestUtility.AddQuestTag(ref site.questTags, tag);
            return quest;
        }

        [Scenario("player-side-caravan-leaves-the-site-with-its-pawns-and-quests-tied-to-the-site-complete-and-fail", MapSize = 120, MaxSeconds = 70, OnlyOn = "1.7")]
        static void CaravanAndQuests(ScenarioContext s) {
            var run = PlayerSideHelpers.Start(s, Kinds[1], false);
            var quests = new List<Quest>();
            try {
                if (run == null || run.Sides.Count != 2) return;
                PlayerSideHelpers.ChooseSides(s, run, 50);
                var victory = StartQuest("Factional War test: win at the site", "AllEnemiesDefeated", QuestEndOutcome.Success, run.Site);
                var lost = StartQuest("Factional War test: the site is gone", "Destroyed", QuestEndOutcome.Fail, run.Site);
                quests.Add(victory);
                quests.Add(lost);
                s.AreEqual(QuestState.Ongoing, victory.State, "setup: the first quest should be running");
                s.AreEqual(QuestState.Ongoing, lost.State, "setup: the second quest should be running");

                var reform = PlayerSideHelpers.ReformCommand(run.Site);
                s.Check(reform != null, "the site should offer the reform caravan command while the player's pawn is on it");
                if (reform == null) return;
                s.Check(reform.Disabled, "the reform caravan command should be disabled while the hostile side is on the map");

                PlayerSideHelpers.KillSide(run.Map, run.B, run.Colonist);
                var friendlyLords = run.Map.lordManager.lords.Count(l => l.faction == run.A && l.ownedPawns.Any(BattleHelpers.Standing));
                s.Check(friendlyLords > 0, "right after the hostile side died the friendly side should still have a lord with standing pawns on the map; it has " + friendlyLords);
                reform = PlayerSideHelpers.ReformCommand(run.Site);
                s.Check(reform != null && reform.Disabled && reform.disabledReason == "SrCommandReformCaravanFailAlliesCollectingLoot".Translate().ToString(), "while the friendly side is still on the map the reform command should be disabled with the allies-collecting-loot text; it is " + (reform == null ? "missing" : reform.Disabled ? "disabled with '" + reform.disabledReason + "'" : "enabled"));

                s.Check(s.RunUntil(() => victory.State != QuestState.Ongoing, 3000), "the quest tied to the site should end once no enemy of the player is left on it");
                s.AreEqual(QuestState.EndedSuccess, victory.State, "the quest that waits for the enemies to be defeated should end in success");
                s.AreEqual(QuestState.Ongoing, lost.State, "the quest that waits for the site to be gone should still be running");

                s.Check(s.RunUntil(() => PlayerSideHelpers.StandingOf(run.Map, run.A) == 0, 8000), "the friendly side should leave the map by itself; still standing: " + PlayerSideHelpers.StandingOf(run.Map, run.A));
                reform = PlayerSideHelpers.ReformCommand(run.Site);
                s.Check(reform != null && !reform.Disabled, "once the sides are gone the reform command should be enabled; it is " + (reform == null ? "missing" : "disabled with '" + reform.disabledReason + "'"));

                var name = run.Colonist.LabelShort;
                run.Caravan = BattleHelpers.LeaveBy(run.Map);
                Current.Game.CurrentMap = s.Map;
                s.Check(run.Caravan != null && !run.Caravan.Destroyed && Find.WorldObjects.Caravans.Contains(run.Caravan), "the caravan formed on the battle map should be on the world map");
                s.Check(run.Caravan != null && run.Caravan.PawnsListForReading.Any(p => p.LabelShort == name && !p.Dead), "the caravan should hold the player's pawn " + name + "; it holds " + (run.Caravan == null ? "nothing" : string.Join(", ", run.Caravan.PawnsListForReading.Select(p => p.LabelShort))));

                s.Check(s.RunUntil(() => run.Site.Destroyed, 3000), "the finished battle site should be gone once the player has left it");
                s.AreEqual(QuestState.EndedFailed, lost.State, "the quest that waits for the site to be gone should have failed");
                s.Check(run.Caravan != null && !run.Caravan.Destroyed && run.Caravan.PawnsListForReading.Any(p => p.LabelShort == name), "the caravan and its pawn should survive the site being removed");
            } finally {
                foreach (var quest in quests)
                    if (Find.QuestManager.Contains(quest)) Find.QuestManager.Remove(quest);
                PlayerSideHelpers.Finish(s, run);
            }
        }

        [Scenario("player-side-helping-a-side-with-an-allied-faction-also-at-the-site-keeps-the-battle-going-by-name", MapSize = 110, MaxSeconds = 80, OnlyOn = "1.7")]
        static void HelpWithAnAlly(ScenarioContext s) {
            var run = PlayerSideHelpers.Start(s, Kinds[1], false);
            Faction third = null;
            var thirdGoodwill = 0;
            var thirdKind = FactionRelationKind.Neutral;
            try {
                if (run == null || run.Sides.Count != 2) return;
                PlayerSideHelpers.ChooseSides(s, run, 50);
                third = Find.FactionManager.AllFactionsVisible.First(f => !f.IsPlayer && f != run.A && f != run.B && f.def.humanlikeFaction && !f.def.permanentEnemy);
                thirdGoodwill = third.GoodwillWith(run.A);
                thirdKind = third.RelationKindWith(run.A);
                third.TryAffectGoodwillWith(run.A, 200, false, false);
                PlayerSideHelpers.SetGoodwill(third, 100);
                var ally = PawnGenerator.GeneratePawn(new PawnGenerationRequest(third.RandomPawnKind(), third));
                GenSpawn.Spawn(ally, CellFinder.RandomClosewalkCellNear(run.Sides[run.A].First(p => p.Spawned).Position, run.Map, 10), run.Map);
                LordMaker.MakeNewLord(third, new LordJob_AssistColony(third, ally.Position), run.Map, new[] { ally });
                var allyName = ally.LabelShort;
                var sideAPawns = run.Sides[run.A].ToList();

                var watch = new PlayerSideHelpers.Watch();
                var ticks = 0;
                while (ticks < 6000) {
                    s.Tick(10);
                    ticks += 10;
                    PlayerSideHelpers.Observe(watch, sideAPawns, new[] { ally }, ticks);
                }
                s.Check(PlayerSideHelpers.StandingOf(run.Map, run.A) > 0 && PlayerSideHelpers.StandingOf(run.Map, run.B) > 0, "after " + ticks + " ticks with the player's ally on the map both sides should still be fighting; standing " + PlayerSideHelpers.StandingOf(run.Map, run.A) + " and " + PlayerSideHelpers.StandingOf(run.Map, run.B));
                var allyDeadBeforeHelp = ally.Dead;

                var friendly = sideAPawns.First(p => p.RaceProps.Humanlike && BattleHelpers.Standing(p) && p.Spawned);
                HealthUtility.DamageUntilDowned(friendly, false);
                TendUtility.DoTend(run.Colonist, friendly, null);
                var aBefore = run.A.PlayerGoodwill;
                var gain = run.A.GetGoodwillGainForExit(friendly, false, out _, out _);
                PlayerSideHelpers.KillHostile(run.Map, run.Colonist);

                while (ticks < 40000 && !friendly.IsWorldPawn()) {
                    s.Tick(10);
                    ticks += 10;
                    PlayerSideHelpers.Observe(watch, sideAPawns, new[] { ally }, ticks);
                }
                s.Check(friendly.IsWorldPawn() && !friendly.Dead, "the helped side should carry its tended pawn " + friendly.LabelShort + " off the map with an ally of the player on it");
                s.AreEqual(aBefore + gain, run.A.PlayerGoodwill, "the helped side's goodwill should rise by what the game said, with the allied faction on the map too");
                s.Check(watch.Closest <= 40f, "the helped side should have come within 40 cells of the player's ally " + allyName + ", so the peace between them is tested; closest was " + watch.Closest);
                s.AreEqual(0, watch.Attacks.Count, "the helped side should not target the player's ally " + allyName + "; " + string.Join(", ", watch.Attacks.Take(5)));
                s.AreEqual(allyDeadBeforeHelp, ally.Dead, "nothing should kill the player's ally " + allyName + " once the hostile pawns are gone; dead before the help " + allyDeadBeforeHelp + ", after " + ally.Dead);
                s.Check(ally.GetLord() == null || ally.GetLord().LordJob.GetType().Namespace != "SR.ModRimWorld.FactionalWar", "the mod should not put the player's ally " + allyName + " into one of its battle lords; its lord is " + ally.GetLord()?.LordJob.GetType().FullName);
                var lostA = sideAPawns.Where(p => !p.Dead && !p.Spawned && !p.IsWorldPawn()).Select(p => p.LabelShort).ToList();
                s.AreEqual(0, lostA.Count, "no pawn of the helped side should vanish; lost by name: " + string.Join(", ", lostA));

                s.Check(s.RunUntil(() => PlayerSideHelpers.StandingOf(run.Map, run.A) == 0, 8000), "the helped side should finish the battle and leave; still standing: " + string.Join("; ", run.Map.mapPawns.SpawnedPawnsInFaction(run.A).Where(BattleHelpers.Standing).Select(p => p.LabelShort + " job " + p.CurJob?.def.defName + " duty " + p.mindState.duty?.def.defName + " toil " + p.GetLord()?.CurLordToil?.GetType().Name + " lord " + (p.GetLord() == null ? "none" : p.GetLord().LordJob.GetType().Name) + " at " + p.Position)));
                var reform = PlayerSideHelpers.ReformCommand(run.Site);
                s.Check(reform != null && !reform.Disabled, "with the sides gone and an ally still there, the reform command should be enabled; it is " + (reform == null ? "missing" : "disabled with '" + reform.disabledReason + "'"));
            } finally {
                PlayerSideHelpers.Finish(s, run);
                if (third != null && run != null && run.A != null) {
                    third.TryAffectGoodwillWith(run.A, thirdGoodwill - third.GoodwillWith(run.A), false, false);
                    third.RelationWith(run.A).kind = thirdKind;
                    run.A.RelationWith(third).kind = thirdKind;
                }
            }
        }

        [Scenario("player-side-bystander-assault-on-the-home-map-nobody-at-peace-attacks-and-relations-stay", MapSize = 120, MaxSeconds = 120, OnlyOn = "1.7")]
        static void BystanderAssault(ScenarioContext s) {
            var snapshot = PlayerSideHelpers.SnapshotRelations();
            try {
                foreach (var f in snapshot.Keys) PlayerSideHelpers.SetGoodwill(f, 100);
                s.SpawnColonist(s.Cell(0, 0), "Bystander");
                var colonists = s.Map.mapPawns.FreeColonistsSpawned.ToList();

                List<Faction> Fire() {
                    for (var attempt = 0; attempt < 6; attempt++) {
                        var fired = DefDatabase<IncidentDef>.GetNamed("SrFactionWar").Worker.TryExecute(new IncidentParms { target = s.Map, points = 1500f });
                        s.Check(fired, "the Faction Assault incident should fire");
                        var found = BattleHelpers.Combatants(s.Map).Select(p => p.Faction).Distinct().ToList();
                        s.AreEqual(2, found.Count, "two NPC factions should fight; found: " + BattleHelpers.Names(found));
                        if (found.Count != 2 || found.Any(f => !f.def.permanentEnemy)) return found;
                        foreach (var pawn in BattleHelpers.Combatants(s.Map).ToList()) pawn.Destroy();
                    }
                    s.Fail("six assaults in a row paired two factions that can never be at peace with the player");
                    return null;
                }

                var controlSides = Fire();
                if (controlSides == null || controlSides.Count != 2) return;
                var proven = new List<string>();
                foreach (var side in controlSides.Where(f => !PlayerSideHelpers.Hostile(f))) {
                    PlayerSideHelpers.PlaceNear(colonists[0], s.Map.mapPawns.SpawnedPawnsInFaction(side).First(), s.Map, 12f);
                    if (PlayerSideHelpers.HostileControlTargets(s, s.Map, side, colonists, 1500).Attacks.Count > 0) proven.Add(side.def.defName);
                    if (!colonists[0].Spawned || colonists[0].Dead) break;
                }
                s.Check(proven.Count > 0, "control: on an assault, with a side made hostile and its nearest pawn hit by a colonist standing 12 cells away, a pawn should target the colonist within 1500 ticks; otherwise the peaceful run below proves nothing");
                foreach (var pawn in BattleHelpers.Combatants(s.Map).ToList()) pawn.Destroy();
                foreach (var f in snapshot.Keys) PlayerSideHelpers.SetGoodwill(f, 100);
                if (!colonists[0].Spawned) return;

                var sides = Fire();
                if (sides == null || sides.Count != 2) return;
                var friendly = sides.Where(f => !PlayerSideHelpers.Hostile(f)).ToList();
                s.Check(friendly.Count > 0, "setup: at least one side should be at peace with the player; the game says " + string.Join(", ", sides.Select(f => f.Name + " " + f.PlayerRelationKind)));
                if (friendly.Count == 0) return;
                PlayerSideHelpers.PlaceNear(colonists[0], s.Map.mapPawns.SpawnedPawnsInFaction(friendly[0]).First(), s.Map, 12f);
                var start = sides.ToDictionary(f => f, f => (f.PlayerGoodwill, f.PlayerRelationKind));
                var watches = friendly.ToDictionary(f => f, f => new PlayerSideHelpers.Watch());
                var ticks = 0;
                while (ticks < 30000 && sides.All(f => PlayerSideHelpers.StandingOf(s.Map, f) > 0)) {
                    s.Tick(10);
                    ticks += 10;
                    foreach (var side in friendly)
                        PlayerSideHelpers.Observe(watches[side], s.Map.mapPawns.SpawnedPawnsInFaction(side), colonists, ticks);
                }
                s.Check(sides.Any(f => PlayerSideHelpers.StandingOf(s.Map, f) == 0), "the battle should end with one side gone within 30000 ticks");
                s.Metric("assault closest peaceful pawn to a colonist", watches.Values.Min(w => w.Closest));
                foreach (var side in friendly)
                    s.AreEqual(0, watches[side].Attacks.Count, side.Name + " is at peace with the player and should not target the player's colonists (closest it came: " + watches[side].Closest + " cells); " + string.Join(", ", watches[side].Attacks.Take(5)));
                foreach (var f in sides) {
                    s.AreEqual(start[f].PlayerGoodwill, f.PlayerGoodwill, "the player's goodwill with " + f.Name + " should be what the game reported before the battle");
                    s.AreEqual(start[f].PlayerRelationKind, f.PlayerRelationKind, "the player's relation with " + f.Name + " should be what the game reported before the battle");
                }
            } finally {
                PlayerSideHelpers.RestoreRelations(snapshot);
            }
        }
    }
}
