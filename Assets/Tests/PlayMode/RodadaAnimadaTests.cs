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
    /// La rodada de Shift desplazaba al jugador sin ninguna lectura visual. Esta prueba
    /// entra por IntentarRodada(), el mismo metodo que el borde de Shift, y comprueba el
    /// giro completo, el retorno exacto al bind y que morir a mitad no deje la raiz girada.
    public class RodadaAnimadaTests
    {
        private class Montaje
        {
            public PlayerController pc;
            public DuelistaAnimator anim;
            public PlayerDodge dodge;
            public SlugActor actor;
            public Transform raiz;
            public Transform cadera;
            public Transform modelo;
        }

        [UnityTest]
        public IEnumerator LaRodadaGiraYVuelveAlBind()
        {
            Montaje m = new Montaje();
            yield return Preparar(m);

            // La calibracion de suela asienta la altura 0,25 s despues del montaje.
            // Con el delta minimo del batch hay que darle reloj fijo antes de capturar
            // el bind, o el roll termina comparando contra la altura previa.
            Time.captureDeltaTime = 1f / 60f;
            for (int i = 0; i < 25; i++) yield return null;
            Time.captureDeltaTime = 0f;

            Quaternion raizRot0 = m.raiz.localRotation;
            Vector3 raizPos0 = m.raiz.localPosition;
            Vector3 caderaPos0 = m.cadera.localPosition;
            Quaternion modeloRot0 = m.modelo.localRotation;
            Vector3 modeloPos0 = m.modelo.localPosition;
            Vector3 inicio = m.pc.transform.position;

            Assert.IsTrue(m.dodge.IntentarRodada(), "la rodada no arranco");
            OcultarPropsCerca(m.pc, 6f);
            float maxAngulo = 0f;
            bool fotoMedia = false;
            Vector3 fotoPos = Vector3.zero;

            // Paso fijo: en batch el primer deltaTime puede comerse la rodada entera en
            // un cuadro y el animador nunca veria el giro. El juego usa el mismo Paso(dt).
            for (int i = 0; i < 70; i++)
            {
                m.dodge.Paso(1f / 60f);
                yield return null;
                maxAngulo = Mathf.Max(maxAngulo, m.anim.AnguloRodada);
                if (i == 0) Debug.Log("RODADA_DIAG " + m.anim.DiagnosticoRodada());
                if (!fotoMedia && m.anim.AnguloRodada >= 160f)
                {
                    fotoMedia = true;
                    fotoPos = m.pc.transform.position;
                    Debug.Log("RODADA_FOTO ang=" + m.anim.AnguloRodada.ToString("F1")
                        + " actor=" + m.pc.transform.position.ToString("F2")
                        + " modeloWorld=" + m.modelo.position.ToString("F2")
                        + " modeloLocal=" + m.modelo.localPosition.ToString("F2")
                        + " modeloRot=" + m.modelo.localRotation.eulerAngles.ToString("F1")
                        + " dist=" + Vector3.Distance(m.modelo.position, m.pc.transform.position).ToString("F2"));
                    LogHero(m.modelo, "RODADA_FOTO_SMR");
                    Capturar(m.pc, m.modelo, "u2", "rodada_media.png", 5.0f, 1.5f, 0.2f);
                }
                if (!m.dodge.Rodando && !m.dodge.Expuesto && i > 25) break;
            }

            for (int i = 0; i < 10; i++) yield return null;

            Assert.GreaterOrEqual(maxAngulo, 300f, "la raiz no completo el giro de la rodada");
            float anguloFinal = Quaternion.Angle(m.raiz.localRotation, raizRot0);
            Assert.LessOrEqual(anguloFinal, 1f, "la raiz no volvio al bind: " + anguloFinal.ToString("F2"));
            Assert.Less(Vector3.Distance(m.raiz.localPosition, raizPos0), 0.0001f,
                "la raiz no volvio a su posicion bind");
            Assert.Less(Vector3.Distance(m.cadera.localPosition, caderaPos0), 0.05f,
                "la cadera no volvio a su posicion de reposo (el idle la mueve unos mm)");
            float modeloAngulo = Quaternion.Angle(m.modelo.localRotation, modeloRot0);
            Assert.LessOrEqual(modeloAngulo, 1f,
                "el modelo visual no volvio a su rotacion bind: " + modeloAngulo.ToString("F2"));
            Assert.Less(Vector3.Distance(m.modelo.localPosition, modeloPos0), 0.0001f,
                "el modelo visual no volvio a su posicion bind");

            Vector3 a = inicio; a.y = 0f;
            Vector3 b = m.pc.transform.position; b.y = 0f;
            float recorrido = Vector3.Distance(a, b);
            Assert.AreEqual(m.dodge.PasoLateral, recorrido, 0.10f,
                "la animacion altero el desplazamiento de la rodada");

            OcultarPropsCerca(m.pc, 6f);
            LogHero(m.modelo, "RODADA_FIN_SMR");
            Capturar(m.pc, m.modelo, "u2", "rodada_fin.png", 5.0f, 1.0f, 1.0f);
            Debug.Log("RODADA_ANIM maxAngulo=" + maxAngulo.ToString("F1")
                + " anguloFinal=" + anguloFinal.ToString("F2")
                + " modeloAngulo=" + modeloAngulo.ToString("F2")
                + " recorrido=" + recorrido.ToString("F2")
                + " paso=" + m.dodge.PasoLateral.ToString("F2")
                + " fotoMedia=" + fotoMedia + " fotoPos=" + fotoPos.ToString("F2"));
        }

        [UnityTest]
        public IEnumerator MorirDuranteLaRodadaDevuelveLaRaiz()
        {
            Montaje m = new Montaje();
            yield return Preparar(m);

            // La calibracion de suela asienta la altura 0,25 s despues del montaje.
            // Con el delta minimo del batch hay que darle reloj fijo antes de capturar
            // el bind, o el roll termina comparando contra la altura previa.
            Time.captureDeltaTime = 1f / 60f;
            for (int i = 0; i < 25; i++) yield return null;
            Time.captureDeltaTime = 0f;

            Quaternion raizRot0 = m.raiz.localRotation;
            Vector3 raizPos0 = m.raiz.localPosition;
            Quaternion modeloRot0 = m.modelo.localRotation;
            Vector3 modeloPos0 = m.modelo.localPosition;

            Assert.IsTrue(m.dodge.IntentarRodada(), "la rodada no arranco");
            for (int i = 0; i < 5; i++)
            {
                m.dodge.Paso(1f / 60f);
                yield return null;
            }
            Assert.IsTrue(m.dodge.Rodando, "la rodada ya habia terminado antes del golpe mortal");
            float dano = m.actor.TakeDamage(99999f, ElementType.Fire);
            Assert.Greater(dano, 0f, "el golpe mortal no entro (invulnerable o escudo)");
            Assert.IsFalse(m.actor.Alive, "el actor deberia estar muerto");

            for (int i = 0; i < 8; i++) yield return null;
            float angulo = Quaternion.Angle(m.raiz.localRotation, raizRot0);
            Assert.LessOrEqual(angulo, 1f,
                "morir en la rodada dejo la raiz girada: " + angulo.ToString("F2"));
            Assert.Less(Vector3.Distance(m.raiz.localPosition, raizPos0), 0.0001f,
                "morir en la rodada dejo el pivote aplicado");
            float modeloAngulo = Quaternion.Angle(m.modelo.localRotation, modeloRot0);
            Assert.LessOrEqual(modeloAngulo, 1f,
                "morir en la rodada dejo el modelo visual girado: " + modeloAngulo.ToString("F2"));
            Assert.Less(Vector3.Distance(m.modelo.localPosition, modeloPos0), 0.0001f,
                "morir en la rodada dejo el modelo visual desplazado");
            Debug.Log("RODADA_MUERTE anguloRaiz=" + angulo.ToString("F2")
                + " modeloAngulo=" + modeloAngulo.ToString("F2")
                + " dano=" + dano.ToString("F1"));
        }

        private static IEnumerator Preparar(Montaje m)
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = UnityEngine.Object.FindAnyObjectByType<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 20; i++) yield return null;

            m.pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(m.pc, "no hay PlayerController");
            m.anim = m.pc.GetComponent<DuelistaAnimator>();
            m.dodge = m.pc.GetComponent<PlayerDodge>();
            m.actor = m.pc.GetComponent<SlugActor>();
            Assert.IsNotNull(m.anim, "el jugador no tiene DuelistaAnimator");
            Assert.IsNotNull(m.dodge, "el jugador no tiene PlayerDodge");
            Assert.IsNotNull(m.actor, "el jugador no tiene SlugActor");

            Transform hero = m.pc.transform.Find("Cuerpo_Shane");
            if (hero == null) hero = GameObject.Find("Cuerpo_Shane")?.transform;
            Assert.IsNotNull(hero, "no se monto Cuerpo_Shane");
            m.raiz = null; m.cadera = null;
            foreach (Transform t in hero.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "raiz") m.raiz = t;
                else if (t.name == "cadera") m.cadera = t;
            }
            Assert.IsNotNull(m.raiz, "el rig no tiene raiz");
            Assert.IsNotNull(m.cadera, "el rig no tiene cadera");
            m.modelo = hero;

            // Referencia estable: rodar hacia +Z y no dejar que otro script rote o
            // desplace al actor durante la medicion. El panel de historia congela el
            // tiempo; sin cerrarlo, Time.deltaTime=0 y el animador no aplica nada.
            StoryPanel historia = UnityEngine.Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            // Foto limpia: la cueva es un mesh combinado y los hongos tapan la rodada.
            // Se apaga solo para la captura; el animador y el desplazamiento no la usan.
            GameObject cueva = GameObject.Find("CuevaPixar");
            if (cueva != null) cueva.SetActive(false);
            GameObject keyGo = new GameObject("luz_key_rodada");
            Light key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.86f, 0.72f);
            key.intensity = 1.2f;
            key.shadows = LightShadows.None;
            key.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            GameObject fillGo = new GameObject("luz_fill_rodada");
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.75f, 0.82f, 1f);
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(15f, 150f, 0f);
            m.pc.transform.rotation = Quaternion.identity;
            m.pc.GetComponent<PlayerController>().enabled = false;
            SlugAnimator raizAnim = m.pc.GetComponent<SlugAnimator>();
            if (raizAnim != null) raizAnim.enabled = false;
            // El Update propio del dodge pisaria el Paso fijo de la prueba.
            m.dodge.enabled = false;
            yield return null;
        }

        private static void Capturar(PlayerController pc, Transform modelo, string carpeta, string nombre,
            float distancia, float altura, float foco)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "anim-heroe", carpeta));
            Directory.CreateDirectory(dir);
            GameObject go = new GameObject("cam_rodada");
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.025f, 0.05f, 1f);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.enabled = false;
            RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Vector3 centro = modelo.position + Vector3.up * foco;
            cam.transform.position = centro + pc.transform.right * distancia + Vector3.up * altura;
            cam.transform.LookAt(centro);
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, nombre), tex.EncodeToPNG());
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(go);
        }

        /// Solo para la foto: saca hongos y props chicos de alrededor. No toca al
        /// heroe, al terreno ni al estado de juego; la captura se lee sin oclusiones.
        private static void OcultarPropsCerca(PlayerController pc, float radio)
        {
            Transform hero = pc.transform.Find("Cuerpo_Shane");
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (hero != null && r.transform.IsChildOf(hero)) continue;
                if (r.transform.IsChildOf(pc.transform)) continue;
                if (r.bounds.size.sqrMagnitude > 100f) continue;
                if (Vector3.Distance(r.bounds.center, pc.transform.position) < radio)
                    r.enabled = false;
            }
        }

        private static void LogHero(Transform modelo, string etiqueta)
        {
            SkinnedMeshRenderer smr = modelo.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null)
            {
                Debug.Log(etiqueta + " sin-SkinnedMeshRenderer");
                return;
            }
            Debug.Log(etiqueta + " enabled=" + smr.enabled
                + " bounds=" + smr.bounds.center.ToString("F2")
                + " size=" + smr.bounds.size.ToString("F2")
                + " root=" + modelo.position.ToString("F2"));
        }
    }
}
