using UnityEngine;

namespace SlugOut.World
{
    /// Geometria pura de la ruta de campana: 28 niveles en cinco bloques (6+6+6+6+4, el ultimo
    /// bloque queda corto porque el clamp manda los niveles restantes a la ultima region).
    /// La usa EXPLORAR para dibujar encabezados por cueva y la tira de nodos sin repetir formulas
    /// de scroll en el HUD.
    public static class CampanaMapa
    {
        public const int NivelesPorRegion = 6;
        public const float AltoFila = 60f;
        public const float AltoEncabezado = 26f;

        public static int RegionDeNivel(int nivel)
        {
            return Mathf.Clamp((Mathf.Max(1, nivel) - 1) / NivelesPorRegion, 0,
                Regiones.Todas.Length - 1);
        }

        public static int PrimerNivel(int region)
        {
            return Mathf.Clamp(region, 0, Regiones.Todas.Length - 1) * NivelesPorRegion + 1;
        }

        public static int UltimoNivel(int region)
        {
            return PrimerNivel(region) + NivelesPorRegion - 1;
        }

        public static int Completados(int region, int desbloqueado)
        {
            return Mathf.Clamp(desbloqueado - PrimerNivel(region), 0, NivelesPorRegion);
        }

        /// Nivel que selecciona la tira de nodos: cae dentro de la region y nunca desbloquea.
        public static int NivelInicial(int region, int desbloqueado)
        {
            int baseNivel = PrimerNivel(region);
            return Mathf.Clamp(desbloqueado, baseNivel, baseNivel + NivelesPorRegion - 1);
        }

        public static float YDeNivel(int nivel)
        {
            int region = RegionDeNivel(nivel);
            // El primer encabezado ocupa la fila 0; sin el desplazamiento quedaba fuera del
            // scroll (y=-26) y la primera cueva no tenia titulo.
            return (nivel - 1) * AltoFila + region * AltoEncabezado + AltoEncabezado;
        }

        public static float YDeEncabezado(int region)
        {
            return YDeNivel(PrimerNivel(region)) - AltoEncabezado;
        }

        public static float AlturaContenido(int totalNiveles)
        {
            int regiones = Mathf.Clamp(RegionDeNivel(totalNiveles) + 1, 1,
                Regiones.Todas.Length);
            return totalNiveles * AltoFila + regiones * AltoEncabezado;
        }
    }
}
