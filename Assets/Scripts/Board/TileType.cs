using UnityEngine;
using SlugOut.Core;

namespace SlugOut.Board
{
    public enum TileType
    {
        Fire,       // Carga babosas de Fuego
        Water,      // Carga babosas de Agua
        Earth,      // Carga babosas de Tierra
        Electric,   // Carga babosas de Electricidad
        Heart,      // Curacion directa
        EnergyBoost // Carga para todas las babosas
    }

    public static class TileHelper
    {
        public static Color GetTileColor(TileType type)
        {
            switch (type)
            {
                case TileType.Fire: return new Color(1f, 0.28f, 0.15f);
                case TileType.Water: return new Color(0.15f, 0.55f, 1f);
                case TileType.Earth: return new Color(0.85f, 0.55f, 0.2f);
                case TileType.Electric: return new Color(1f, 0.9f, 0.1f);
                case TileType.Heart: return new Color(0.25f, 0.95f, 0.45f);
                case TileType.EnergyBoost: return new Color(0.85f, 0.3f, 1f);
                default: return Color.white;
            }
        }

        public static ElementType ToElementType(TileType type)
        {
            switch (type)
            {
                case TileType.Fire: return ElementType.Fire;
                case TileType.Water: return ElementType.Water;
                case TileType.Earth: return ElementType.Earth;
                case TileType.Electric: return ElementType.Electric;
                default: return ElementType.Energy;
            }
        }
    }
}
