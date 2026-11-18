using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace SR.ModRimWorld.FactionalWar
{
    public static class KillTargetUtil
    {
        public static bool AnyLordPawnCanReach(Lord lord, Pawn target)
        {
            foreach (var hunter in lord.ownedPawns)
            {
                if (hunter.Spawned && !hunter.Downed && hunter.CanReach(target, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
