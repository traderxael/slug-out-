namespace SlugOut.Core
{
    public enum ElementType
    {
        Fire,       // Fuego (Infierno, Bengala)
        Water,      // Agua (Aquabeek)
        Earth,      // Tierra / Roca (Demoledora, Carnero)
        Air,        // Aire / Viento (Tornado, Planeadora)
        Electric,   // Electrico (Electroshock, Amperling)
        Ice,        // Hielo (Congelada, Esquirla Helada)
        Plant,      // Planta (Tejedora)
        Energy,     // Energia pura (Fandango, Enigma)
        Shadow,     // Ghoul / Sombra (Dark Furnace, Negablade)
        Light,      // Sagrada / Curativa (Sanadora Doc, Sanadora Blanca)
        Toxic       // Venenosa (Neotox)
    }

    public enum SlugAbilityType
    {
        DirectDamage,
        Heal,
        Shield,
        Freeze,
        BurnOverTime,
        Stun,
        EnergyDrain
    }

    public static class ElementalChart
    {
        public static float GetMultiplier(ElementType attacker, ElementType defender)
        {
            switch (attacker)
            {
                case ElementType.Fire:
                    if (defender == ElementType.Ice || defender == ElementType.Plant) return 1.5f;
                    if (defender == ElementType.Water) return 0.75f;
                    break;

                case ElementType.Water:
                    if (defender == ElementType.Fire || defender == ElementType.Earth) return 1.5f;
                    if (defender == ElementType.Electric || defender == ElementType.Plant) return 0.75f;
                    break;

                case ElementType.Ice:
                    if (defender == ElementType.Water || defender == ElementType.Plant) return 1.5f;
                    if (defender == ElementType.Fire) return 0.75f;
                    break;

                case ElementType.Earth:
                    if (defender == ElementType.Electric) return 1.5f;
                    if (defender == ElementType.Water) return 0.75f;
                    break;

                case ElementType.Electric:
                    if (defender == ElementType.Water) return 1.5f;
                    if (defender == ElementType.Earth) return 0.75f;
                    break;

                case ElementType.Air:
                    if (defender == ElementType.Plant || defender == ElementType.Shadow) return 1.5f;
                    break;

                case ElementType.Energy:
                case ElementType.Light:
                    if (defender == ElementType.Shadow) return 1.75f;
                    break;

                case ElementType.Shadow:
                    if (defender == ElementType.Light || defender == ElementType.Energy) return 1.5f;
                    break;
            }

            return 1.0f;
        }

        /// Anillo al reves: lo que vencía pierde y lo que perdía vence. Se invierte el
        /// multiplicador en vez de reescribir la tabla, para que el nivel 25 use la misma
        /// ley que el 1 y el jugador solo tenga que aprender una cosa nueva.
        public static float GetMultiplier(ElementType attacker, ElementType defender, bool anilloInvertido)
        {
            float m = GetMultiplier(attacker, defender);
            return anilloInvertido && m != 1.0f ? 1.0f / m : m;
        }
    }
}
