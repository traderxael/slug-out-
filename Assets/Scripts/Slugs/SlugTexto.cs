using SlugOut.Core;

namespace SlugOut.Slugs
{
    /// Etiquetas visibles de los elementos, en un solo lugar. Los IDs y los ordinales
    /// de ElementType no se tocan: solo cambia la presentacion.
    public static class SlugTexto
    {
        public static string NombreElemento(ElementType e)
        {
            switch (e)
            {
                case ElementType.Fire: return "Fuego";
                case ElementType.Water: return "Agua";
                case ElementType.Earth: return "Tierra";
                case ElementType.Air: return "Aire";
                case ElementType.Electric: return "Electrico";
                case ElementType.Ice: return "Hielo";
                case ElementType.Plant: return "Planta";
                case ElementType.Energy: return "Energia";
                case ElementType.Shadow: return "Sombra";
                case ElementType.Light: return "Luz";
                case ElementType.Toxic: return "Toxico";
                default: return e.ToString();
            }
        }

        /// Descripciones autoradas con el tipo en ingles ("tipo Earth blindada"): se
        /// traduce solo la presentacion, sin tocar el asset ni el id.
        public static string TraducirTexto(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace(" Fire", " Fuego").Replace(" Water", " Agua")
                .Replace(" Earth", " Tierra").Replace(" Air", " Aire")
                .Replace(" Electric", " Electrico").Replace(" Ice", " Hielo")
                .Replace(" Plant", " Planta").Replace(" Shadow", " Sombra")
                .Replace(" Energy", " Energia").Replace(" Light", " Luz")
                .Replace(" Toxic", " Toxico");
        }
    }
}
