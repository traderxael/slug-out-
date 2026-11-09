using UnityEngine;

namespace SlugOut.World
{
    /// Estrellas por nivel: 0-3 según el resultado, guardadas en PlayerPrefs.
    public static class Estrellas
    {
        private const string CLAVE_PREFIX = "slugout.estrellas_";

        public static int Obtener(int nivel) { return PlayerPrefs.GetInt(CLAVE_PREFIX + nivel, 0); }

        /// Guarda las estrellas de un nivel si supera las ya guardadas.
        public static void Guardar(int nivel, int estrellas)
        {
            int clave = PlayerPrefs.GetInt(CLAVE_PREFIX + nivel, 0);
            if (estrellas > clave)
            {
                PlayerPrefs.SetInt(CLAVE_PREFIX + nivel, estrellas);
                PlayerPrefs.Save();
            }
        }

        public static int Total()
        {
            int total = 0;
            for (int n = 1; n <= SlugStory.TotalCampana; n++)
                total += Obtener(n);
            return total;
        }

        /// Calcula estrellas al terminar un nivel.
        /// 1 estrella: ganar. +1: capturar al menos la meta. +1: terminar con >50 % de vida.
        public static int Calcular(bool gano, int capturadas, int metaCapturas, float hp, float hpMax)
        {
            if (!gano) return 0;
            int s = 1;
            if (metaCapturas > 0 && capturadas >= metaCapturas) s++;
            if (hpMax > 0f && hp / hpMax > 0.5f) s++;
            return Mathf.Min(3, s);
        }
    }
}
