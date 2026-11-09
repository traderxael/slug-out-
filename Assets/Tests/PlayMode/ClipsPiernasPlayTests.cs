using System.Collections;
using NUnit.Framework;
using SlugOut.Core;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// El héroe carga el JSON de clips y mueve las piernas con él cuando camina.
    public class ClipsPiernasPlayTests
    {
        /// Esta prueba apaga la arena para congelar la muerte animada. Si la arena es la
        /// persistente, dejarla apagada impide spawnear en todas las pruebas siguientes;
        /// el TearDown la devuelve aunque el test falle a mitad.
        [TearDown]
        public void ReactivarArena()
        {
            if (GameFlow.Activo == null) return;
            SlugArenaGame arena = GameFlow.Activo.GetComponent<SlugArenaGame>();
            if (arena != null) arena.enabled = true;
        }

        [UnityTest]
        public IEnumerator ElHeroeUsaLosClipsHorneados()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = UnityEngine.Object.FindAnyObjectByType<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 25; i++) yield return null;

            StoryPanel historia = UnityEngine.Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            EnemySlugAI[] ias = UnityEngine.Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++) if (ias[i] != null) ias[i].enabled = false;

            PlayerController pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            DuelistaAnimator anim = pc != null ? pc.GetComponent<DuelistaAnimator>() : null;
            Transform hero = pc != null ? pc.transform.Find("Cuerpo_Shane") : null;
            if (hero == null) hero = GameObject.Find("Cuerpo_Shane")?.transform;
            Assert.IsNotNull(pc, "no hay PlayerController");
            Assert.IsNotNull(anim, "no hay DuelistaAnimator");
            Assert.IsNotNull(hero, "no se monto Cuerpo_Shane");
            Assert.IsTrue(anim.UsandoClips, "el heroe no encontro los clips horneados");

            Transform muslo = null;
            foreach (Transform t in hero.GetComponentsInChildren<Transform>(true))
                if (t.name == "muslo_izq") { muslo = t; break; }
            Assert.IsNotNull(muslo, "falta muslo_izq");
            Quaternion bind = muslo.localRotation;
            float maxDesvio = 0f;
            for (int i = 0; i < 90; i++)
            {
                pc.transform.position += Vector3.forward * (0.8f * Time.deltaTime);
                pc.ApoyarEnPiso();
                yield return null;
                maxDesvio = Mathf.Max(maxDesvio, Quaternion.Angle(bind, muslo.localRotation));
            }
            Assert.Greater(maxDesvio, 1f, "el muslo no se movio: el clip no llego al hueso");

            // Impacto: el clip mueve tambien los brazos (el procedural de impacto solo
            // toca cabeza y columna). Se dispara la ruta y se exige reaccion en el brazo.
            Transform brazo = null;
            Transform columna = null;
            Transform cadera = null;
            Transform pieIzq = null;
            Transform pieDer = null;
            foreach (Transform t in hero.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "brazo_izq") brazo = t;
                if (t.name == "columna") columna = t;
                if (t.name == "cadera") cadera = t;
                if (t.name == "pie_izq") pieIzq = t;
                if (t.name == "pie_der") pieDer = t;
            }
            Assert.IsNotNull(brazo, "falta brazo_izq");
            Assert.IsNotNull(columna, "falta columna");
            Quaternion brazoBind = brazo.localRotation;
            float maxBrazo = 0f;
            anim.AnimarImpacto();
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                maxBrazo = Mathf.Max(maxBrazo, Quaternion.Angle(brazoBind, brazo.localRotation));
            }
            Assert.Greater(maxBrazo, 0.5f, "el impacto no llegó al brazo: el clip no se aplicó");

            // Muerte: se dispara el reloj del animador sin matar al actor. Matarlo abre el
            // panel de resultado, que pone timeScale=0 y congela la propia animacion (se
            // quedaba a medio colapso) ademas de contaminar los tests siguientes.
            // Se corre dos veces, sin y con clip: la pose final tiene que cambiar. Las dos
            // parten del bind (AplicarMuerte llama ResetearTodo), asi que comparar finales
            // aisla el efecto del clip sin depender del bind real del FBX.
            arena.enabled = false;
            const System.Reflection.BindingFlags privado =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            System.Reflection.FieldInfo reloj = typeof(DuelistaAnimator).GetField("_tMuerte", privado);
            System.Reflection.FieldInfo campoClip = typeof(DuelistaAnimator).GetField("_clipMuerte", privado);
            Assert.IsNotNull(reloj, "cambio el campo _tMuerte de DuelistaAnimator");
            Assert.IsNotNull(campoClip, "cambio el campo _clipMuerte de DuelistaAnimator");
            object clipMuerte = campoClip.GetValue(anim);
            Assert.IsNotNull(clipMuerte, "el heroe no cargo el clip de muerte");

            System.Collections.IEnumerator EsperarMuerte()
            {
                // Por tiempo, no por cuadros: en batch el juego corre a cientos de fps y
                // 70 frames no alcanzaban los 0,75 s de la muerte.
                float inicio = Time.time;
                float realInicio = Time.realtimeSinceStartup;
                while (Time.time - inicio < 1.2f && Time.realtimeSinceStartup - realInicio < 3f)
                    yield return null;
            }

            float PiesSobreRaiz()
            {
                float bajo = float.MaxValue;
                if (pieIzq != null) bajo = Mathf.Min(bajo, pieIzq.position.y);
                if (pieDer != null) bajo = Mathf.Min(bajo, pieDer.position.y);
                return bajo - pc.transform.position.y;
            }

            float piesVivo = PiesSobreRaiz();
            campoClip.SetValue(anim, null);
            reloj.SetValue(anim, 0f);
            yield return EsperarMuerte();
            Quaternion columnaProcedural = columna.localRotation;
            float piesProcedural = PiesSobreRaiz();

            campoClip.SetValue(anim, clipMuerte);
            reloj.SetValue(anim, 0f);
            yield return EsperarMuerte();
            Quaternion columnaClip = columna.localRotation;
            float piesClip = PiesSobreRaiz();

            float deltaMuerte = Quaternion.Angle(columnaProcedural, columnaClip);
            Debug.Log("CLIPS_CUERPO maxBrazo=" + maxBrazo.ToString("F2")
                + " deltaMuerte=" + deltaMuerte.ToString("F2")
                + " timeScale=" + Time.timeScale.ToString("F2")
                + " piesVivo=" + piesVivo.ToString("F3")
                + " piesProc=" + piesProcedural.ToString("F3")
                + " piesClip=" + piesClip.ToString("F3")
                + (cadera != null ? " caderaY=" + cadera.localPosition.y.ToString("F4") : ""));
            Assert.Greater(deltaMuerte, 3f,
                "la pose de muerte con clip es igual al procedural: el clip no se aplico");
            Debug.Log("CLIPS_PIERNAS maxDesvioMuslo=" + maxDesvio.ToString("F2")
                + " clips=" + ClipsProcedurales.HayClips);
        }
    }
}
