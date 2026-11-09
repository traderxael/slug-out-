using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.World
{
    /// Muestreador de clips horneados en Blender. No crea Animator ni AnimationClip:
    /// guarda, por hueso y por cuadro, la rotacion local relativa al rest, y DuelistaAnimator
    /// la aplica como bind * q sobre el sistema procedural.
    public static class ClipsProcedurales
    {
        [System.Serializable] public class BoneQ { public string bone; public float x, y, z, w; }
        [System.Serializable] public class Frame { public float hip; public BoneQ[] bones; }
        [System.Serializable] public class Clip { public string name; public float stride; public Frame[] frames; }
        [System.Serializable] public class Set { public int fps; public Clip[] clips; }

        private static Set _set;
        private static bool _cargado;
        private static readonly Dictionary<string, Set> _porPersonaje = new Dictionary<string, Set>();

        public static bool HayClips
        {
            get
            {
                Cargar();
                return _set != null && _set.clips != null && _set.clips.Length > 0;
            }
        }

        public static void Cargar()
        {
            if (_cargado) return;
            _cargado = true;
            TextAsset ta = Resources.Load<TextAsset>("Animacion/shane_piernas");
            if (ta == null)
            {
                Debug.LogWarning("[Clips] falta Resources/Animacion/shane_piernas.json; se usa el ciclo procedural.");
                return;
            }
            try
            {
                _set = JsonUtility.FromJson<Set>(ta.text);
            }
            catch (System.Exception e)
            {
                _set = null;
                Debug.LogWarning("[Clips] JSON invalido: " + e.Message);
            }
        }

        /// Juego de clips propio de un personaje: Resources/Animacion/<id>.json.
        ///
        /// El juego entero sharing un solo juego de clips (el del heroe) hacia
        /// que todos los duelistas caminan igual. Cada personaje riggeado trae
        /// el suyo, horneado en Blender con su esqueleto y su zancada.
        /// Devuelve null si ese personaje no tiene juego propio.
        public static Set JuegoDe(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Set s;
            if (_porPersonaje.TryGetValue(id, out s)) return s;
            s = null;
            TextAsset ta = Resources.Load<TextAsset>("Animacion/" + id);
            if (ta != null)
            {
                try
                {
                    s = JsonUtility.FromJson<Set>(ta.text);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Clips] JSON invalido de " + id + ": " + e.Message);
                    s = null;
                }
            }
            _porPersonaje[id] = s;
            return s;
        }

        public static Clip Buscar(string nombre, string id = null)
        {
            Set set = null;
            if (!string.IsNullOrEmpty(id)) set = JuegoDe(id);
            if (set == null) { Cargar(); set = _set; }
            if (set == null || set.clips == null) return null;
            for (int i = 0; i < set.clips.Length; i++)
                if (set.clips[i] != null && set.clips[i].name == nombre) return set.clips[i];
            return null;
        }

        public static int Fps
        {
            get { Cargar(); return _set != null ? Mathf.Max(1, _set.fps) : 30; }
        }

        /// `fase` en ciclos (0..1). Interpola los dos cuadros vecinos del hueso pedido.
        public static bool Muestrear(Clip clip, float fase, string hueso, out Quaternion q)
        {
            q = Quaternion.identity;
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return false;
            float f = Mathf.Repeat(fase, 1f) * clip.frames.Length;
            int i0 = Mathf.FloorToInt(f);
            if (i0 >= clip.frames.Length) i0 = clip.frames.Length - 1;
            int i1 = (i0 + 1) % clip.frames.Length;
            float t = f - i0;
            BoneQ a = Encontrar(clip.frames[i0], hueso);
            BoneQ b = Encontrar(clip.frames[i1], hueso);
            if (a == null || b == null) return false;
            Quaternion qa = new Quaternion(a.x, a.y, a.z, a.w);
            Quaternion qb = new Quaternion(b.x, b.y, b.z, b.w);
            q = Quaternion.Slerp(qa, qb, t);
            return true;
        }

        private static BoneQ Encontrar(Frame f, string hueso)
        {
            if (f == null || f.bones == null) return null;
            for (int i = 0; i < f.bones.Length; i++)
                if (f.bones[i] != null && f.bones[i].bone == hueso) return f.bones[i];
            return null;
        }

        /// Canal `hip`: offset local de cadera (negativo = agacharse) para que el pie
        /// plantado sea alcanzable con la pierna de este rig.
        public static bool MuestrearHip(Clip clip, float fase, out float hip)
        {
            hip = 0f;
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return false;
            float f = Mathf.Repeat(fase, 1f) * clip.frames.Length;
            int i0 = Mathf.FloorToInt(f);
            if (i0 >= clip.frames.Length) i0 = clip.frames.Length - 1;
            int i1 = (i0 + 1) % clip.frames.Length;
            float t = f - i0;
            hip = Mathf.Lerp(clip.frames[i0].hip, clip.frames[i1].hip, t);
            return true;
        }

        /// Clips de una sola pasada (impacto, muerte): el progreso se recorta a 0..1 y
        /// el ultimo cuadro no empalma con el primero, se queda como pose final.
        public static bool MuestrearUnaVez(Clip clip, float progreso, string hueso, out Quaternion q)
        {
            q = Quaternion.identity;
            if (!IndicesUnaVez(clip, progreso, out int i0, out int i1, out float t)) return false;
            BoneQ a = Encontrar(clip.frames[i0], hueso);
            BoneQ b = Encontrar(clip.frames[i1], hueso);
            if (a == null || b == null) return false;
            q = Quaternion.Slerp(new Quaternion(a.x, a.y, a.z, a.w),
                new Quaternion(b.x, b.y, b.z, b.w), t);
            return true;
        }

        public static bool MuestrearHipUnaVez(Clip clip, float progreso, out float hip)
        {
            hip = 0f;
            if (!IndicesUnaVez(clip, progreso, out int i0, out int i1, out float t)) return false;
            hip = Mathf.Lerp(clip.frames[i0].hip, clip.frames[i1].hip, t);
            return true;
        }

        private static bool IndicesUnaVez(Clip clip, float progreso,
            out int i0, out int i1, out float t)
        {
            i0 = 0;
            i1 = 0;
            t = 0f;
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return false;
            float f = Mathf.Clamp01(progreso) * (clip.frames.Length - 1);
            i0 = Mathf.FloorToInt(f);
            if (i0 >= clip.frames.Length) i0 = clip.frames.Length - 1;
            i1 = Mathf.Min(i0 + 1, clip.frames.Length - 1);
            t = f - i0;
            return true;
        }
    }
}
