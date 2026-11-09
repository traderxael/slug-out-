using System.Collections;
using System.IO;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// La cuarta cueva no es solo una fila en la tabla: tiene que construirse con sus tres
    /// siluetas, sus tres bocas y sus coberturas jugables. Esta prueba arma la region sola,
    /// sin cargar Mapa3D, para que el costo sea bajo y el fallo apunte a la region.
    public class CuartaRegionPlayTests
    {
        [UnityTest]
        public IEnumerator LaCuartaRegionArmaSusTresZonasCoberturasYCapa8()
        {
            // Escena propia: si otra prueba dejo Mapa3D cargado, construir aca no debe tocar
            // sus paredes ni su terreno, y la region no se lleva estado de vuelta al suite.
            Scene previa = SceneManager.GetActiveScene();
            Scene prueba = SceneManager.GetSceneByName("RegionPruebaCeniza");
            if (!prueba.IsValid()) prueba = SceneManager.CreateScene("RegionPruebaCeniza");
            SceneManager.SetActiveScene(prueba);

            var go = new GameObject("RegionPruebaCanon");
            go.SetActive(false);   // Awake no debe construir la region 0 antes de tiempo
            var region = go.AddComponent<RegionCuevaSeca>();

            Vector2 desfaseAntes = Terrain3D.DesfaseRegion;
            float amplitudAntes = Terrain3D.AmplitudRegion;
            Color ambienteAntes = RenderSettings.ambientLight;
            bool nieblaAntes = RenderSettings.fog;
            float nieblaInicioAntes = RenderSettings.fogStartDistance;
            float nieblaFinAntes = RenderSettings.fogEndDistance;
            Color nieblaColorAntes = RenderSettings.fogColor;

            try
            {
                Transform boca;
                region.Reconstruir(3, out boca, 0);
                go.SetActive(true);   // activa los colliders de la region recien armada
                yield return null;
                Physics.SyncTransforms();

                Assert.AreEqual("EL CAÑON DE CENIZA", RegionCuevaSeca.NombreRegionActual);
                Assert.That(boca, Is.Not.Null, "la cuarta region tiene que tener salida hacia la cueva 0");
                Assert.AreEqual("Tunel_LA_CUEVA_SECA", boca.name,
                    "salir por la boca que apunta a la region de origen es lo que hace que el viaje se lea");
                Assert.AreEqual(3, region.Microhabitats, "son tres zonas, no dos ni cuatro");
                Assert.AreEqual(3, region.TunelesRaiz.Length, "una boca por cada cueva vecina");

                AssertGrupo(region, "Microhabitat_ChimeneaCeniza");
                AssertGrupo(region, "Microhabitat_PuenteRoto");
                AssertGrupo(region, "Microhabitat_LagunaAzufre");

                Assert.AreEqual(3, ContarHijos(region.Root, "Cobertura_"),
                    "la region anuncia tres coberturas");
                Transform cobertura = region.Root.Find("Cobertura_0");
                Assert.That(cobertura, Is.Not.Null);
                Assert.AreEqual(3, cobertura.GetComponentsInChildren<MeshRenderer>(true).Length,
                    "la cobertura tiene que verse como roca, no como tres cubos vacios");
                Assert.AreEqual(0, cobertura.GetComponentsInChildren<Collider>(true).Length,
                    "la piedra visible no lleva collider: el proxy de capa 8 manda");
                Transform muros = region.Root.Find("Muros");
                Assert.That(muros, Is.Not.Null, "las coberturas necesitan su proxy de colision");
                int proxies = 0;
                for (int i = 0; i < muros.childCount; i++)
                {
                    Transform t = muros.GetChild(i);
                    if (!t.name.StartsWith("muro_cobertura_")) continue;
                    proxies++;
                    Assert.AreEqual(ColisionJugador.CapaMuros, t.gameObject.layer,
                        "el proxy tiene que estar en la capa que corta tiros y frena al jugador");
                    BoxCollider caja = t.GetComponent<BoxCollider>();
                    Assert.That(caja, Is.Not.Null, t.name + ": sin collider no frena nada");
                    if (caja == null) continue;

                    // La cobertura tapa de verdad: una linea que la cruza no esta despejada.
                    Vector3 centro = caja.bounds.center;
                    float fuera = Mathf.Max(caja.bounds.extents.x, caja.bounds.extents.z) + 1.5f;
                    Assert.IsFalse(SlugShot.LineaDespejada(centro + t.forward * fuera,
                        centro - t.forward * fuera),
                        t.name + ": el tiro atraviesa la cobertura");
                }
                Assert.AreEqual(3, proxies, "una cobertura visible sin proxy no corta ningun tiro");

                Assert.That(Terrain3D.DesfaseRegion, Is.EqualTo(Regiones.De(3).relieveDesfase));
                Assert.That(Terrain3D.AmplitudRegion, Is.EqualTo(Regiones.De(3).relieveAmplitud));
                Assert.AreEqual(0f, Terrain3D.Altura(0f, 0f), 0.0001f,
                    "la plaza de la cuarta region tambien tiene que ser plana");
            }
            finally
            {
                region.Reconstruir(0, out _);   // deja Actual y el relieve como estaban
                Terrain3D.DesfaseRegion = desfaseAntes;
                Terrain3D.AmplitudRegion = amplitudAntes;
                RenderSettings.ambientLight = ambienteAntes;
                RenderSettings.fog = nieblaAntes;
                RenderSettings.fogStartDistance = nieblaInicioAntes;
                RenderSettings.fogEndDistance = nieblaFinAntes;
                RenderSettings.fogColor = nieblaColorAntes;
                if (previa.IsValid()) SceneManager.SetActiveScene(previa);
                Object.Destroy(go);
                if (prueba.IsValid()) SceneManager.UnloadSceneAsync(prueba);
            }
        }

        [UnityTest]
        public IEnumerator LasCuatroRegionesArmanTresCoberturasEnCapa8()
        {
            Scene previa = SceneManager.GetActiveScene();
            Scene prueba = SceneManager.GetSceneByName("RegionPruebaCoberturas");
            if (!prueba.IsValid()) prueba = SceneManager.CreateScene("RegionPruebaCoberturas");
            SceneManager.SetActiveScene(prueba);

            var go = new GameObject("RegionPruebaCoberturas");
            go.SetActive(false);
            var region = go.AddComponent<RegionCuevaSeca>();
            Vector2 desfaseAntes = Terrain3D.DesfaseRegion;
            float amplitudAntes = Terrain3D.AmplitudRegion;
            try
            {
                for (int r = 0; r < Regiones.Todas.Length; r++)
                {
                    region.Reconstruir(r, out _);
                    go.SetActive(true);
                    yield return null;
                    Physics.SyncTransforms();

                    Assert.AreEqual(3, region.Microhabitats,
                        Regiones.De(r).nombre + ": la region no armo sus tres zonas");
                    Transform muros = region.Root.Find("Muros");
                    Assert.That(muros, Is.Not.Null, Regiones.De(r).nombre + ": sin proxies de cobertura");
                    int proxies = 0;
                    for (int i = 0; i < muros.childCount; i++)
                    {
                        Transform t = muros.GetChild(i);
                        if (!t.name.StartsWith("muro_cobertura_")) continue;
                        proxies++;
                        Assert.AreEqual(ColisionJugador.CapaMuros, t.gameObject.layer,
                            Regiones.De(r).nombre + ": el proxy quedo fuera de la capa de muros");
                    }
                    Assert.AreEqual(3, proxies,
                        Regiones.De(r).nombre + ": faltan coberturas jugables (" + proxies + ")");
                }
            }
            finally
            {
                region.Reconstruir(0, out _);
                Terrain3D.DesfaseRegion = desfaseAntes;
                Terrain3D.AmplitudRegion = amplitudAntes;
                if (previa.IsValid()) SceneManager.SetActiveScene(previa);
                Object.Destroy(go);
                if (prueba.IsValid()) SceneManager.UnloadSceneAsync(prueba);
            }
        }

        [UnityTest]
        public IEnumerator CapturaLaCoberturaNueva()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("batch sin dispositivo grafico: no se puede capturar");

            Scene previa = SceneManager.GetActiveScene();
            Scene prueba = SceneManager.GetSceneByName("RegionPruebaFoto");
            if (!prueba.IsValid()) prueba = SceneManager.CreateScene("RegionPruebaFoto");
            SceneManager.SetActiveScene(prueba);
            var go = new GameObject("RegionFoto");
            go.SetActive(false);
            var region = go.AddComponent<RegionCuevaSeca>();
            var camGo = new GameObject("cam_foto");
            var luzGo = new GameObject("luz_foto");
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                region.Reconstruir(0, out _);
                go.SetActive(true);
                yield return null;
                Physics.SyncTransforms();
                Transform cob = region.Root.Find("Cobertura_0");
                Assert.That(cob, Is.Not.Null);

                var piso = GameObject.CreatePrimitive(PrimitiveType.Plane);
                piso.transform.position = new Vector3(0f, -0.01f, 0f);
                piso.transform.localScale = Vector3.one * 8f;
                piso.transform.SetParent(go.transform, true);
                Object.Destroy(piso.GetComponent<Collider>());

                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
                cam.fieldOfView = 40f;
                cam.transform.position = cob.position + new Vector3(6.5f, 3.4f, -6.5f);
                cam.transform.LookAt(cob.position + Vector3.up * 1.3f);

                Light luz = luzGo.AddComponent<Light>();
                luz.type = LightType.Directional;
                luz.color = new Color(1f, 0.88f, 0.72f);
                luz.intensity = 1.35f;
                luz.shadows = LightShadows.None;
                luz.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
                var relleno = new GameObject("luz_relleno");
                relleno.transform.SetParent(luzGo.transform, false);
                Light fl = relleno.AddComponent<Light>();
                fl.type = LightType.Directional;
                fl.color = new Color(0.55f, 0.72f, 1f);
                fl.intensity = 0.5f;
                fl.shadows = LightShadows.None;
                relleno.transform.rotation = Quaternion.Euler(18f, 140f, 0f);

                rt = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(1024, 768, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                tex.Apply();
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                    "Screenshots", "region"));
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, "cobertura_3d.png"), tex.EncodeToPNG());
                RenderTexture.active = null;
                cam.targetTexture = null;
            }
            finally
            {
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) Object.DestroyImmediate(rt);
                Object.Destroy(camGo);
                Object.Destroy(luzGo);
                if (previa.IsValid()) SceneManager.SetActiveScene(previa);
                Object.Destroy(go);
                if (prueba.IsValid()) SceneManager.UnloadSceneAsync(prueba);
            }
        }

        static void AssertGrupo(RegionCuevaSeca region, string nombre)
        {
            Assert.That(region.Root.Find(nombre), Is.Not.Null,
                "falta la silueta " + nombre + " en la cuarta region");
        }

        static int ContarHijos(Transform raiz, string prefijo)
        {
            int n = 0;
            for (int i = 0; i < raiz.childCount; i++)
                if (raiz.GetChild(i).name.StartsWith(prefijo)) n++;
            return n;
        }
    }
}
