using UnityEngine;

namespace SlugOut.World
{
    /// Matematica pura del minimapa fijo de la arena: mundo -> disco GUI, con el mismo
    /// origen y escala para habitats, coberturas, bocas y jugador. Separado del HUD para
    /// poder probar el mapeo y el clamp sin abrir una pantalla.
    public static class MapaRadar
    {
        public const float RadioArena = 24.6f;
        /// Un poco mas que las paredes: deja margen para que las bocas no toquen el filo.
        public const float RadioMapaMundo = 26f;

        public static Vector2 MundoADisco(Vector3 mundo, float cx, float cy, float radioMapa)
        {
            return MundoADisco(mundo.x, mundo.z, cx, cy, radioMapa);
        }

        public static Vector2 MundoADisco(float x, float z, float cx, float cy, float radioMapa)
        {
            float escala = radioMapa / RadioMapaMundo;
            Vector2 p = new Vector2(x, z) * escala;
            if (p.sqrMagnitude > radioMapa * radioMapa)
                p = p.normalized * radioMapa;
            return new Vector2(cx + p.x, cy - p.y);
        }

        public static Vector2 CoberturaADisco(float angulo, float radio, float cx, float cy,
                                              float radioMapa)
        {
            float a = angulo * Mathf.Deg2Rad;
            return MundoADisco(Mathf.Cos(a) * radio, Mathf.Sin(a) * radio, cx, cy, radioMapa);
        }

        public static string CodigoRegion(int region)
        {
            switch (region)
            {
                case 0: return "CS";
                case 1: return "GE";
                case 2: return "VV";
                case 3: return "CC";
                default: return "--";
            }
        }

        public static string NombreCorto(int region)
        {
            switch (region)
            {
                case 0: return "SECA";
                case 1: return "ECO";
                case 2: return "VENA";
                case 3: return "CANON";
                default: return "?";
            }
        }
    }
}
