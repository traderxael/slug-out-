using System.Collections;
using System.IO;
using NUnit.Framework;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// Capturas reproducibles de las cuatro cuevas para revisar a ojo la identidad, los props
    /// flotantes y los bordes del terreno. Son cuatro pruebas separadas porque el puente de IA
    /// del editor puede abortar una corrida con un log de conexion externo; asi no se pierden
    /// las otras tres capturas.
    public class CuevasCapturaPlayTests
    {
        [UnityTest] public IEnumerator CapturaCueva0() { yield return Capturar(0); }
        [UnityTest] public IEnumerator CapturaCueva1() { yield return Capturar(1); }
        [UnityTest] public IEnumerator CapturaCueva2() { yield return Capturar(2); }
        [UnityTest] public IEnumerator CapturaCueva3() { yield return Capturar(3); }

        private static IEnumerator Capturar(int r)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("batch sin dispositivo grafico: no se puede capturar");
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            if (GameFlow.Activo != null) GameFlow.Activo.IniciarCampana(1);
            yield return null;
            RegionCuevaSeca region = Object.FindAnyObjectByType<RegionCuevaSeca>();
            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            Camera cam = Camera.main;
            SlugArenaGame arena = Object.FindAnyObjectByType<SlugArenaGame>();
            GameHUD hud = Object.FindAnyObjectByType<GameHUD>();
            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            Assert.IsNotNull(region);
            Assert.IsNotNull(pc);
            Assert.IsNotNull(cam);
            if (arena != null) arena.enabled = false;
            if (hud != null) hud.enabled = false;
            if (historia != null) historia.enabled = false;

            EnemySlugAI[] ias = Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++) if (ias[i] != null) ias[i].gameObject.SetActive(false);

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "terreno-cuevas"));
            Directory.CreateDirectory(dir);
            const int w = 1024, h = 576;
            region.Reconstruir(r, out _);
            yield return null; yield return null;
            pc.Colocar(Vector3.zero);
            for (int i = 0; i < 8; i++) yield return null;

            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            string path = Path.Combine(dir, "cueva_" + r + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("CAPTURA_CUEVA=" + path);
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.Destroy(tex);
            Object.Destroy(rt);
        }
    }
}
