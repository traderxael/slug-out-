using System;
using SlugOut.Core;
using UnityEngine;

namespace SlugOut.World
{
    /// Desafío diario por semilla: una semilla derivada de la fecha UTC genera un nivel
    /// especial determinista. Tabla de clasificación local (mejor puntuación).
    public static class DesafioDiario
    {
        private const string CLAVE_SEED = "slugout.diario_seed";
        private const string CLAVE_PUNTUACION = "slugout.diario_puntuacion";

        public static int SemillaDelDia()
        {
            var hoy = DateTime.UtcNow.Date;
            int seed = hoy.Year * 10000 + hoy.Month * 100 + hoy.Day;
            // Mezcla para que no sea obvio y cambie cada día. unchecked: el desbordamiento
            // intentional es lo que hace la semilla (hash estilo FNV sobre la fecha).
            unchecked { seed = seed * 16777619 + hoy.DayOfYear * 97531; }
            int guardada = PlayerPrefs.GetInt(CLAVE_SEED, 0);
            int fechaGuardada = PlayerPrefs.GetInt(CLAVE_SEED + "_fecha", 0);
            if (fechaGuardada != seed)
            {
                PlayerPrefs.SetInt(CLAVE_SEED, seed);
                PlayerPrefs.SetInt(CLAVE_SEED + "_fecha", seed);
                PlayerPrefs.SetInt(CLAVE_PUNTUACION, 0);
                PlayerPrefs.Save();
            }
            return seed;
        }

        public static int PuntuacionMejor()
        {
            return PlayerPrefs.GetInt(CLAVE_PUNTUACION, 0);
        }

        public static void RegistrarPuntuacion(int puntos)
        {
            int mejor = PuntuacionMejor();
            if (puntos > mejor)
            {
                PlayerPrefs.SetInt(CLAVE_PUNTUACION, puntos);
                PlayerPrefs.Save();
            }
        }

        /// Genera un NivelConfig determinista a partir de la semilla del día.
        public static NivelConfig NivelDelDia(int numeroNivel)
        {
            int seed = SemillaDelDia();
            System.Random rng = new System.Random(seed ^ (numeroNivel * 7919));
            int total = 8 + rng.Next(0, 10);
            int maxVivos = 2 + rng.Next(0, 4);
            float intervalo = 0.9f + (float)rng.NextDouble() * 1.2f;
            float velocidad = 2.5f + (float)rng.NextDouble() * 2.5f;
            float dano = 5f + (float)rng.NextDouble() * 7f;
            int capturas = 1 + rng.Next(0, 4);
            return new NivelConfig(total, maxVivos, intervalo, velocidad, dano, capturas)
            {
                nivel = numeroNivel,
                tema = (ElementType)rng.Next(0, 8),
                anilloInvertido = rng.NextDouble() < 0.3f,
                rocas = true,
                infinito = false,
            };
        }
    }
}
