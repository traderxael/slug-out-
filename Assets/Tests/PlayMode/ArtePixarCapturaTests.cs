using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    public class ArtePixarCapturaTests
    {
        [UnityTest]
        public IEnumerator CapturaModelosEnUnity()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("batch sin dispositivo grafico: no se puede capturar");

            GameObject camGo = new GameObject("captura_cam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.025f, 0.05f, 1f);
            cam.fieldOfView = 40f;
            camGo.transform.position = new Vector3(0.45f, 1.55f, -4.6f);
            camGo.transform.LookAt(new Vector3(0.45f, 0.85f, 0f));

            GameObject key = new GameObject("key");
            Light kl = key.AddComponent<Light>();
            kl.type = LightType.Directional;
            kl.color = new Color(1f, 0.86f, 0.72f);
            kl.intensity = 1.1f;
            kl.shadows = LightShadows.None;
            key.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

            GameObject fill = new GameObject("fill");
            Light fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.color = new Color(0.75f, 0.82f, 1f);
            fl.intensity = 0.45f;
            fl.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(15f, 130f, 0f);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 8f;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material piso = new Material(lit);
            piso.color = new Color(0.02f, 0.03f, 0.045f);
            if (piso.HasProperty("_Smoothness")) piso.SetFloat("_Smoothness", 0.35f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = piso;

            GameObject hero = new GameObject("hero");
            hero.transform.position = new Vector3(-0.55f, 1f, 0f);
            GameObject s1 = new GameObject("s1");
            s1.transform.position = new Vector3(0.40f, 1f, 0f);
            GameObject s2 = new GameObject("s2");
            s2.transform.position = new Vector3(1.02f, 1f, 0f);
            GameObject s3 = new GameObject("s3");
            s3.transform.position = new Vector3(1.64f, 1f, 0f);

            MethodInfo shane = typeof(SlugArenaGame).GetMethod("AttachShane", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo modelo = typeof(SlugArenaGame).GetMethod("AttachModel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(shane);
            Assert.IsNotNull(modelo);
            shane.Invoke(null, new object[] { hero, SlugRoster.Buscar("electroshock") });
            modelo.Invoke(null, new object[] { s1, SlugRoster.Buscar("electroshock"), new Vector3(0f, -1f, 0f), false });
            modelo.Invoke(null, new object[] { s2, SlugRoster.Buscar("ascua"), new Vector3(0f, -1f, 0f), false });
            modelo.Invoke(null, new object[] { s3, SlugRoster.Buscar("copo"), new Vector3(0f, -1f, 0f), false });
            foreach (GameObject go in new[] { hero, s1, s2, s3 })
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Retrato") t.gameObject.SetActive(false);
            foreach (var par in new[] { ("electroshock", s1), ("ascua", s2), ("copo", s3) })
            {
                Transform b = par.Item2.transform.Find("Cuerpo_" + par.Item1);
                string mats = "";
                if (b != null)
                    foreach (Renderer r in b.GetComponentsInChildren<Renderer>(true))
                        foreach (Material m in r.sharedMaterials)
                            if (m != null) mats += m.name + ",";
                Debug.Log("CAPTURA_MATERIALES id=" + par.Item1 + " cuerpo=" + (b != null) + " mats=" + mats);
            }

            yield return null;
            if (!Application.isBatchMode) yield return new WaitForEndOfFrame();

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
            string path = Path.Combine(dir, "modelos_unity.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("CAPTURA_ARTE=" + path);

            camGo.transform.position = new Vector3(0.45f, 1.55f, 4.6f);
            camGo.transform.LookAt(new Vector3(0.45f, 0.85f, 0f));
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            string pathFrente = Path.Combine(dir, "modelos_unity_frente.png");
            File.WriteAllBytes(pathFrente, tex.EncodeToPNG());
            Debug.Log("CAPTURA_ARTE_FRENTE=" + pathFrente);

            // Segunda foto: las 8 salvajes que se ven de cerca en las cuevas.
            hero.SetActive(false);
            s1.SetActive(false);
            s2.SetActive(false);
            s3.SetActive(false);
            string[] salvajesIds =
            {
                "corriente", "centella", "guijarro", "marea",
                "tenue", "roquito", "liana", "gotita"
            };
            GameObject[] salvajes = new GameObject[salvajesIds.Length];
            for (int i = 0; i < salvajesIds.Length; i++)
            {
                string id = salvajesIds[i];
                SlugData d = SlugRoster.Buscar(id);
                Assert.IsNotNull(d, "falta el SlugData " + id);
                GameObject go = new GameObject("salvaje_" + id);
                go.transform.position = new Vector3(-1.75f + i * 0.5f, 1f, 0f);
                salvajes[i] = go;
                object[] args = { go, d, new Vector3(0f, -1f, 0f), false };
                bool montado = (bool)modelo.Invoke(null, args);
                Assert.IsTrue(montado, id + ": AttachModel no monto el GLB _pixar");
                Transform b = go.transform.Find("Cuerpo_" + id);
                Assert.IsNotNull(b, id + ": no monto Cuerpo_" + id);
                string mats = "";
                foreach (Renderer r in b.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        if (m != null) mats += m.name + ",";
                Debug.Log("CAPTURA_SALVAJE id=" + id + " mats=" + mats);
            }
            cam.fieldOfView = 40f;
            camGo.transform.position = new Vector3(0f, 0.95f, 3.8f);
            camGo.transform.LookAt(new Vector3(0f, 0.32f, 0f));
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            string pathSalvajes = Path.Combine(dir, "salvajes_unity.png");
            File.WriteAllBytes(pathSalvajes, tex.EncodeToPNG());
            Debug.Log("CAPTURA_ARTE_SALVAJES=" + pathSalvajes);

            // Tercera foto: dos filas del roster de 5 materiales, una especie por elemento.
            foreach (GameObject go in salvajes) go.SetActive(false);
            string[] rosterIds =
            {
                "bengala", "leviatan", "bloque", "tornado", "zeus", "ventisca",
                "selva", "enigma", "vacio", "chispa", "huracan", "elemental-agua"
            };
            GameObject[] roster = new GameObject[rosterIds.Length];
            for (int i = 0; i < rosterIds.Length; i++)
            {
                string id = rosterIds[i];
                SlugData d = SlugRoster.Buscar(id);
                Assert.IsNotNull(d, "falta el SlugData " + id);
                GameObject go = new GameObject("roster_" + id);
                int fila = i / 6;
                int col = i % 6;
                go.transform.position = new Vector3(-1.05f + col * 0.42f, 1f, -fila * 0.8f);
                roster[i] = go;
                object[] args = { go, d, new Vector3(0f, -1f, 0f), false };
                bool montado = (bool)modelo.Invoke(null, args);
                Assert.IsTrue(montado, id + ": AttachModel no monto el GLB _pixar");
                Transform b = go.transform.Find("Cuerpo_" + id);
                Assert.IsNotNull(b, id + ": no monto Cuerpo_" + id);
            }
            cam.fieldOfView = 40f;
            camGo.transform.position = new Vector3(0f, 1.05f, 4.2f);
            camGo.transform.LookAt(new Vector3(0f, 0.35f, -0.4f));
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            string pathRoster = Path.Combine(dir, "roster_unity.png");
            File.WriteAllBytes(pathRoster, tex.EncodeToPNG());
            Debug.Log("CAPTURA_ARTE_ROSTER=" + pathRoster);

            RenderTexture.active = null;
            cam.targetTexture = null;

            Object.Destroy(tex);
            Object.Destroy(rt);
            Object.Destroy(camGo);
            Object.Destroy(key);
            Object.Destroy(fill);
            Object.Destroy(floor);
            Object.Destroy(hero);
            Object.Destroy(s1);
            Object.Destroy(s2);
            Object.Destroy(s3);
            foreach (GameObject go in salvajes) Object.Destroy(go);
            foreach (GameObject go in roster) Object.Destroy(go);
        }
    }
}
