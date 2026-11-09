using UnityEngine;

namespace SlugOut.World
{
    /// Shader de iluminacion del pipeline activo, con fallback.
    ///
    /// Los materiales del proyecto se crearon con URP Lit, pero el paquete URP no esta
    /// instalado (Packages/manifest.json no lo incluye): Shader.Find devuelve null y
    /// `new Material(null)` tira ArgumentNullException en runtime. En RegionCuevaSeca
    /// ese throw corta Construir() a mitad y deja la cueva sin coberturas, sin borde y
    /// sin babosas salvajes. Una sola puerta con fallback evita que vuelva a pasar.
    public static class Shaders
    {
        public static Shader Lit()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null || !s.isSupported) s = Shader.Find("Standard");
            return s;
        }
    }
}
