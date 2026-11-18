// ******************************************************************
//       /\ /|       @file       IncidentWorkerFactionWarTempCampSiteGenerate.cs
//       \ V/        @brief      事件处理 生成临时营地场地
//       | "")       @author     Shadowrabbit, yingtu0401@gmail.com
//       /  |                    
//      /  \\        @Modified   2021-06-24 18:47:29
//    *(__\_\        @Copyright  Copyright (c) 2021, Shadowrabbit
// ******************************************************************

using System.Collections.Generic;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SR.ModRimWorld.FactionalWar
{
    [UsedImplicitly]
    public class IncidentWorkerFactionWarTempCampSiteGenerate : IncidentWorker
    {
        private const int MinDist = 2; //生成在玩家最近几格
        private const int MaxDist = 7; //生成在玩家最远几格

        private static bool TryFindSides(out Faction camp, out Faction attacker)
        {
            return FactionUtil.TryFindCampSides(SymbolResolverTempCamp.DefaultPawnsPoints.min,
                SiteTempCamp.ThreatPoints.min, out camp, out attacker);
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return TryFindSides(out _, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!TileFinder.TryFindNewSiteTile(out var tileId, MinDist, MaxDist))
            {
                Log.Warning("[SR.ModRimWorld.FactionalWar]no free world tile for a Faction Defense site");
                return false;
            }

            if (!TryFindSides(out var camp, out var attacker))
            {
                Log.Warning("[SR.ModRimWorld.FactionalWar]no hostile faction has an enemy that can assault its camp");
                return false;
            }

            var sitePartList = new List<SitePartDef> {SitePartDefOf.SrFactionTempCamp};
            //生成默认部分场地参数
            SiteMakerHelper.GenerateDefaultParams(parms.points, tileId, camp, sitePartList,
                out var sitePartDefsWithParams);
            //场地
            var site = WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.SrSiteFactionWarTempCamp);
            if (!(site is SiteTempCamp siteFactionWarTempCamp))
            {
                Log.Error("[SR.ModRimWorld.FactionalWar]site is not type of SiteTempCamp");
                return false;
            }
            siteFactionWarTempCamp.Tile = tileId;
            siteFactionWarTempCamp.factionMustRemainHostile = false;
            siteFactionWarTempCamp.SetFaction(camp);
            siteFactionWarTempCamp.SetSides(camp, attacker);
            if (sitePartDefsWithParams != null)
            {
                foreach (var sitePart in sitePartDefsWithParams)
                {
                    siteFactionWarTempCamp.AddPart(new SitePart(siteFactionWarTempCamp, sitePart.def,
                        sitePart.parms));
                }
            }

            siteFactionWarTempCamp.desiredThreatPoints = siteFactionWarTempCamp.ActualThreatPoints;
            Find.WorldObjects.Add(siteFactionWarTempCamp);
            //中立信件通知
            var letter = LetterMaker.MakeLetter("SrLabelFactionWarTempCamp".Translate(),
                "SrDescFactionWarTempCamp".Translate(),
                parms.customLetterDef ?? LetterDefOf.NeutralEvent,
                siteFactionWarTempCamp,
                parms.faction, parms.quest, parms.letterHyperlinkThingDefs);
            Find.LetterStack.ReceiveLetter(letter);
            return true;
        }
    }
}
