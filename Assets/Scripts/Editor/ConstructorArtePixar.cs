using System.IO;
using SlugOut.Slugs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SlugOut.Editor
{
    public static class ConstructorArtePixar
    {
        const string DirMat = "Assets/Art/Pixar/Materiales";
        const string DirEscena = "Assets/Scenes";
        static Shader _lit;

        [MenuItem("SlugOut/Arte/Crear escena de presentacion")]
        public static void CrearMenu()
        {
            Crear();
            EditorUtility.DisplayDialog("Arte Pixar", "Escena ArtePixar creada en Assets/Scenes.", "OK");
        }

        public static void RunBatch()
        {
            try
            {
                Crear();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("CONSTRUCTOR_ARTE_ERROR " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void Crear()
        {
            _lit = SlugOut.World.Shaders.Lit();
            if (_lit == null) throw new System.Exception("no encontre URP/Lit");
            Directory.CreateDirectory(DirMat);
            Directory.CreateDirectory(DirEscena);

            Material piedra = Mat("Piedra", new Color(0.08f, 0.09f, 0.12f), null, 0.3f, 0f, true);
            Material piedraClara = Mat("PiedraClara", new Color(0.16f, 0.18f, 0.22f), null, 0.3f, 0f, false);
            Material hongoTallo = Mat("HongoTallo", new Color(0.55f, 0.85f, 0.9f), new Color(0.1f, 0.45f, 0.55f), 0.4f, 0f, false);
            Material hongoCap = Mat("HongoCap", new Color(0.1f, 0.6f, 0.9f), new Color(0.1f, 0.7f, 1f) * 1.6f, 0.3f, 0f, false);
            Material cristal = Mat("Cristal", new Color(0.45f, 0.9f, 1f), new Color(0.25f, 0.8f, 1f) * 1.5f, 0.2f, 0.1f, false);
            Material cristalVioleta = Mat("CristalVioleta", new Color(0.55f, 0.35f, 0.95f), new Color(0.45f, 0.2f, 1f) * 1.4f, 0.2f, 0.1f, false);
            Material llama = Mat("Llama", new Color(1f, 0.45f, 0.1f), new Color(1f, 0.35f, 0.05f) * 1.8f, 0.3f, 0f, false);
            Material estandarte = Mat("Estandarte", new Color(0.05f, 0.08f, 0.18f), null, 0.5f, 0f, false);
            Material oro = Mat("Oro", new Color(0.8f, 0.6f, 0.2f), new Color(0.7f, 0.5f, 0.15f) * 0.6f, 0.25f, 0.6f, false);
            Material agua = Mat("Agua", new Color(0.1f, 0.4f, 0.7f), new Color(0.1f, 0.5f, 0.9f) * 1.3f, 0.15f, 0.1f, false);
            Material cuerpo = MatTex("ShaneCuerpo", "Assets/Resources/Shane/Texturas/shane_base.png",
                "Assets/Resources/Shane/Texturas/shane_normal.png", 0.45f);
            Material blaster = MatTex("ShaneBlaster", "Assets/Resources/Shane/Texturas/shane_base.png",
                "Assets/Resources/Shane/Texturas/shane_normal.png", 0.4f);
            Material botas = Mat("ShaneBotas", new Color(0.10f, 0.12f, 0.17f), null, 0.3f, 0.05f, false);
            Material tubos = Mat("ShaneTubos", new Color(1f, 0.9f, 0.1f), new Color(1f, 0.85f, 0.08f) * 1.6f, 0.25f, 0f, false);

            Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.008f, 0.018f, 0.04f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.04f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.05f, 0.09f);

            CilindroAbierto("Cueva", new Vector3(0.45f, 3f, 0.5f), 6f, 6f, 32, piedra);
            Primitivo(PrimitiveType.Plane, "Piso", new Vector3(0.45f, -0.015f, 0f),
                new Vector3(1.2f, 1f, 1.2f), piedraClara, false);
            Primitivo(PrimitiveType.Cylinder, "Plataforma", new Vector3(0.45f, -0.045f, 0f),
                new Vector3(2.15f, 0.045f, 2.15f), piedraClara, false);

            for (int i = 0; i < 5; i++)
            {
                float x = -2.35f + i * 1.2f;
                float z = -2.2f + (i % 2) * 0.5f;
                Hongo("Hongo_" + i, new Vector3(x, 0f, z), 0.75f + 0.2f * (i % 3), hongoTallo, hongoCap);
            }
            Cristal("Cristal_A", new Vector3(-2.6f, 0f, 0.6f), 0.75f, cristal);
            Cristal("Cristal_B", new Vector3(-2.2f, 0f, 1.4f), 0.5f, cristalVioleta);
            Cristal("Cristal_C", new Vector3(2.7f, 0f, -1.2f), 0.9f, cristal);
            Cristal("Cristal_D", new Vector3(1.9f, 0f, 1.7f), 0.55f, cristalVioleta);
            Brasero("Brasero_A", new Vector3(-2.55f, 0f, 1.5f), llama, piedraClara);
            Brasero("Brasero_B", new Vector3(2.75f, 0f, 1.35f), llama, piedraClara);
            for (int i = 0; i < 4; i++)
            {
                Hongo("HongoFondo_" + i, new Vector3(-2.4f + i * 1.6f, 0f, -3.2f),
                    0.9f + 0.15f * (i % 2), hongoTallo, hongoCap);
                Cristal("CristalFondo_" + i, new Vector3(-2.6f + i * 1.7f, 0f, -3.7f),
                    0.6f + 0.2f * (i % 2), i % 2 == 0 ? cristal : cristalVioleta);
            }

            Primitivo(PrimitiveType.Cube, "Estandarte", new Vector3(0.45f, 2.3f, -2.9f),
                new Vector3(1.6f, 2.2f, 0.06f), estandarte, false);
            Primitivo(PrimitiveType.Cube, "Emblema", new Vector3(0.45f, 2.55f, -2.82f),
                new Vector3(0.35f, 0.5f, 0.03f), oro, false);
            Primitivo(PrimitiveType.Cube, "Emblema2", new Vector3(0.45f, 2.0f, -2.82f),
                new Vector3(0.65f, 0.08f, 0.03f), oro, false);

            GameObject hero = Instanciar("Assets/Resources/Shane/shane_pixar_fbx.fbx", new Vector3(-0.55f, 0f, 0f));
            AplicarHeroe(hero, cuerpo, blaster, botas, tubos);
            InstanciarSlug("electroshock", new Vector3(0.40f, 0f, 0f));
            InstanciarSlug("ascua", new Vector3(1.02f, 0f, 0f));
            InstanciarSlug("copo", new Vector3(1.64f, 0f, 0f));

            GameObject key = new GameObject("Luz_Key");
            Light kl = key.AddComponent<Light>();
            kl.type = LightType.Directional;
            kl.color = new Color(1f, 0.86f, 0.7f);
            kl.intensity = 1.2f;
            kl.shadows = LightShadows.None;
            key.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            GameObject fill = new GameObject("Luz_Fill");
            Light fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.color = new Color(0.4f, 0.65f, 1f);
            fl.intensity = 0.45f;
            fl.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(15f, 130f, 0f);
            Punto("Luz_Hongos", new Vector3(-1.8f, 0.7f, -0.8f), new Color(0.2f, 0.75f, 1f), 3.5f);
            Punto("Luz_Brasero", new Vector3(2.7f, 0.8f, 1.3f), new Color(1f, 0.45f, 0.15f), 3f);
            Punto("Luz_Cristal", new Vector3(-2.5f, 0.8f, 0.6f), new Color(0.45f, 0.4f, 1f), 2.5f);

            GameObject camGo = new GameObject("Camara_Arte");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.02f, 0.045f);
            cam.fieldOfView = 40f;
            camGo.transform.position = new Vector3(0.45f, 1.55f, 4.6f);
            camGo.transform.LookAt(new Vector3(0.45f, 0.85f, 0f));
            camGo.tag = "MainCamera";

            EditorSceneManager.SaveScene(escena, Path.Combine(DirEscena, "ArtePixar.unity"));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Capturar(cam);
            Debug.Log("ESCENA_ARTE=Assets/Scenes/ArtePixar.unity");
        }

        static Material Mat(string nombre, Color color, Color? emision, float smooth, float metallic, bool dobleCara)
        {
            string path = DirMat + "/" + nombre + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(_lit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = _lit;
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", dobleCara ? 0f : 2f);
            if (emision.HasValue)
            {
                m.SetColor("_EmissionColor", emision.Value);
                m.EnableKeyword("_EMISSION");
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MatTex(string nombre, string basePath, string normalPath, float smooth)
        {
            string path = DirMat + "/" + nombre + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(_lit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = _lit;
            m.color = Color.white;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (baseTex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", baseTex);
            if (normalTex != null && m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", normalTex);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Primitivo(PrimitiveType tipo, string nombre, Vector3 pos, Vector3 escala, Material mat, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(tipo);
            go.name = nombre;
            go.transform.position = pos;
            go.transform.localScale = escala;
            if (!collider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return go;
        }

        static GameObject CilindroAbierto(string nombre, Vector3 centro, float radio, float alto, int seg, Material mat)
        {
            Vector3[] v = new Vector3[seg * 2];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                float x = Mathf.Cos(a) * radio;
                float z = Mathf.Sin(a) * radio;
                v[i] = new Vector3(x, -alto * 0.5f, z);
                v[seg + i] = new Vector3(x, alto * 0.5f, z);
            }
            int[] t = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                int b0 = i;
                int b1 = (i + 1) % seg;
                int t0 = seg + i;
                int t1 = seg + (i + 1) % seg;
                t[i * 6] = b0;
                t[i * 6 + 1] = t1;
                t[i * 6 + 2] = b1;
                t[i * 6 + 3] = b0;
                t[i * 6 + 4] = t0;
                t[i * 6 + 5] = t1;
            }
            Mesh m = new Mesh();
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            GameObject go = new GameObject(nombre);
            go.transform.position = centro;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static GameObject Instanciar(string path, Vector3 pos)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.Exception("falta " + path);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;
            return go;
        }

        static void AplicarHeroe(GameObject hero, Material cuerpo, Material blaster, Material botas, Material tubos)
        {
            foreach (Renderer r in hero.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    if (n.Contains("cuerpo")) mats[i] = cuerpo;
                    else if (n.Contains("blaster")) mats[i] = blaster;
                    else if (n.Contains("botas")) mats[i] = botas;
                    else if (n.Contains("tubos")) mats[i] = tubos;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static void InstanciarSlug(string id, Vector3 pos)
        {
            GameObject go = Instanciar("Assets/Resources/Slugs/" + id + "_pixar.glb", pos);
            SlugData d = AssetDatabase.LoadAssetAtPath<SlugData>("Assets/ScriptableObjects/Slugs/" + id + ".asset");
            Color color = d != null ? d.elementColor : Color.cyan;
            Material brillo = Mat("Brillo_" + id, color, color * 1.6f, 0.25f, 0f, false);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].name.ToLowerInvariant().Contains("_brillo")) mats[i] = brillo;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        static void Hongo(string nombre, Vector3 pos, float escala, Material tallo, Material cap)
        {
            GameObject stem = Primitivo(PrimitiveType.Cylinder, nombre + "_tallo",
                pos + Vector3.up * (0.35f * escala), new Vector3(0.09f * escala, 0.35f * escala, 0.09f * escala), tallo, false);
            GameObject head = Primitivo(PrimitiveType.Sphere, nombre + "_cap",
                pos + Vector3.up * (0.72f * escala), new Vector3(0.38f * escala, 0.16f * escala, 0.38f * escala), cap, false);
            stem.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            head.transform.rotation = Quaternion.identity;
        }

        static void Cristal(string nombre, Vector3 pos, float alto, Material mat)
        {
            GameObject go = Cono(nombre, 0.18f, alto, pos + Vector3.up * (alto * 0.5f), mat);
            go.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            GameObject go2 = Cono(nombre + "_b", 0.11f, alto * 0.7f, pos + new Vector3(0.2f, alto * 0.35f, 0.1f), mat);
            go2.transform.rotation = Quaternion.Euler(0f, -20f, 5f);
        }

        static GameObject Cono(string nombre, float radio, float alto, Vector3 centro, Material mat)
        {
            int seg = 8;
            Vector3[] v = new Vector3[seg + 1];
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                v[i] = new Vector3(Mathf.Cos(a) * radio, -alto * 0.5f, Mathf.Sin(a) * radio);
            }
            v[seg] = new Vector3(0f, alto * 0.5f, 0f);
            int[] t = new int[seg * 3 + seg * 3];
            for (int i = 0; i < seg; i++)
            {
                t[i * 3] = i;
                t[i * 3 + 1] = (i + 1) % seg;
                t[i * 3 + 2] = seg;
                t[seg * 3 + i * 3] = (i + 1) % seg;
                t[seg * 3 + i * 3 + 1] = i;
                t[seg * 3 + i * 3 + 2] = seg;
            }
            Mesh m = new Mesh();
            m.vertices = v;
            m.triangles = t;
            m.RecalculateNormals();
            GameObject go = new GameObject(nombre);
            go.transform.position = centro;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static void Brasero(string nombre, Vector3 pos, Material llama, Material baseMat)
        {
            Primitivo(PrimitiveType.Cylinder, nombre + "_base", pos + Vector3.up * 0.25f,
                new Vector3(0.3f, 0.25f, 0.3f), baseMat, false);
            Primitivo(PrimitiveType.Sphere, nombre + "_fuego", pos + Vector3.up * 0.65f,
                new Vector3(0.22f, 0.32f, 0.22f), llama, false);
        }

        static void Punto(string nombre, Vector3 pos, Color color, float rango)
        {
            GameObject go = new GameObject(nombre);
            go.transform.position = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = 0.3f;
            l.range = rango;
            l.shadows = LightShadows.None;
        }

        static void Capturar(Camera cam)
        {
            const int w = 1280;
            const int h = 720;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "arte-pixar"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "escena_cueva.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Debug.Log("CAPTURA_CUEVA=" + path);
        }
    }
}
