using System.Collections.Generic;

namespace SlugOut.World
{
    /// Progreso de uso de una sola babosa: XP acumulada, nivel y si ya evolucionó a velocimorph.
    [System.Serializable]
    public class SlugProgresoData
    {
        public string slugId;
        public int xp;
        public int nivel;
        public bool evoluido;
    }

    /// Guardado completo de progreso de todas las babosas (un JSON en PlayerPrefs).
    [System.Serializable]
    public class SlugProgresoSave
    {
        public List<SlugProgresoData> slugs = new List<SlugProgresoData>();
    }
}
