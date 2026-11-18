// ******************************************************************
//       /\ /|       @file       SettingModel.cs
//       \ V/        @brief      模组设置页面数据模型
//       | "")       @author     Shadowrabbit, yingtu0401@gmail.com
//       /  |                    
//      /  \\        @Modified   2021-08-21 05:14:08
//    *(__\_\        @Copyright  Copyright (c) 2021, Shadowrabbit
// ******************************************************************

using Verse;

namespace SR.ModRimWorld.FactionalWar
{
    public class SettingModel : ModSettings
    {
        public float threatPointFactor = BattleScale.DefaultThreatPointFactor;
        public bool needOptimization = BattleScale.DefaultNeedOptimization;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref threatPointFactor, "threatPointFactor", BattleScale.DefaultThreatPointFactor);
            Scribe_Values.Look(ref needOptimization, "needOptimization", BattleScale.DefaultNeedOptimization);
        }

        public void SetDefault()
        {
            threatPointFactor = BattleScale.DefaultThreatPointFactor;
            needOptimization = BattleScale.DefaultNeedOptimization;
        }
    }
}