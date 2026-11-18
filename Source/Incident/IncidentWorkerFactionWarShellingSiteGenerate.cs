// ******************************************************************
//       /\ /|       @file       IncidentWorkerFactionWarShellingSiteGenerate.cs
//       \ V/        @brief      事件处理 派系炮击战 场地生成
//       | "")       @author     Shadowrabbit, yingtu0401@gmail.com
//       /  |                    
//      /  \\        @Modified   2021-06-23 09:52:28
//    *(__\_\        @Copyright  Copyright (c) 2021, Shadowrabbit
// ******************************************************************
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SR.ModRimWorld.FactionalWar
{
    [UsedImplicitly]
    public class IncidentWorkerFactionWarShellingSiteGenerate : IncidentWorker
    {
        private const int MinDist = 2; //生成在玩家最近几格
        private const int MaxDist = 7; //生成在玩家最远几格

        private static bool TryFindSides(out Faction faction1, out Faction faction2)
        {
            FactionUtil.GetHostileFactionPair(out faction1, out faction2, SiteFactionWarShelling.ThreatPoints.min,
                PawnGroupKindDefOf.Combat, Find.FactionManager.AllFactionsVisible.ToList(),
                SiteFactionWarShelling.CanFight);
            return faction1 != null && faction2 != null;
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return TryFindSides(out _, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!TileFinder.TryFindNewSiteTile(out var tileId, MinDist, MaxDist))
            {
                Log.Warning("[SR.ModRimWorld.FactionalWar]no free world tile for a Faction Bombardment site");
                return false;
            }

            if (!TryFindSides(out var faction1, out var faction2))
            {
                Log.Warning("[SR.ModRimWorld.FactionalWar]no two hostile factions can stage a Faction Bombardment");
                return false;
            }

            var sitePartList = new List<SitePartDef> {SitePartDefOf.SrFactionWarShelling};
            //生成默认部分场地参数
            SiteMakerHelper.GenerateDefaultParams(parms.points, tileId, null, sitePartList,
                out var sitePartDefsWithParams);
            //场地
            var siteFactionWarShelling = (SiteFactionWarShelling)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf
                .SrSiteFactionWarShelling);
            siteFactionWarShelling.Tile = tileId;
            siteFactionWarShelling.SetSides(faction1, faction2);
            siteFactionWarShelling.factionMustRemainHostile = false;
            if (sitePartDefsWithParams != null)
            {
                foreach (var sitePart in sitePartDefsWithParams)
                    siteFactionWarShelling.AddPart(new SitePart(siteFactionWarShelling, sitePart.def, sitePart.parms));
            }

            siteFactionWarShelling.desiredThreatPoints = siteFactionWarShelling.ActualThreatPoints;
            Find.WorldObjects.Add(siteFactionWarShelling);
            //中立信件通知
            var letter = LetterMaker.MakeLetter("SrLabelFactionWarShelling".Translate(),
                "SrDescFactionWarShelling".Translate(),
                parms.customLetterDef ?? LetterDefOf.NeutralEvent,
                siteFactionWarShelling,
                parms.faction, parms.quest, parms.letterHyperlinkThingDefs);
            Find.LetterStack.ReceiveLetter(letter);
            return true;
        }
    }
}
