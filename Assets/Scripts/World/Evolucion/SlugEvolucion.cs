using SlugOut.Slugs;
using UnityEngine;

namespace SlugOut.World
{
    /// Evolución protoform → velocimorph por XP de uso (S2).
    /// Cada disparo y cada captura otorgan XP a la babosa equipada. Al llegar a umbrales
    /// (100/250/500/1000/2000) sube de nivel; en el nivel 3 cambia a velocimorph.
    /// El progreso persiste entre partidas en PlayerPrefs (slugout.progreso).
    public static class SlugEvolucion
    {
        static readonly int[] Umbrales = { 100, 250, 500, 1000, 2000 };
        const int NivelEvolucion = 3;

        public static int XpDeDisparo = 2;
        public static int XpDeCaptura = 10;

        static SlugProgresoSave _save;
        static bool _cargado;

        static SlugProgresoSave Save
        {
            get
            {
                if (!_cargado)
                {
                    _cargado = true;
                    string json = PlayerPrefs.GetString("slugout.progreso", "");
                    _save = string.IsNullOrEmpty(json)
                        ? new SlugProgresoSave()
                        : JsonUtility.FromJson<SlugProgresoSave>(json);
                }
                return _save;
            }
        }

        static void Guardar()
        {
            PlayerPrefs.SetString("slugout.progreso", JsonUtility.ToJson(_save));
            PlayerPrefs.Save();
        }

        static SlugProgresoData Obtener(string slugId, bool crear = true)
        {
            foreach (var s in Save.slugs)
                if (s.slugId == slugId) return s;
            if (!crear) return null;
            var n = new SlugProgresoData { slugId = slugId };
            Save.slugs.Add(n);
            return n;
        }

        /// Otorga XP por usar una babosa (disparo o captura). Persiste al cambiar el nivel.
        public static void GanarXP(string slugId, int xp)
        {
            if (string.IsNullOrEmpty(slugId) || xp <= 0) return;
            var s = Obtener(slugId);
            s.xp += xp;
            int nivelAntes = s.nivel;
            s.nivel = 0;
            for (int i = 0; i < Umbrales.Length; i++)
                if (s.xp >= Umbrales[i]) s.nivel = i + 1;
            s.evoluido = s.nivel >= NivelEvolucion;
            Guardar();
            if (s.nivel > nivelAntes)
                Debug.Log("[SlugEvolucion] " + slugId + " subió a nivel " + s.nivel +
                    " (xp=" + s.xp + ", evoluido=" + s.evoluido + ")");
        }

        public static bool EstaEvoluido(string slugId)
        {
            var s = Obtener(slugId, false);
            return s != null && s.evoluido;
        }

        public static int Nivel(string slugId)
        {
            var s = Obtener(slugId, false);
            return s != null ? s.nivel : 0;
        }

        public static int Xp(string slugId)
        {
            var s = Obtener(slugId, false);
            return s != null ? s.xp : 0;
        }

        public static int XpParaSiguiente(string slugId)
        {
            var s = Obtener(slugId, false);
            if (s == null) return Umbrales[0];
            int idx = Mathf.Clamp(s.nivel, 0, Umbrales.Length - 1);
            return Umbrales[idx];
        }

        /// +5 % de daño por nivel de evolución.
        public static float BonusDanio(string slugId) { return 0.05f * Nivel(slugId); }
        /// +2 m/s de velocidad de tiro por nivel de evolución.
        public static float BonusVelocidad(string slugId) { return 2f * Nivel(slugId); }

        /// Sprite que debe usarse: velocimorph si está evoluida, protoform si no.
        public static Sprite ObtenerSprite(SlugData data)
        {
            if (data == null) return null;
            return EstaEvoluido(data.slugId) ? data.velocimorphSprite : data.protoformSprite;
        }
    }
}
