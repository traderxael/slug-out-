using System.Collections;
using System.IO;
using NUnit.Framework;
using SlugOut.Core;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// El jugador y la rodada deben chocar con el muro del borde (las cuatro Pared_* de la
    /// escena), con los pilares y con las rocas grandes. Se llama a PlayerController.Mover y
    /// Colocar desde la prueba para no depender del teclado, y se comprueba que las bocas de
    /// tunel siguen cruzando al jugador de cueva.
    public class MurosTests
    {
        PlayerController pc;

        // El paquete com.unity.ai.assistant loguea un error de relay al azar y tumba
        // la prueba que le toque; estas pruebas miden geometria, no logs.
        [UnitySetUp]
        public IEnumerator SilenciarRuidoExterno()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestaurarLogs()
        {
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ElJugadorNoCruzaElMuroDelBorde()
        {
            yield return Preparar();
            float[] angulos = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
            int salio = 0;
            for (int i = 0; i < angulos.Length; i++)
            {
                float rad = angulos[i] * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                pc.Colocar(dir * 10f);
                for (int k = 0; k < 400; k++) pc.Mover(dir * 0.1f);
                Vector3 fin = pc.transform.position;
                float r = new Vector2(fin.x, fin.z).magnitude;
                // No toda direccion llega al borde: en diagonal hay pilares y rocas grandes que
                // tambien son solidas. Lo que se prueba es que NINGUNA direccion saca al jugador
                // de la cueva, y que el borde sigue siendo alcanzable por los cuatro ejes.
                Escribir("borde ang=" + angulos[i] + " fin=" + fin.ToString("F2")
                    + " r=" + r.ToString("F3") + " alto=" + Terrain3D.Altura(fin.x, fin.z).ToString("F2")
                    + " contactos=" + pc.ContactosMuro + " freno=" + FrenoDe(pc, dir));
                Assert.LessOrEqual(Mathf.Abs(fin.x), MurosCueva.CaraBorde + 0.05f,
                    "el jugador cruzo el muro por el angulo " + angulos[i] + " (x)");
                Assert.LessOrEqual(Mathf.Abs(fin.z), MurosCueva.CaraBorde + 0.05f,
                    "el jugador cruzo el muro por el angulo " + angulos[i] + " (z)");
                Assert.Greater(pc.ContactosMuro, 0, "no hubo contacto en " + angulos[i]);
                if (r > 20f) salio++;
            }
            Escribir("borde direcciones que llegaron al borde=" + salio + "/8");
            Assert.GreaterOrEqual(salio, 4, "el borde no es alcanzable por ningun lado");
            CapturarJugador(pc, "jugador-en-el-anillo");
        }

        [UnityTest]
        public IEnumerator ElJugadorNoCruzaUnPilar()
        {
            yield return Preparar();
            GameObject pilar = GameObject.Find("muro_pilar_0_0");
            Assert.IsNotNull(pilar, "no se encontro el proxy del pilar");
            pc.Colocar(pilar.transform.position + new Vector3(-2.5f, 0f, 0f));
            Vector3 inicio = pc.transform.position;
            Vector3 dir = pilar.transform.position - inicio;
            dir.y = 0f;
            dir.Normalize();
            for (int i = 0; i < 80; i++) pc.Mover(dir * 0.05f);
            float distancia = Vector3.Distance(pc.transform.position, pilar.transform.position);
            Escribir("pilar distancia=" + distancia.ToString("F3")
                + " contactos=" + pc.ContactosMuro);
            Assert.GreaterOrEqual(distancia, 1.1f, "el jugador entro en el pilar");
            Assert.Greater(pc.ContactosMuro, 0, "el pilar no genero contacto");
            CapturarJugador(pc, "jugador-contra-pilar");
        }

        [UnityTest]
        public IEnumerator DeslizaSinClavarse()
        {
            yield return Preparar();
            pc.Colocar(new Vector3(20f, 0f, 0f));
            Vector3 radial = Vector3.right;
            Vector3 tangente = Vector3.Cross(Vector3.up, radial).normalized;
            Vector3 dir = (radial * 0.7f + tangente * 0.7f).normalized;
            Vector3 inicio = pc.transform.position;
            for (int i = 0; i < 240; i++) pc.Mover(dir * 0.05f);
            Vector3 fin = pc.transform.position;
            float tangencial = Mathf.Abs(Vector3.Dot(fin - inicio, tangente));
            Escribir("desliz fin=" + fin.ToString("F2") + " tangencial=" + tangencial.ToString("F3"));
            Assert.LessOrEqual(Mathf.Abs(fin.x), MurosCueva.CaraBorde + 0.05f,
                "el jugador cruzo el muro deslizando");
            Assert.Greater(tangencial, 1f, "el jugador se clavo contra el muro");
        }

        [UnityTest]
        public IEnumerator LaRodadaNoCruzaElMuro()
        {
            yield return Preparar();
            pc.Colocar(new Vector3(22.6f, 0f, 0f));
            pc.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            PlayerDodge dodge = pc.GetComponent<PlayerDodge>();
            Assert.IsNotNull(dodge, "el jugador no tiene PlayerDodge");
            Vector3 inicio = pc.transform.position;
            Assert.IsTrue(dodge.IntentarRodada(), "la rodada no arranco");
            for (int i = 0; i < 60; i++)
            {
                dodge.Paso(1f / 60f);
                pc.ApoyarEnPiso();
            }
            float avance = Vector3.Distance(inicio, pc.transform.position);
            Escribir("rodada fin=" + pc.transform.position.ToString("F2")
                + " avance=" + avance.ToString("F3"));
            Assert.LessOrEqual(pc.transform.position.x, MurosCueva.CaraBorde + 0.05f,
                "la rodada cruzo el muro del borde");
            Assert.Less(avance, dodge.PasoLateral, "la rodada no la freno ningun muro");
            CapturarJugador(pc, "rodada-cortada");
        }

        [UnityTest]
        public IEnumerator LaBocaDelTunelSigueAbierta()
        {
            yield return Preparar();
            int antes = RegionCuevaSeca.Actual;
            TunelPasadizo[] tuneles = Object.FindObjectsByType<TunelPasadizo>();
            Assert.Greater(tuneles.Length, 0, "no hay tuneles en la region");
            TunelPasadizo tunel = tuneles[0];
            // El enfriamiento es estatico y puede venir de otra prueba/escena.
            yield return new WaitForSecondsRealtime(1.8f);
            bool viajo = tunel.Viajar(pc.transform);
            yield return null;
            Escribir("tunel antes=" + antes + " despues=" + RegionCuevaSeca.Actual
                + " viajo=" + viajo + " pos=" + pc.transform.position.ToString("F2"));
            Assert.IsTrue(viajo, "el viaje del tunel fallo: " + TunelPasadizo.UltimoMotivo);
            Assert.AreNotEqual(antes, RegionCuevaSeca.Actual, "la region no cambio");
            Assert.IsFalse(ColisionJugador.SobreMuro(pc.transform.position),
                "el jugador aparecio dentro de un muro tras el viaje");
        }

        [UnityTest]
        public IEnumerator LosAccesosSiguenLibres()
        {
            yield return Preparar();
            float[] habitats = { 12f, 140f, 252f };
            for (int h = 0; h < habitats.Length; h++)
            {
                float rad = habitats[h] * Mathf.Deg2Rad;
                for (float r = 8f; r <= 21f; r += 0.5f)
                {
                    float x = Mathf.Cos(rad) * r;
                    float z = Mathf.Sin(rad) * r;
                    Vector3 p = new Vector3(x, Terrain3D.Altura(x, z) + 1f, z);
                    Assert.IsFalse(ColisionJugador.SobreMuro(p),
                        "muro en el acceso al habitat " + habitats[h] + " en r=" + r.ToString("F1")
                        + " (" + MuroCercano(p) + ")");
                }
            }
        }

        [UnityTest]
        public IEnumerator NingunMuroDentroDeLaPlaza()
        {
            yield return Preparar();
            BoxCollider[] cajas = Object.FindObjectsByType<BoxCollider>();
            int muros = 0;
            for (int i = 0; i < cajas.Length; i++)
            {
                int capa = cajas[i].gameObject.layer;
                if (capa != ColisionJugador.CapaMuros) continue;
                muros++;
                float r = new Vector2(cajas[i].transform.position.x,
                    cajas[i].transform.position.z).magnitude;
                Assert.Greater(r, 4.5f, "hay un muro en la plaza: " + cajas[i].name + " r=" + r.ToString("F2"));
                Assert.IsFalse(cajas[i].isTrigger, "un muro es trigger: " + cajas[i].name);
            }
            Escribir("muros total=" + muros);
            Assert.Greater(muros, 10, "no se construyeron los muros esperados");
        }

        /// El hueco de la boca no puede ser una puerta al vacio: en cada lateral del pasaje,
        /// el jugador que empuja hacia afuera o viaja por el tunel o queda frenado por el muro.
        [UnityTest]
        public IEnumerator PorLaBocaNoSeSaleAlVacio()
        {
            float[] laterales = { 0f, 1.4f, -1.4f };
            for (int l = 0; l < laterales.Length; l++)
            {
                yield return Preparar();
                TunelPasadizo[] bocas = Object.FindObjectsByType<TunelPasadizo>();
                Assert.Greater(bocas.Length, 0, "la region no tiene bocas de tunel");
                Vector3 boca = bocas[0].transform.position; boca.y = 0f;
                Vector3 radial = boca.normalized;
                Vector3 lateral = Vector3.Cross(Vector3.up, radial);
                int antes = RegionCuevaSeca.Actual;
                Vector3 pie = radial * (boca.magnitude - 3f) + lateral * laterales[l];
                pc.Colocar(pie);
                yield return new WaitForSecondsRealtime(1.8f);
                for (int i = 0; i < 120 && RegionCuevaSeca.Actual == antes; i++)
                {
                    pc.Mover(radial * 0.06f);
                    yield return null;
                }
                float r = new Vector2(pc.transform.position.x, pc.transform.position.z).magnitude;
                bool viajo = RegionCuevaSeca.Actual != antes;
                float alCentro = Vector2.Distance(new Vector2(pc.transform.position.x,
                    pc.transform.position.z), new Vector2(boca.x, boca.z));
                Escribir("boca lateral=" + laterales[l].ToString("F1") + " r=" + r.ToString("F3")
                    + " viajo=" + viajo + " radio=" + bocas[0].radio.ToString("F2")
                    + " region=" + RegionCuevaSeca.Actual + " antes=" + antes
                    + " rBoca=" + boca.magnitude.ToString("F2")
                    + " alCentro=" + alCentro.ToString("F2")
                    + " motivo=" + TunelPasadizo.UltimoMotivo
                    + " freno=" + FrenoDe(pc, radial));
                Assert.IsTrue(viajo
                    || (Mathf.Abs(pc.transform.position.x) <= MurosCueva.CaraBorde + 0.05f
                        && Mathf.Abs(pc.transform.position.z) <= MurosCueva.CaraBorde + 0.05f),
                    "el jugador salio de la cueva por la boca en lateral " + laterales[l]);
                Assert.IsTrue(viajo,
                    "la boca no cruza al jugador: se frena " + alCentro.ToString("F2")
                    + " m antes del disparador (" + TunelPasadizo.UltimoMotivo + ")");
            }
        }

        /// Que collider tiene justo delante: sin esto un "no paso" se lee como "el hueco no se
        /// corto" cuando puede ser una pared de la escena, el pilar del tunel o el propio anillo.
        static string FrenoDe(PlayerController jugador, Vector3 rumbo)
        {
            Vector3 p = jugador.transform.position;
            float semi = Mathf.Max(0f, ColisionJugador.AltoDefecto * 0.5f
                - ColisionJugador.RadioDefecto);
            RaycastHit hit;
            if (Physics.CapsuleCast(p - Vector3.up * semi, p + Vector3.up * semi,
                    ColisionJugador.RadioDefecto, rumbo, out hit, 4f, ColisionJugador.Mascara,
                    QueryTriggerInteraction.Ignore))
                return hit.collider.name + " a " + hit.distance.ToString("F2")
                    + "m (r=" + new Vector2(hit.point.x, hit.point.z).magnitude.ToString("F2") + ")";
            return "nada en 4 m";
        }

        /// Como encuadra la camara del juego cuando el jugador esta pegado al muro. Se mide y
        /// se captura: el encuadre es lo que ve el usuario, no el barrido.
        [UnityTest]
        public IEnumerator LaCamaraAlLlegarAlMuro()
        {
            yield return Preparar();
            pc.Colocar(new Vector3(18f, 0f, 0f));
            for (int i = 0; i < 160; i++)
            {
                pc.Mover(Vector3.right * 0.1f);
                yield return null;
            }
            Camera cam = Object.FindAnyObjectByType<Camera>();
            Assert.IsNotNull(cam, "no hay camara en escena");
            float rJugador = new Vector2(pc.transform.position.x, pc.transform.position.z).magnitude;
            float rCam = new Vector2(cam.transform.position.x, cam.transform.position.z).magnitude;
            float dist = Vector3.Distance(cam.transform.position, pc.transform.position);
            Escribir("camara rJugador=" + rJugador.ToString("F2") + " rCamara=" + rCam.ToString("F2")
                + " dist=" + dist.ToString("F2") + " alto=" + cam.transform.position.y.ToString("F2")
                + " terceraPersona=" + (cam.GetComponent<CameraFollow>() != null));
            yield return null;
            CapturarCamaraJuego("camara-en-el-anillo");
            Assert.LessOrEqual(cam.transform.position.x, MurosCueva.CaraBorde,
                "la camara se fue fuera del muro: x=" + cam.transform.position.x.ToString("F2"));
            Assert.LessOrEqual(Mathf.Abs(cam.transform.position.z), MurosCueva.CaraBorde,
                "la camara se fue fuera del muro: z=" + cam.transform.position.z.ToString("F2"));
            Assert.GreaterOrEqual(dist, 4f, "la camara se aplasto contra el jugador");
        }

        IEnumerator Preparar()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            // La prueba del tunel deja Actual en otra cueva: sin volver a la 0, este setup
            // construiria la region equivocada y mediria sus props con los angulos de la Cueva Seca.
            RegionCuevaSeca region = Object.FindAnyObjectByType<RegionCuevaSeca>();
            if (region != null) region.Reconstruir(0, out _);
            Physics.SyncTransforms();
            yield return null;
            // La arena del GameFlow persistente, no la copia de escena que Awake pudo destruir.
            SlugArenaGame arena = GameFlow.Activo.GetComponent<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 30; i++) yield return null;

            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            EnemySlugAI[] ias = Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++) if (ias[i] != null) ias[i].enabled = false;

            pc = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(pc, "no hay PlayerController");
            pc.enabled = false;
            pc.Colocar(Vector3.zero);
            yield return null;
            LogGeometria();
        }

        static void LogGeometria()
        {
            string[] nombres = { "Pared_Norte", "Pared_Sur", "Pared_Este", "Pared_Oeste",
                "Pilares_cueva", "Rocas_suelo" };
            for (int i = 0; i < nombres.Length; i++)
            {
                GameObject go = GameObject.Find(nombres[i]);
                if (go == null) { Escribir("geo " + nombres[i] + " AUSENTE"); continue; }
                Renderer r = go.GetComponent<Renderer>();
                if (r == null) r = go.GetComponentInChildren<Renderer>();
                Escribir("geo " + nombres[i] + " capa=" + go.layer
                    + (r != null ? " bounds=" + r.bounds.size.ToString("F2")
                        + " y=" + r.bounds.min.ToString("F2") + ".." + r.bounds.max.y.ToString("F2")
                        : " sin renderer"));
            }
            BoxCollider[] cajas = Object.FindObjectsByType<BoxCollider>();
            int capa8 = 0;
            for (int i = 0; i < cajas.Length; i++)
                if (cajas[i].gameObject.layer == ColisionJugador.CapaMuros) capa8++;
            Escribir("colliders capaMuros=" + capa8);
        }

        static void CapturarJugador(PlayerController jugador, string nombre)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "muros"));
            Directory.CreateDirectory(dir);
            var go = new GameObject("cam_muros");
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.025f, 0.05f, 1f);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.enabled = false;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.transform.position = jugador.transform.position + new Vector3(-3.4f, 1.5f, 0f);
            cam.transform.LookAt(jugador.transform.position + Vector3.up * 0.9f);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, nombre + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(go);
        }

        static void CapturarCamaraJuego(string nombre)
        {
            Camera cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null) return;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "muros"));
            Directory.CreateDirectory(dir);
            bool previo = cam.enabled;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.enabled = true;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, nombre + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            cam.enabled = previo;
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(rt);
        }

        static string MuroCercano(Vector3 p)
        {
            Collider[] cs = Physics.OverlapCapsule(p - Vector3.up * 0.5f, p + Vector3.up * 0.5f,
                0.5f, ColisionJugador.Mascara, QueryTriggerInteraction.Ignore);
            if (cs.Length == 0) return "sin collider";
            string s = "";
            for (int i = 0; i < cs.Length; i++)
            {
                Vector3 c = cs[i].bounds.center;
                Vector3 e = cs[i].bounds.extents;
                s += (i > 0 ? ", " : "") + cs[i].name + " pos="
                    + cs[i].transform.position.ToString("F2")
                    + " bounds=" + c.ToString("F2") + " ext=" + e.ToString("F2")
                    + " dist=" + Vector3.Distance(p, c).ToString("F2");
            }
            return s;
        }

        static void Escribir(string linea)
        {
            string ruta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "muros.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.AppendAllText(ruta, linea + "\n");
            Debug.Log("MUROS " + linea);
        }
    }
}
