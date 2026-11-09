using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.Slugs
{
    /// Roster de babosas accesible en runtime.
    /// Fuente preferida: Resources/SlugsData/SlugRoster (asset cableado, ver SlugOut/3. en el menu Editor).
    /// Si no existe todavia, se arma en memoria desde los SlugData del proyecto, para que el
    /// menu y la seleccion de personaje nunca queden vacios por un paso de setup.
    [CreateAssetMenu(fileName = "SlugRoster", menuName = "SlugOut/Slug Roster", order = 3)]
    public class SlugRoster : ScriptableObject
    {
        [SerializeField] private SlugData[] babosas;

        private static SlugData[] _cache;

        public SlugData[] Lista
        {
            get
            {
                if (babosas == null || babosas.Length == 0) Poblar();
                return babosas;
            }
        }

        public static SlugData[] Todos
        {
            get
            {
                if (_cache != null && _cache.Length > 0) return _cache;
                _cache = DesdeRecursos();
                if (_cache == null || _cache.Length == 0)
                {
                    // El fallback solo ve lo que ya esta cargado: en un build eso son los 22 de la
                    // escena, no los 62. Avisar en vez de callarse, que asi es como se colaba.
                    Debug.LogWarning("[SlugRoster] falta Resources/SlugsData/SlugRoster: cae al fallback de assets cargados y el roster queda incompleto. Ejecuta SlugOut/4.");
                    _cache = DesdeCarga();
                }
                Ordenar(_cache);
                Debug.Log("[SlugRoster] " + (_cache == null ? 0 : _cache.Length) + " babosas disponibles");
                return _cache;
            }
        }

        public static SlugData Buscar(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            SlugData[] t = Todos;
            if (t == null) return null;
            for (int i = 0; i < t.Length; i++)
                if (t[i] != null && t[i].slugId == id) return t[i];
            return null;
        }

        public static void Invalidar() { _cache = null; }

        private static SlugData[] DesdeRecursos()
        {
            SlugRoster cont = Resources.Load<SlugRoster>("SlugsData/SlugRoster");
            return cont != null ? cont.Lista : null;
        }

        /// Fallback: cualquier SlugData ya cargado (los referencian la escena y los prefabs).
        private static SlugData[] DesdeCarga()
        {
            var lista = new List<SlugData>();
            foreach (Object o in Resources.FindObjectsOfTypeAll<SlugData>())
            {
                SlugData d = o as SlugData;
                if (d == null || string.IsNullOrEmpty(d.slugId) || lista.Contains(d)) continue;
                lista.Add(d);
            }
            return lista.ToArray();
        }

        private static void Ordenar(SlugData[] t)
        {
            if (t == null) return;
            System.Array.Sort(t, (a, b) => string.CompareOrdinal(a != null ? a.slugName : "", b != null ? b.slugName : ""));
        }

        [ContextMenu("Recargar desde la carpeta")]
        public void Poblar()
        {
#if UNITY_EDITOR
            string[] guias = UnityEditor.AssetDatabase.FindAssets("t:SlugData", new[] { "Assets/ScriptableObjects/Slugs" });
            babosas = new SlugData[guias.Length];
            for (int i = 0; i < guias.Length; i++)
                babosas[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<SlugData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guias[i]));
            Ordenar(babosas);
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            Invalidar();
#endif
        }
    }
}
