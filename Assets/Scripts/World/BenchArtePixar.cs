using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SlugOut.Core;
using SlugOut.Slugs;
using UnityEngine;

namespace SlugOut.World
{
    /// Solo se activa con -benchArte en el player. Inicia la campana con Electroshock,
    /// calienta 5 s y muestrea 20 s; escribe FPS promedio, 1% low y p95 en el log.
    public class BenchArtePixar : MonoBehaviour
    {
        private bool _modoDuelo;
        private bool _modoRuta;
        private bool _modoColeccion;
        private bool _modoVinculo;
        private bool _modoPantallas;
        private bool _modoAnim;
        private bool _modoRoster;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Iniciar()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != "-benchArte" && args[i] != "-benchDuelo"
                    && args[i] != "-benchRuta" && args[i] != "-benchColeccion"
                    && args[i] != "-benchVinculo" && args[i] != "-benchPantallas"
                    && args[i] != "-benchAnim" && args[i] != "-benchRoster") continue;
                GameObject go = new GameObject("BenchArtePixar");
                Object.DontDestroyOnLoad(go);
                BenchArtePixar b = go.AddComponent<BenchArtePixar>();
                b._modoDuelo = args[i] == "-benchDuelo";
                b._modoRuta = args[i] == "-benchRuta";
                b._modoColeccion = args[i] == "-benchColeccion";
                b._modoVinculo = args[i] == "-benchVinculo";
                b._modoPantallas = args[i] == "-benchPantallas";
                b._modoAnim = args[i] == "-benchAnim";
                b._modoRoster = args[i] == "-benchRoster";
                return;
            }
        }

        private IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            string capturas = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Screenshots", "arte-pixar"));
            Directory.CreateDirectory(capturas);

            if (_modoDuelo)
            {
                yield return CapturarDuelo(capturas);
                yield break;
            }
            if (_modoRuta)
            {
                yield return CapturarRuta(capturas);
                yield break;
            }
            if (_modoColeccion)
            {
                yield return CapturarColeccion(capturas);
                yield break;
            }
            if (_modoVinculo)
            {
                yield return CapturarVinculo(capturas);
                yield break;
            }
            if (_modoPantallas)
            {
                yield return CapturarPantallas(capturas);
                yield break;
            }
            if (_modoAnim)
            {
                yield return CapturarAnim();
                yield break;
            }
            if (_modoRoster)
            {
                yield return CapturarRoster(capturas);
                yield break;
            }

            // La captura incluye IMGUI porque la toma el compositor, no la camara: es la unica
            // forma de ver el HUD real sin abrir el Editor.
            yield return new WaitForSecondsRealtime(1f);
            ScreenCapture.CaptureScreenshot(Path.Combine(capturas, "menu_campana.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            if (GameFlow.Activo != null) GameFlow.Activo.IniciarCampana(1);
            yield return new WaitForSecondsRealtime(1f);
            SlugArenaGame arena = Object.FindAnyObjectByType<SlugArenaGame>();
            if (arena != null) arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            DesactivarRocas(arena);
            yield return new WaitForSecondsRealtime(5f);
            DesactivarRocas(arena);

            // El panel de historia queda abierto hasta que el jugador lo cierra: tapa el HUD
            // en una captura automatica. Se apaga solo para la foto y las fases de medicion.
            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            yield return new WaitForSecondsRealtime(0.6f);

            ScreenCapture.CaptureScreenshot(Path.Combine(capturas, "hud_combate.png"));
            Debug.Log("CAPTURA_HUD=" + Path.Combine(capturas, "hud_combate.png"));
            yield return new WaitForSecondsRealtime(0.4f);
            ScreenCapture.CaptureScreenshot(Path.Combine(capturas, "hud_combate_2.png"));
            Debug.Log("CAPTURA_HUD=" + Path.Combine(capturas, "hud_combate_2.png"));

            GameHUD hud = Object.FindAnyObjectByType<GameHUD>();
            GameObject cueva = GameObject.Find("CuevaPixar");
            Light[] luces = cueva != null ? cueva.GetComponentsInChildren<Light>(true) : null;

            yield return Medir("A_full", 6f, true);
            if (luces != null)
                for (int i = 0; i < luces.Length; i++) luces[i].enabled = false;
            yield return Medir("B_sin_luces_cueva", 6f, false);
            if (cueva != null) cueva.SetActive(false);
            yield return Medir("C_sin_cueva", 6f, false);
            if (hud != null) hud.enabled = false;
            yield return Medir("D_sin_hud", 6f, false);

            if (hud != null) hud.enabled = true;
            if (cueva != null) cueva.SetActive(true);
            if (luces != null)
                for (int i = 0; i < luces.Length; i++) luces[i].enabled = true;
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        /// Captura el prompt de vinculo: arranca la campana, apaga la historia y pone una
        /// babosa salvaje a tres metros del jugador, en su campo de vision.
        private static void DesactivarRocas(SlugArenaGame arena)
        {
            RockShower[] todos = Object.FindObjectsByType<RockShower>();
            for (int i = 0; i < todos.Length; i++) todos[i].Activar(false);
            if (arena != null)
            {
                RockShower rs = arena.GetComponent<RockShower>();
                if (rs != null) rs.Activar(false);
            }
            // Las rocas ya soltadas siguen cayendo aunque el emisor este apagado.
            GameObject[] objetos = Object.FindObjectsByType<GameObject>();
            for (int i = 0; i < objetos.Length; i++)
            {
                if (objetos[i] != null && objetos[i].name == "Roca")
                    Object.Destroy(objetos[i]);
            }
        }

        private static IEnumerator CapturarVinculo(string capturas)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (GameFlow.Activo != null) GameFlow.Activo.IniciarCampana(1);
            yield return new WaitForSecondsRealtime(1f);
            SlugArenaGame arena = Object.FindAnyObjectByType<SlugArenaGame>();
            if (arena != null) arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            DesactivarRocas(arena);
            yield return new WaitForSecondsRealtime(1.5f);

            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;

            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            if (pc != null)
            {
                GameObject go = new GameObject("SalvajePrueba");
                go.transform.position = pc.transform.position + pc.transform.forward * 3.2f
                    + pc.transform.right * 1.6f + Vector3.up * 0.02f;
                BabosaSalvaje b = go.AddComponent<BabosaSalvaje>();
                b.slugId = "electroshock";
                b.habitat = "Fumarolas";
            }
            yield return new WaitForSecondsRealtime(1.2f);
            DesactivarRocas(arena);

            string foto = Path.Combine(capturas, "vinculo.png");
            ScreenCapture.CaptureScreenshot(foto);
            Debug.Log("CAPTURA_VINCULO=" + foto);
            yield return new WaitForSecondsRealtime(0.8f);
            Application.Quit();
        }

        /// Captura las subpantallas del menú sin navegar con clics.
        private static IEnumerator CapturarPantallas(string capturas)
        {
            yield return new WaitForSecondsRealtime(1f);
            GameFlow gf = GameFlow.Activo;
            var campo = typeof(GameFlow).GetField("_pantalla",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (gf == null || campo == null) { Application.Quit(); yield break; }
            string[] pantallas = { "Niveles", "Babosas", "Ajustes" };
            for (int i = 0; i < pantallas.Length; i++)
            {
                campo.SetValue(gf, System.Enum.Parse(campo.FieldType, pantallas[i]));
                yield return new WaitForSecondsRealtime(0.8f);
                string foto = Path.Combine(capturas, "menu_" + pantallas[i].ToLowerInvariant() + ".png");
                if (File.Exists(foto)) File.Delete(foto);
                ScreenCapture.CaptureScreenshot(foto);
                Debug.Log("CAPTURA_PANTALLA=" + foto);
                // La captura se escribe al final del cuadro; no cambies de pantalla hasta que
                // exista en disco, o la primera foto sale con la pantalla siguiente.
                for (int k = 0; k < 90 && !File.Exists(foto); k++) yield return null;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        /// Captura la animacion del heroe sin depender del Editor: apuntar de frente y
        /// de perfil, caminata, carrera y rodada. El movimiento es scriptado con la misma
        /// ley de velocidad del PlayerController; la rodada entra por IntentarRodada(),
        /// el mismo metodo que usa el borde de Shift.
        private static IEnumerator CapturarAnim()
        {
            string dir = CarpetaAnim();
            yield return new WaitForSecondsRealtime(1f);
            if (GameFlow.Activo != null) GameFlow.Activo.IniciarCampana(1);
            yield return new WaitForSecondsRealtime(1f);
            SlugArenaGame arena = Object.FindAnyObjectByType<SlugArenaGame>();
            if (arena != null) arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            DesactivarRocas(arena);
            yield return new WaitForSecondsRealtime(2f);
            DesactivarRocas(arena);

            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameHUD hud = Object.FindAnyObjectByType<GameHUD>();
            if (hud != null) hud.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;

            // La IA enemiga no debe interrumpir las poses con tiros o embestidas.
            EnemySlugAI[] ias = Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++) ias[i].enabled = false;

            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            PlayerDodge dodge = pc != null ? pc.GetComponent<PlayerDodge>() : null;
            DuelistaAnimator animHeroe = pc != null ? pc.GetComponent<DuelistaAnimator>() : null;
            if (pc == null)
            {
                Debug.LogWarning("[BenchAnim] no hay PlayerController; no se puede capturar.");
                Application.Quit();
                yield break;
            }

            GameObject camGo = new GameObject("cam_anim");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.025f, 0.05f, 1f);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.enabled = false;
            RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);

            yield return new WaitForSecondsRealtime(0.5f);
            Vector3 foco = pc.transform.position + Vector3.up * 0.65f;
            Foto(cam, tex, Path.Combine(dir, "apuntar_frente.png"),
                pc.transform.position + pc.transform.forward * 3.4f + Vector3.up * 1.4f, foco);
            Foto(cam, tex, Path.Combine(dir, "apuntar_lado.png"),
                pc.transform.position + pc.transform.right * 3.4f + Vector3.up * 1.4f, foco);
            yield return null;

            yield return MoverCapturando(pc, 0.8f, 0.9f, cam, tex, Path.Combine(dir, "caminata.png"));
            yield return MoverCapturando(pc, 6f, 0.5f, cam, tex, Path.Combine(dir, "carrera.png"));

            bool rodada = dodge != null && dodge.IntentarRodada();
            Debug.Log("CAPTURA_ANIM_RODADA intento=" + rodada);
            yield return new WaitForSecondsRealtime(0.14f);
            foco = pc.transform.position + Vector3.up * 0.55f;
            Foto(cam, tex, Path.Combine(dir, "rodada_media.png"),
                pc.transform.position + pc.transform.right * 3.4f + Vector3.up * 1.2f, foco);
            yield return new WaitForSecondsRealtime(0.30f);
            foco = pc.transform.position + Vector3.up * 0.65f;
            Foto(cam, tex, Path.Combine(dir, "rodada_fin.png"),
                pc.transform.position + pc.transform.right * 3.4f + Vector3.up * 1.4f, foco);

            yield return new WaitForSecondsRealtime(0.4f);

            // Clip de cuerpo completo: impacto en el pico del sobre (0,26 s).
            if (animHeroe != null)
            {
                animHeroe.AnimarImpacto();
                yield return new WaitForSecondsRealtime(0.13f);
                // Desde arriba y cerca: a la altura de los ojos los hongos del claro
                // tapaban justo el torso en las capturas anteriores.
                foco = pc.transform.position + Vector3.up * 0.8f;
                Foto(cam, tex, Path.Combine(dir, "impacto.png"),
                    pc.transform.position + pc.transform.right * 2.4f + Vector3.up * 2f, foco);
                yield return new WaitForSecondsRealtime(0.35f);
            }

            // Capturas con IMGUI: el aviso de captura y el panel de resultado se
            // dibujan con OnGUI, asi que van por el compositor y no por la camara RT.
            if (hud != null) hud.enabled = true;
            yield return new WaitForSecondsRealtime(0.4f);
            SlugActor presa = null;
            var vivos = SlugActor.Vivos;
            for (int i = 0; i < vivos.Count; i++)
            {
                SlugActor a = vivos[i];
                if (a != null && !a.IsPlayer && a.Alive && !a.Capturada) { presa = a; break; }
            }
            if (presa != null) presa.Capturar();
            yield return new WaitForSecondsRealtime(0.7f);
            string fotoCaptura = Path.Combine(dir, "hud_captura.png");
            ScreenCapture.CaptureScreenshot(fotoCaptura);
            Debug.Log("CAPTURA_HUD_FASE1=" + fotoCaptura + " toast=" + (hud != null && hud.ToastVisible));
            yield return new WaitForSecondsRealtime(0.9f);

            if (animHeroe != null)
            {
                animHeroe.AnimarMuerteParaCaptura();
                yield return new WaitForSecondsRealtime(0.9f);
                foco = pc.transform.position + Vector3.up * 0.4f;
                string fotoMuerte = Path.Combine(dir, "muerte.png");
                Foto(cam, tex, fotoMuerte,
                    pc.transform.position + pc.transform.right * 2.4f + Vector3.up * 1.9f, foco);
                Debug.Log("CAPTURA_ANIM_MUERTE=" + fotoMuerte);
            }

            SlugActor jugador = pc.GetComponent<SlugActor>();
            if (jugador != null) jugador.TakeDamage(99999f, ElementType.Fire);
            yield return new WaitForSecondsRealtime(1.2f);
            string fotoResultado = Path.Combine(dir, "hud_resultado.png");
            ScreenCapture.CaptureScreenshot(fotoResultado);
            Debug.Log("CAPTURA_RESULTADO_FASE1=" + fotoResultado
                + " visible=" + (hud != null && hud.ResultadoVisible));
            yield return new WaitForSecondsRealtime(0.9f);

            Debug.Log("CAPTURA_ANIM_DIR=" + dir);
            Object.Destroy(tex);
            Object.Destroy(rt);
            Object.Destroy(camGo);
            Application.Quit();
        }

        /// En el player la raiz del proyecto queda tres niveles arriba de la carpeta _Data;
        /// en el Editor, Assets esta un nivel abajo. Misma carpeta en ambos casos.
        private static string CarpetaAnim()
        {
            string raiz = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "anim-heroe"))
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Screenshots", "anim-heroe"));
            Directory.CreateDirectory(raiz);
            return raiz;
        }

        private static IEnumerator MoverCapturando(PlayerController pc, float velocidad,
            float segundos, Camera cam, Texture2D tex, string ruta)
        {
            float t = 0f;
            while (t < segundos)
            {
                float dt = Time.deltaTime;
                pc.transform.position += pc.transform.forward * (velocidad * dt);
                pc.ApoyarEnPiso();
                t += dt;
                yield return null;
            }
            Foto(cam, tex, ruta,
                pc.transform.position + pc.transform.right * 3.4f + Vector3.up * 1.3f,
                pc.transform.position + Vector3.up * 0.65f);
        }

        private static void Foto(Camera cam, Texture2D tex, string ruta, Vector3 pos, Vector3 mira)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(mira);
            cam.Render();
            RenderTexture anterior = RenderTexture.active;
            RenderTexture.active = cam.targetTexture;
            tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            RenderTexture.active = anterior;
            Debug.Log("CAPTURA_ANIM=" + ruta);
        }

        private static IEnumerator CapturarColeccion(string capturas)
        {
            yield return new WaitForSecondsRealtime(1f);
            CollectionUI.Abrir();
            yield return new WaitForSecondsRealtime(1f);
            string foto = Path.Combine(capturas, "coleccion.png");
            ScreenCapture.CaptureScreenshot(foto);
            Debug.Log("CAPTURA_COLECCION=" + foto);
            yield return new WaitForSecondsRealtime(0.8f);
            Application.Quit();
        }

        /// Captura el banner de historia y despues la ruta abierta (misma pausa que TAB).
        private static IEnumerator CapturarRuta(string capturas)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (GameFlow.Activo != null) GameFlow.Activo.IniciarCampana(1);
            yield return new WaitForSecondsRealtime(1f);
            SlugArenaGame arena = Object.FindAnyObjectByType<SlugArenaGame>();
            if (arena != null) arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            DesactivarRocas(arena);
            yield return new WaitForSecondsRealtime(2.2f);
            DesactivarRocas(arena);

            string fragmento = Path.Combine(capturas, "historia_fragmento.png");
            ScreenCapture.CaptureScreenshot(fragmento);
            Debug.Log("CAPTURA_RUTA=" + fragmento);
            yield return new WaitForSecondsRealtime(0.5f);

            StoryPanel[] paneles = Object.FindObjectsByType<StoryPanel>();
            var campo = typeof(StoryPanel).GetField("rutaAbierta",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var propiedad = typeof(StoryPanel).GetProperty("RutaVisible");
            for (int i = 0; i < paneles.Length; i++)
            {
                if (campo != null) campo.SetValue(paneles[i], true);
                var set = propiedad != null ? propiedad.GetSetMethod(true) : null;
                if (set != null) set.Invoke(paneles[i], new object[] { true });
            }
            GameFeel.Pausa = true;
            Debug.Log("PANELES_HISTORIA=" + paneles.Length);
            yield return new WaitForSecondsRealtime(0.8f);

            string ruta = Path.Combine(capturas, "ruta_cueva.png");
            ScreenCapture.CaptureScreenshot(ruta);
            Debug.Log("CAPTURA_RUTA=" + ruta + " paneles=" + paneles.Length);
            yield return new WaitForSecondsRealtime(0.8f);
            Application.Quit();
        }

        /// Captura la pantalla de duelo sin depender del Editor. Ademas juega un turno por
        /// reflexion para que la segunda foto muestre el registro y las barras actualizadas.
        private static IEnumerator CapturarDuelo(string capturas)
        {
            yield return new WaitForSecondsRealtime(1f);
            TorneoUI.Abrir();
            yield return new WaitForSecondsRealtime(1.2f);
            string primera = Path.Combine(capturas, "duelo_torneo.png");
            ScreenCapture.CaptureScreenshot(primera);
            Debug.Log("CAPTURA_DUELO=" + primera);
            yield return new WaitForSecondsRealtime(0.6f);

            try
            {
                var metodo = typeof(TorneoUI).GetMethod("Turno",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (metodo != null && TorneoUI.Instancia != null)
                    metodo.Invoke(TorneoUI.Instancia, new object[] { AccionDuelo.Atacar, -1 });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BenchDuelo] no se pudo jugar el turno: " + e.Message);
            }
            yield return new WaitForSecondsRealtime(0.8f);

            string segunda = Path.Combine(capturas, "duelo_torneo_2.png");
            ScreenCapture.CaptureScreenshot(segunda);
            Debug.Log("CAPTURA_DUELO=" + segunda);
            yield return new WaitForSecondsRealtime(0.5f);

            // Cierra el duelo entero para fotografiar el modal de resultado.
            int guardia = 0;
            while (TorneoUI.Instancia != null && !TorneoUI.Instancia.Terminado && guardia++ < 40)
            {
                try
                {
                    var metodo = typeof(TorneoUI).GetMethod("Turno",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (metodo != null) metodo.Invoke(TorneoUI.Instancia, new object[] { AccionDuelo.Atacar, -1 });
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[BenchDuelo] turno cortado: " + e.Message);
                    break;
                }
                yield return new WaitForSecondsRealtime(0.06f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            string resultado = Path.Combine(capturas, "duelo_resultado.png");
            ScreenCapture.CaptureScreenshot(resultado);
            Debug.Log("CAPTURA_DUELO=" + resultado + " terminado=" + (TorneoUI.Instancia != null && TorneoUI.Instancia.Terminado));
            yield return new WaitForSecondsRealtime(0.8f);
            Application.Quit();
        }

        /// Mide el escenario con los modelos del roster de 5 materiales: son los que
        /// aparecen en la arena, asi que -benchArte (que solo usa los habitats salvajes
        /// v6) no los llegaba a medir.
        private static IEnumerator CapturarRoster(string capturas)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (GameFlow.Activo != null) GameFlow.Activo.IniciarCampana(1);
            yield return new WaitForSecondsRealtime(1f);
            SlugArenaGame arena = Object.FindAnyObjectByType<SlugArenaGame>();
            if (arena != null) arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            DesactivarRocas(arena);
            yield return new WaitForSecondsRealtime(2f);
            DesactivarRocas(arena);

            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameHUD hud = Object.FindAnyObjectByType<GameHUD>();
            if (hud != null) hud.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;

            EnemySlugAI[] ias = Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++) ias[i].enabled = false;

            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            MethodInfo attach = typeof(SlugArenaGame).GetMethod(
                "AttachModel", BindingFlags.NonPublic | BindingFlags.Static);
            string[] ids =
            {
                "bengala", "leviatan", "bloque", "tornado", "zeus", "ventisca",
                "selva", "enigma", "vacio", "chispa", "huracan", "elemental-agua"
            };
            if (pc != null && attach != null)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    SlugData d = SlugRoster.Buscar(ids[i]);
                    if (d == null) continue;
                    GameObject go = new GameObject("roster_" + ids[i]);
                    int fila = i / 6;
                    int col = i % 6;
                    go.transform.position = pc.transform.position
                        + pc.transform.forward * 3.2f
                        + pc.transform.right * ((col - 2.5f) * 0.45f)
                        + pc.transform.up * (0.02f - fila * 0.02f);
                    attach.Invoke(null, new object[] { go, d, Vector3.zero, false });
                }
            }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Medir("Roster", 6f, true);
            string foto = Path.Combine(capturas, "roster_bench.png");
            ScreenCapture.CaptureScreenshot(foto);
            Debug.Log("CAPTURA_ROSTER=" + foto);
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        private static IEnumerator Medir(string fase, float segundos, bool principal)
        {
            List<float> dt = new List<float>(4096);
            float t0 = Time.realtimeSinceStartup;
            int f0 = Time.frameCount;
            while (Time.realtimeSinceStartup - t0 < segundos)
            {
                dt.Add(Time.unscaledDeltaTime);
                yield return null;
            }
            float elapsed = Mathf.Max(0.0001f, Time.realtimeSinceStartup - t0);
            int frames = Time.frameCount - f0;
            dt.Sort();
            float p95 = dt.Count > 0 ? dt[Mathf.Clamp(Mathf.FloorToInt(dt.Count * 0.95f), 0, dt.Count - 1)] : 0f;
            float p99 = dt.Count > 0 ? dt[Mathf.Clamp(Mathf.FloorToInt(dt.Count * 0.99f), 0, dt.Count - 1)] : 0f;
            float avgFps = frames / elapsed;
            float low1 = p99 > 0f ? 1f / p99 : 0f;
            string linea = "FPS_" + fase + " avg=" + avgFps.ToString("F1") + " 1%low=" + low1.ToString("F1")
                + " p95ms=" + (p95 * 1000f).ToString("F1") + " frames=" + frames
                + " seconds=" + elapsed.ToString("F1");
            if (principal)
                Debug.Log("FPS_BUILD avg=" + avgFps.ToString("F1") + " 1%low=" + low1.ToString("F1")
                    + " p95ms=" + (p95 * 1000f).ToString("F1") + " frames=" + frames
                    + " seconds=" + elapsed.ToString("F1"));
            Debug.Log(linea);
        }
    }
}
