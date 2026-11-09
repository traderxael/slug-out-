using UnityEngine;

namespace SlugOut.World
{
    /// Una sola puerta al ruido procedural del juego. Cada dominio tiene su semilla y su
    /// frecuencia, para que el terreno, las rocas y el techo no compartan el mismo dibujo y
    /// para que tocar uno no mueva el otro.
    ///
    /// El motor es FastNoiseLite (MIT, Jordan Peck): port C# de un solo archivo, sin build ni
    /// dependencias, en `Assets/Scripts/World/FastNoiseLite.cs`. Se eligio porque el terreno
    /// tenia su propio ruido de valor a mano (hash entero + bilineal) y no tenia forma de
    /// subir a FBM, domain warp ni cellular sin reescribir la funcion a mano otra vez.
    ///
    /// Las frecuencias van por metro. El detalle del terreno no puede pasar de ~0.37: la malla
    /// del terreno muestrea cada 0.67 m, y una onda mas corta que cuatro muestras no se ve,
    /// solo se aliasea.
    public static class RuidoMundo
    {
        public const int SemillaDetalle = 20261029;
        public const int SemillaRoca = 613;
        public const int SemillaTecho = 1207;

        public const float FrecuenciaDetalle = 0.22f;
        public const float FrecuenciaRoca = 0.12f;
        public const float FrecuenciaTecho = 0.09f;

        private static FastNoiseLite _detalle;
        private static FastNoiseLite _roca;
        private static FastNoiseLite _techo;

        /// Detalle de alta frecuencia del piso: FBm de dos octavas sobre OpenSimplex2.
        public static FastNoiseLite DetalleTerreno
        {
            get
            {
                if (_detalle == null) _detalle = Crear(SemillaDetalle, FrecuenciaDetalle, 2);
                return _detalle;
            }
        }

        /// Variacion de las rocas: 3D, para que la forma no se repita al girar.
        public static FastNoiseLite Roca
        {
            get
            {
                if (_roca == null) _roca = Crear(SemillaRoca, FrecuenciaRoca, 3);
                return _roca;
            }
        }

        /// Ondulacion del manto de la cueva: mas lenta que el detalle del piso.
        public static FastNoiseLite Techo
        {
            get
            {
                if (_techo == null) _techo = Crear(SemillaTecho, FrecuenciaTecho, 2);
                return _techo;
            }
        }

        private static FastNoiseLite Crear(int semilla, float frecuencia, int octavas)
        {
            var n = new FastNoiseLite();
            n.SetSeed(semilla);
            n.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            n.SetFractalType(FastNoiseLite.FractalType.FBm);
            n.SetFractalOctaves(octavas);
            n.SetFractalLacunarity(2f);
            n.SetFractalGain(0.5f);
            n.SetFrequency(frecuencia);
            return n;
        }

        /// Detalle del piso en [-1, 1]. Mismo punto, mismo valor: es determinista.
        public static float Detalle(float x, float z) { return DetalleTerreno.GetNoise(x, z); }

        /// Variacion de roca en [-1, 1].
        public static float Roca3D(float x, float y, float z) { return Roca.GetNoise(x, y, z); }

        /// Ondulacion del techo en [-1, 1].
        public static float Techo2D(float x, float z) { return Techo.GetNoise(x, z); }
    }
}
