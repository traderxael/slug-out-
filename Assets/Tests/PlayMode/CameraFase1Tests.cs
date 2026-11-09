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
    /// Cámara de la fase 1: anticipación por desplazamiento, FOV por velocidad y
    /// ninguna posición por debajo del terreno. No cambia el encuadre base que ya
    /// protege GameplayLookTests.
    public class CameraFase1Tests
    {
        [UnityTest]
        public IEnumerator AnticipaYNoEntraEnElSuelo()
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

            StoryPanel historia = UnityEngine.Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 60f;
            EnemySlugAI[] ias = UnityEngine.Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++) if (ias[i] != null) ias[i].enabled = false;

            PlayerController pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Camera cam = Camera.main;
            Assert.IsNotNull(pc, "no hay PlayerController");
            Assert.IsNotNull(cam, "no hay camara principal");
            CameraFollow follow = cam.GetComponent<CameraFollow>();
            Assert.IsNotNull(follow, "la camara no tiene CameraFollow");

            pc.transform.rotation = Quaternion.identity;
            for (int i = 0; i < 20; i++) yield return null;
            float fovBase = cam.fieldOfView;
            Vector3 inicio = pc.transform.position;

            // Movimiento real con paso fijo: la camara debe mirar adelante y abrir FOV.
            for (int i = 0; i < 20; i++)
            {
                pc.transform.position += Vector3.forward * 0.06f;
                pc.ApoyarEnPiso();
                yield return null;
            }
            yield return null;
            Vector3 adelanto = follow.FocoActual - pc.transform.position;
            adelanto.y = 0f;
            float fovMovimiento = cam.fieldOfView;
            Debug.Log("CAM_FASE1 focoAdelante=" + adelanto.z.ToString("F2")
                + " fovBase=" + fovBase.ToString("F1")
                + " fovMovimiento=" + fovMovimiento.ToString("F1")
                + " comfort=" + GameFlow.ReducirDestellos);
            Assert.Greater(adelanto.z, 0.05f, "la camara no anticipa el avance");
            if (!GameFlow.ReducirDestellos)
                Assert.Greater(fovMovimiento, fovBase + 0.2f,
                    "el FOV no responde a la velocidad");
            else
                Assert.That(fovMovimiento, Is.EqualTo(fovBase).Within(0.3f),
                    "el confort debe dejar el FOV fijo");

            // Varios puntos del terreno: la camara nunca queda enterrada.
            Vector3[] puntos =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(12f, 0f, 8f),
                new Vector3(-10f, 0f, 14f),
                new Vector3(18f, 0f, -6f),
            };
            for (int p = 0; p < puntos.Length; p++)
            {
                Vector3 q = puntos[p];
                q.y = Terrain3D.Altura(q.x, q.z) + 1f;
                pc.transform.position = q;
                for (int i = 0; i < 15; i++) yield return null;
                float suelo = Terrain3D.Altura(cam.transform.position.x, cam.transform.position.z);
                Assert.GreaterOrEqual(cam.transform.position.y, suelo + 0.05f,
                    "la camara entro en el terreno en " + q.ToString("F1"));
                Assert.IsFalse(float.IsNaN(cam.transform.position.x)
                    || float.IsNaN(cam.transform.position.y)
                    || float.IsNaN(cam.transform.position.z),
                    "la camara quedo en NaN");
                if (p == 0)
                    Assert.Less(Vector3.Distance(pc.transform.position, inicio), 50f,
                        "el jugador se fue lejos del punto de prueba");
            }
            Time.captureDeltaTime = 0f;
        }
    }
}
