using System;

namespace World.Sim.Encounters.Ward
{
    public enum Essence { Flame = 0, Frost = 1, Vine = 2, Light = 3 }

    public static class EssenceInfo
    {
        public static string DisplayName(Essence e)
        {
            switch (e)
            {
                case Essence.Flame: return "Flame";
                case Essence.Frost: return "Frost";
                case Essence.Vine: return "Vine";
                case Essence.Light: return "Light";
                default: throw new ArgumentOutOfRangeException(nameof(e));
            }
        }

        public static char Symbol(Essence e)
        {
            switch (e)
            {
                case Essence.Flame: return 'F';
                case Essence.Frost: return 'I';
                case Essence.Vine: return 'V';
                case Essence.Light: return 'L';
                default: throw new ArgumentOutOfRangeException(nameof(e));
            }
        }
    }
}
