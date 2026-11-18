using System;

namespace SR.ModRimWorld.FactionalWar
{
    public static class BattleScale
    {
        public const float DefaultThreatPointFactor = 1f;
        public const bool DefaultNeedOptimization = true;
        public const float MinThreatPointFactor = 0.1f;
        public const float MaxThreatPointFactor = 2f;

        public static float Points(float points, float threatPointFactor)
        {
            if (!(threatPointFactor >= MinThreatPointFactor && threatPointFactor <= MaxThreatPointFactor))
            {
                throw new ArgumentOutOfRangeException(nameof(threatPointFactor), threatPointFactor,
                    $"the threat point factor must be between {MinThreatPointFactor} and {MaxThreatPointFactor}");
            }

            return points * threatPointFactor;
        }
    }
}
