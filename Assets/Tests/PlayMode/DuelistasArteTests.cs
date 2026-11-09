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
    /// Verifica el arte nuevo en el motor sin depender de llegar a un rival: carga los
    /// cuatro FBX regenerados, comprueba que siguen riggeados y que traen los materiales
    /// de cara. Si el import rompiera el modelo, DuelistaMazo.Cargar devolveria null.
    public class DuelistasArteTests
    {
        [Test]
        public void LosCuatroDuelistasTraenSkinYCaraNueva()
        {
            string[] ids = { "kord", "trixie", "pronto", "dr-blakk" };
            foreach (string id in ids)
            {
                GameObject modelo = DuelistaMazo.Cargar(id);
                Assert.IsNotNull(modelo, "no carga Resources/Duelists/" + id);

                SkinnedMeshRenderer smr = modelo.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.IsNotNull(smr, id + " no tiene SkinnedMeshRenderer");

                bool cara = false;
                foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && (m.name.Contains("_boca") || m.name.Contains("_ojo")
                                          || m.name.Contains("_pupila")))
                            cara = true;
                Assert.IsTrue(cara, id + " no trae materiales de cara nuevos");
            }
        }

        /// Captura a los cuatro con el tintado real de SlugArenaGame.AttachDuelista. Un
        /// render de Blender no prueba que los ribetes _tubos salgan emisivos en URP ni
        /// que el negro de Blakk se recorte contra la cueva; esta toma si.
        [UnityTest]
        public IEnumerator CapturaLosCuatroConTintadoDelJuego()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("batch sin dispositivo grafico: no se puede capturar");

            MethodInfo montar = typeof(SlugArenaGame).GetMethod("AttachDuelista",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(montar, "se renombro AttachDuelista y esta captura deja de probar nada");

            GameObject camGo = new GameObject("duelistas_cam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.025f, 0.05f, 1f);
            cam.fieldOfView = 40f;

            GameObject keyGo = new GameObject("duelistas_key");
            Light kl = keyGo.AddComponent<Light>();
            kl.type = LightType.Directional;
            kl.color = new Color(1f, 0.86f, 0.72f);
            kl.intensity = 1.1f;
            kl.shadows = LightShadows.None;
            keyGo.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

            GameObject fillGo = new GameObject("duelistas_fill");
            Light fl = fillGo.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.color = new Color(0.75f, 0.82f, 1f);
            fl.intensity = 0.45f;
            fl.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(15f, 130f, 0f);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "duelistas_piso";
            floor.transform.localScale = Vector3.one * 8f;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material piso = new Material(lit);
            piso.color = new Color(0.02f, 0.03f, 0.045f);
            if (piso.HasProperty("_Smoothness")) piso.SetFloat("_Smoothness", 0.35f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = piso;

            string[] ids = { "kord", "trixie", "pronto", "dr-blakk" };
            string[] babosas = { "infierno", "helada", "electroshock", "ghoul" };
            for (int i = 0; i < ids.Length; i++)
            {
                GameObject actor = new GameObject("duelistas_" + ids[i]);
                actor.transform.position = new Vector3(-1.35f + i * 0.9f, 0f, 0f);
                bool montado = (bool)montar.Invoke(null, new object[]
                {
                    actor, ids[i], SlugRoster.Buscar(babosas[i])
                });
                Assert.IsTrue(montado, "no se monto " + ids[i]);
            }

            yield return null;
            if (!Application.isBatchMode) yield return new WaitForEndOfFrame();

            const int w = 1280;
            const int h = 720;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review"));
            Directory.CreateDirectory(dir);

            for (int toma = 0; toma < 2; toma++)
            {
                // El exportador deja al personaje mirando a +Z (docstring de
                // build_personajes): la toma de frente va del lado +Z.
                camGo.transform.position = new Vector3(0.45f, 1.25f, toma == 0 ? 4.2f : -4.2f);
                camGo.transform.LookAt(new Vector3(0.45f, 0.95f, 0f));
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                string path = Path.Combine(dir,
                    toma == 0 ? "duelistas_unity_frente.png" : "duelistas_unity_espalda.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log("CAPTURA_DUELISTAS=" + path);
            }

            cam.targetTexture = null;
            RenderTexture.active = null;
            rt.Release();
            Object.Destroy(camGo);
            Object.Destroy(keyGo);
            Object.Destroy(fillGo);
            Object.Destroy(floor);
            for (int i = 0; i < ids.Length; i++)
            {
                GameObject actor = GameObject.Find("duelistas_" + ids[i]);
                if (actor != null) Object.Destroy(actor);
            }
        }
    }
}
