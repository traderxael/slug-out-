using System.Collections.Generic;
using System.IO;
using SlugOut.World;
using UnityEditor;
using UnityEngine;

namespace SlugOut.Editor
{
    public static class ValidadorArtePixar
    {
        static readonly string[] Huesos =
        {
            "raiz", "cadera", "columna", "cuello", "cabeza",
            "brazo_der", "antebrazo_der", "mano_der",
            "brazo_izq", "antebrazo_izq", "mano_izq",
            "muslo_der", "pantorrilla_der", "pie_der",
            "muslo_izq", "pantorrilla_izq", "pie_izq"
        };

        static readonly string[] Babosas =
        {
            "electroshock", "ascua", "copo",
            "corriente", "centella", "guijarro", "marea",
            "tenue", "roquito", "liana", "gotita"
        };

        // Las 53 del roster que aun no tenian modelo v6: mismo cuerpo de la familia,
        // pero 5 materiales (sin _brillo) y menos triangulos.
        static readonly string[] BabosasRoster =
        {
            "bengala", "chispa", "elemental-fuego", "fenix", "fosforo", "ignia", "infierno", "vulcano",
            "abisal", "bubbaleone", "elemental-agua", "leviatan", "bloque", "carnero", "coloso",
            "demoledora", "elemental-tierra", "titan", "brisa", "elemental-aire", "huracan",
            "planeadora", "rafaga", "remolino", "tifon", "tornado", "magnetosa", "relampago",
            "tormenta", "zeus", "zumbido", "congelada", "escarcha", "esquirla-helada", "eterna",
            "glaciar", "helada", "polaro", "ventisca", "brote", "enredadera", "espina", "primigenia",
            "selva", "tejedora", "elemental-energia", "enigma", "fandango", "abismo", "ghoul", "mota",
            "oculta", "vacio"
        };

        [System.Serializable]
        public class Reporte
        {
            public string fecha;
            public List<string> ok = new List<string>();
            public List<string> avisos = new List<string>();
            public List<string> errores = new List<string>();
        }

        [MenuItem("SlugOut/Arte/Validar modelos Pixar")]
        public static void ValidarMenu()
        {
            Reporte r = Validar();
            Debug.Log(JsonUtility.ToJson(r, true));
        }

        public static void RunBatch()
        {
            Reporte r = Validar();
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "arte-pixar");
            Directory.CreateDirectory(dir);
            string json = JsonUtility.ToJson(r, true);
            File.WriteAllText(Path.Combine(dir, "validacion.json"), json);
            Debug.Log("VALIDACION_ARTE=" + json);
            if (Application.isBatchMode)
                EditorApplication.Exit(r.errores.Count == 0 ? 0 : 1);
        }

        static Reporte Validar()
        {
            Reporte r = new Reporte { fecha = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            ValidarHeroe(r);
            for (int i = 0; i < Babosas.Length; i++) ValidarBabosa(r, Babosas[i], false);
            for (int i = 0; i < BabosasRoster.Length; i++) ValidarBabosa(r, BabosasRoster[i], true);
            return r;
        }

        static void ValidarHeroe(Reporte r)
        {
            GameObject prefab = Resources.Load<GameObject>("Shane/shane_pixar");
            bool fbx = false;
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>("Shane/shane_pixar_fbx");
                fbx = prefab != null;
                if (fbx) r.avisos.Add("se valida el FBX de respaldo (el GLB no esta en Resources)");
            }
            if (prefab == null)
            {
                r.errores.Add("falta Resources/Shane/shane_pixar y shane_pixar_fbx (glTFast y FBX no importaron)");
                return;
            }
            GameObject inst = Object.Instantiate(prefab);
            try
            {
                HashSet<string> nombres = new HashSet<string>();
                foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) nombres.Add(t.name);
                for (int i = 0; i < Huesos.Length; i++)
                {
                    if (!nombres.Contains(Huesos[i]))
                        r.errores.Add("shane_pixar: falta el hueso " + Huesos[i]);
                }
                int tris = ContarTris(inst);
                if (tris > 16000) r.errores.Add("shane_pixar: " + tris + " tris (limite 16000)");
                else r.ok.Add("shane_pixar tris=" + tris);

                MaterialesPixar.QuitarAnimadores(inst);
                if (inst.GetComponentInChildren<Animator>(true) != null)
                    r.errores.Add("shane_pixar: quedo un Animator");
                if (fbx || !MaterialesPixar.EsModeloTexturizado(inst))
                {
                    MaterialesPixar.PrepararHeroeFbx(inst, Color.cyan);
                    r.avisos.Add("shane_pixar: materiales reconstruidos desde Resources/Shane/Texturas (FBX)");
                }

                bool textura = false;
                bool tubos = false;
                foreach (Renderer render in inst.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in render.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (MaterialesPixar.TieneTextura(m)) textura = true;
                        if (m.name.IndexOf("tubos", System.StringComparison.OrdinalIgnoreCase) >= 0) tubos = true;
                    }
                }
                if (!textura) r.errores.Add("shane_pixar: perdio la textura");
                if (!tubos) r.errores.Add("shane_pixar: falta el material _tubos");
                if (textura && tubos) r.ok.Add("shane_pixar textura + _tubos");
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        static void ValidarBabosa(Reporte r, string id, bool roster)
        {
            GameObject prefab = Resources.Load<GameObject>("Slugs/" + id + "_pixar");
            if (prefab == null)
            {
                r.errores.Add("falta Resources/Slugs/" + id + "_pixar.glb");
                return;
            }
            GameObject inst = Object.Instantiate(prefab);
            try
            {
                if (inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                    r.errores.Add(id + "_pixar: no debe traer SkinnedMeshRenderer");
                MaterialesPixar.QuitarAnimadores(inst);
                if (inst.GetComponentInChildren<Animator>(true) != null)
                    r.errores.Add(id + "_pixar: quedo un Animator");

                int limiteTris = roster ? 4500 : 7000;
                int tris = ContarTris(inst);
                if (tris > limiteTris) r.errores.Add(id + "_pixar: " + tris + " tris (limite " + limiteTris + ")");
                else r.ok.Add(id + "_pixar tris=" + tris);

                MeshRenderer[] mrs = inst.GetComponentsInChildren<MeshRenderer>(true);
                if (mrs.Length != 1) r.errores.Add(id + "_pixar: " + mrs.Length + " MeshRenderer (debe ser 1)");
                if (mrs.Length == 1)
                {
                    MeshRenderer mr = mrs[0];
                    float alto = mr.bounds.size.y;
                    float piso = mr.bounds.min.y;
                    if (alto < 0.55f || alto > 0.61f)
                        r.errores.Add(id + "_pixar: altura " + alto.ToString("0.###") + " fuera de 0.55-0.61");
                    if (Mathf.Abs(piso) > 0.02f)
                        r.errores.Add(id + "_pixar: base en y=" + piso.ToString("0.###"));
                    Material[] mats = mr.sharedMaterials;
                    if (mats.Length == 0 || mats[0] == null ||
                        !mats[0].name.EndsWith("_cuerpo", System.StringComparison.OrdinalIgnoreCase))
                        r.errores.Add(id + "_pixar: la primitiva 0 no es _cuerpo");
                    int limiteMats = roster ? 6 : 9;
                    if (mats.Length > limiteMats)
                        r.errores.Add(id + "_pixar: " + mats.Length + " materiales (limite " + limiteMats + ")");
                }

                bool cuerpo = false;
                bool brillo = false;
                bool cresta = false;
                foreach (Renderer render in inst.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in render.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (m.name.EndsWith("_cuerpo", System.StringComparison.OrdinalIgnoreCase) &&
                            MaterialesPixar.TieneTextura(m)) cuerpo = true;
                        if (m.name.EndsWith("_brillo", System.StringComparison.OrdinalIgnoreCase)) brillo = true;
                        if (m.name.EndsWith("_cresta", System.StringComparison.OrdinalIgnoreCase)) cresta = true;
                    }
                }
                if (!cuerpo) r.errores.Add(id + "_pixar: falta textura en _cuerpo");
                if (!cresta) r.errores.Add(id + "_pixar: falta material _cresta");
                if (!roster && !brillo) r.errores.Add(id + "_pixar: falta material _brillo");
                if (cuerpo && cresta && (roster || brillo))
                    r.ok.Add(id + "_pixar textura + cresta" + (roster ? "" : " + _brillo"));
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        static int ContarTris(GameObject raiz)
        {
            int tris = 0;
            foreach (SkinnedMeshRenderer s in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh != null) tris += s.sharedMesh.triangles.Length / 3;
            foreach (MeshFilter f in raiz.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null) tris += f.sharedMesh.triangles.Length / 3;
            return tris;
        }
    }
}
