using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.World
{
    /// Catálogo de ecos disponibles. Se eligen 3 al azar (sin repetir) cada vez que se
    /// ofrece el menú dentro del nivel.
    public static class EcoCatalogo
    {
        static readonly List<EcoDef> _todos = new List<EcoDef>
        {
            new EcoDef { id = "eco_danio", nombre = "Tiro más fuerte", descripcion = "+15 % de daño por disparo", tipo = EcoTipo.Danio, valor = 0.15f, nivelMax = 8 },
            new EcoDef { id = "eco_cadencia", nombre = "Recarga veloz", descripcion = "-12 % de recarga por tubo", tipo = EcoTipo.Cadencia, valor = 0.12f, nivelMax = 8 },
            new EcoDef { id = "eco_cargador", nombre = "Tubo extra", descripcion = "+1 tubo del cargador", tipo = EcoTipo.Cargador, valor = 1f, nivelMax = 10 },
            new EcoDef { id = "eco_velocidad", nombre = "Tiro veloz", descripcion = "+6 m/s de velocidad del tiro", tipo = EcoTipo.VelocidadDisparo, valor = 6f, nivelMax = 6 },
            new EcoDef { id = "eco_vida", nombre = "Reserva de vida", descripcion = "+20 de vida máxima", tipo = EcoTipo.VidaMax, valor = 20f, nivelMax = 10 },
            new EcoDef { id = "eco_captura", nombre = "Captura fácil", descripcion = "Umbral de captura al 30 %", tipo = EcoTipo.CapturaUmbral, valor = 0.05f, nivelMax = 6 },
            new EcoDef { id = "eco_energia", nombre = "Energía plena", descripcion = "+30 de energía máxima", tipo = EcoTipo.Energia, valor = 30f, nivelMax = 6 },
        };

        public static IReadOnlyList<EcoDef> Todos { get { return _todos; } }

        /// Elige 3 ecos distintos del catálogo. Nunca devuelve el mismo id dos veces en la misma oferta.
        public static EcoDef[] ElegirTres(System.Random rng)
        {
            var indices = new List<int>();
            for (int i = 0; i < _todos.Count; i++) indices.Add(i);
            for (int i = 0; i < 3 && i < indices.Count; i++)
            {
                int j = i + rng.Next(indices.Count - i);
                int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
            }
            var resultado = new EcoDef[Mathf.Min(3, indices.Count)];
            for (int i = 0; i < resultado.Length; i++) resultado[i] = _todos[indices[i]];
            return resultado;
        }
    }
}
