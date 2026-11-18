// ******************************************************************
//       /\ /|       @file       IncidentWorkerFactionWarContentionSiteGenerate.cs
//       \ V/        @brief      事件处理 派系争夺 场地生成
//       | "")       @author     Shadowrabbit, yingtu0401@gmail.com
//       /  |                    
//      /  \\        @Modified   2021-06-24 09:03:56
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
    public class IncidentWorkerFactionWarContentionSiteGenerate : IncidentWorker
    {
        private const int MinDist = 2; //生成在玩家最近几格
        private const int MaxDist = 7; //生成在玩家最远几格

        private static bool TryFindSides(out Faction faction1, out Faction faction2)
        {
            FactionUtil.GetHostileFactionPair(out faction1, out faction2, SiteFactionWarContention.FactionPoints.min,
                PawnGroupKindDefOf.Combat, Find.FactionManager.AllFactionsVisible.ToList(),
                SiteFactionWarContention.CanFight);
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
                Log.Warning("[SR.ModRimWorld.FactionalWar]no free world tile for a Faction Contention site");
                return false;
            }

            if (!TryFindSides(out var faction1, out var faction2))
            {
                Log.Warning("[SR.ModRimWorld.FactionalWar]no two factions hostile to the player and to each other can contend for a Faction Contention");
                return false;
            }

            var sitePartList = new List<SitePartDef> {SitePartDefOf.SrFactionContention};
            //生成默认部分场地参数
            SiteMakerHelper.GenerateDefaultParams(parms.points, tileId, null, sitePartList,
                out var sitePartDefsWithParams);
            //场地
            var siteFactionWarContention = (SiteFactionWarContention) WorldObjectMaker.MakeWorldObject(WorldObjectDefOf
                .SrSiteFactionWarContention);
            siteFactionWarContention.Tile = tileId;
            siteFactionWarContention.SetSides(faction1, faction2);
            siteFactionWarContention.factionMustRemainHostile = false;
            if (sitePartDefsWithParams != null)
            {
                foreach (var sitePart in sitePartDefsWithParams)
                {
                    siteFactionWarContention.AddPart(new SitePart(siteFactionWarContention, sitePart.def,
                        sitePart.parms));
                }
            }

            siteFactionWarContention.desiredThreatPoints = siteFactionWarContention.ActualThreatPoints;
            Find.WorldObjects.Add(siteFactionWarContention);
            //中立信件通知
            var letter = LetterMaker.MakeLetter("SrLabelFactionWarContention".Translate(),
                "SrDescFactionWarContention".Translate(),
                parms.customLetterDef ?? LetterDefOf.NeutralEvent,
                siteFactionWarContention,
                parms.faction, parms.quest, parms.letterHyperlinkThingDefs);
            Find.LetterStack.ReceiveLetter(letter);
            return true;
        }
    }
}