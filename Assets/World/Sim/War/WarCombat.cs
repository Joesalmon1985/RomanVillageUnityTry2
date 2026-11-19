namespace World.Sim.War
{
    /// <summary>WA-3: the look of a soldier follows its highest stat level; Warrior when no single stat leads.</summary>
    public enum SoldierLook { Warrior = 0, Brute = 1, Raider = 2, Shieldbearer = 3, Skirmisher = 4 }

    /// <summary>WA-9 stand-in for a health bar: a coarse wear state the view turns into animation.</summary>
    public enum Wear { Fresh = 0, Hurt = 1, BadlyHurt = 2 }

    /// <summary>Integer stats of one fighter (WA-2). DefencePct and EvadePct are whole percentages.</summary>
    public struct Combatant
    {
        public int Health, Attack, DefencePct, EvadePct;
        public Combatant(int health, int attack, int defencePct, int evadePct)
        {
            Health = health; Attack = attack; DefencePct = defencePct; EvadePct = evadePct;
        }
    }

    /// <summary>The pure duel functions (WA-8). Integer and seeded.</summary>
    public static class DuelRules
    {
        /// <summary>One swing: 0 if the target evades (Evade %), else Attack x (100 - Defence) / 100.</summary>
        public static int Swing(Combatant attacker, Combatant target, Rng rng, out bool hit)
        {
            hit = !rng.Chance(target.EvadePct, 100);
            if (!hit) return 0;
            return attacker.Attack * (100 - target.DefencePct) / 100;
        }

        /// <summary>
        /// Plays a whole duel, a swinging first, then alternating, until one is at 0 health. Returns 0 if a wins, 1 if b wins;
        /// hpA and hpB are the health left. Stops after 100000 swings as a guard (then the one with more health wins).
        /// </summary>
        public static int Resolve(Combatant a, Combatant b, Rng rng, out int hpA, out int hpB)
        {
            hpA = a.Health; hpB = b.Health;
            for (int i = 0; i < 100000; i++)
            {
                if ((i & 1) == 0)
                {
                    hpB -= Swing(a, b, rng, out _);
                    if (hpB <= 0) { hpB = 0; return 0; }
                }
                else
                {
                    hpA -= Swing(b, a, rng, out _);
                    if (hpA <= 0) { hpA = 0; return 1; }
                }
            }
            return hpA >= hpB ? 0 : 1;
        }

        /// <summary>Fresh above 60 % of full health, Hurt above 30 %, else BadlyHurt.</summary>
        public static Wear WearOf(int hp, int maxHp)
        {
            if (maxHp <= 0) return Wear.BadlyHurt;
            if (hp * 100 > maxHp * 60) return Wear.Fresh;
            if (hp * 100 > maxHp * 30) return Wear.Hurt;
            return Wear.BadlyHurt;
        }
    }
}
