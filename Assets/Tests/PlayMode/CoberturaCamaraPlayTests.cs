using System.Collections;
using NUnit.Framework;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// El tiro del jugador nacia con el Collider del primitivo. El Raycast de la camara
    /// (mascara ~0) lo golpeaba y pegaba un zoom; ahora usa SlugShot.Lanzar, que lo elimina.
    public class CoberturaCamaraPlayTests
    {
        [UnityTest]
        public IEnumerator ElTiroDelJugadorNoDejaCollider()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;

            PlayerShooter shooter = Object.FindAnyObjectByType<PlayerShooter>();
            Assert.IsNotNull(shooter, "no hay PlayerShooter en el nivel");

            shooter.SoltarPrueba(0.3f);
            yield return null;

            SlugShot mio = null;
            SlugShot[] tiros = Object.FindObjectsByType<SlugShot>();
            for (int i = 0; i < tiros.Length; i++)
                if (tiros[i] != null && tiros[i].DesdeJugador) { mio = tiros[i]; break; }

            Assert.IsNotNull(mio, "SoltarPrueba no genero un SlugShot del jugador");
            Assert.That(mio.GetComponent<Collider>(), Is.Null,
                "el tiro conserva un Collider y la camara lo va a golpear");
            Object.Destroy(mio.gameObject);
        }

        /// La camara no puede quedar dentro del muro cuando el jugador se le pega de espaldas:
        /// el clamp de distanciaMinima la empujaba al otro lado de la piedra.
        [UnityTest]
        public IEnumerator LaCamaraNoSeQuedaDentroDelMuroDelBorde()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;

            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            Camera cam = Camera.main;
            Assert.IsNotNull(pc, "no hay PlayerController");
            Assert.IsNotNull(cam, "no hay camara principal");

            pc.Colocar(new Vector3(23.5f, 0f, 0f));
            pc.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            cam.transform.position = new Vector3(31f, 9f, 0f);
            yield return new WaitForSecondsRealtime(1.2f);

            Assert.Less(cam.transform.position.x, MurosCueva.CaraBorde,
                "la camara quedo dentro del muro del borde: x=" + cam.transform.position.x.ToString("F2"));
            Assert.Greater(cam.transform.position.x, 23f,
                "la camara se aplasto contra el jugador en vez de quedarse antes del muro");
        }
    }
}
