using UnityEngine;

namespace SlugOut.World
{
    /// Una mejora que se ofrece dentro del nivel (estilo "eco de la cueva").
    /// El jugador elige una de tres; el bono dura hasta que termina el nivel.
    public enum EcoTipo
    {
        Danio,           // +x% de daño por disparo
        Cadencia,        // -x% de recarga por tubo
        Cargador,        // +1 tubo del cargador
        VelocidadDisparo, // +x m/s de velocidad del tiro
        VidaMax,         // +x de vida máxima (se llena al instante)
        CapturaUmbral,   // baja el umbral de captura (más fácil capturar)
        Energia,         // +x de energía máxima
    }

    [System.Serializable]
    public class EcoDef
    {
        public string id;
        public string nombre;
        public string descripcion;
        public EcoTipo tipo;
        public float valor;        // cuanto vale un nivel de este eco
        public int nivelMax = 5;
        public Sprite icono;       // opcional; si falta se dibuja un chip de color
    }

    /// Oferta que se le presenta al jugador: qué eco y en qué nivel quedaría si lo elige.
    public struct EcoOferta
    {
        public EcoDef def;
        public int nivelResultado; // nivel del eco después de elegirlo
    }
}
