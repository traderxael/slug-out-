#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace SlugOut.World
{
    // Arnes editor-only del ciclo de vinculo. No entra en builds.
    // Preserva la coleccion/equipo del jugador al terminar.
    public class ArneltVinculoEscena : MonoBehaviour
    {
        private const string kCol = "slugout.coleccion";
        private const string kEq = "slugout.equipo";
        private const string kColBackup = "slugout.test.col";
        private const string kEqBackup = "slugout.test.eq";

        public static void Restaurar()
        {
            if (PlayerPrefs.HasKey(kColBackup))
            {
                PlayerPrefs.SetString(kCol, PlayerPrefs.GetString(kColBackup, ""));
                PlayerPrefs.DeleteKey(kColBackup);
            }
            if (PlayerPrefs.HasKey(kEqBackup))
            {
                PlayerPrefs.SetString(kEq, PlayerPrefs.GetString(kEqBackup, ""));
                PlayerPrefs.DeleteKey(kEqBackup);
            }
            PlayerPrefs.Save();
        }

        public static void Run()
        {
            var go = new GameObject("ArneltVinculoEscena");
            go.AddComponent<ArneltVinculoEscena>().Iniciar();
        }

        private void Iniciar()
        {
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Probar());
        }

        private IEnumerator Probar()
        {
            string colAntes = PlayerPrefs.GetString(kCol, "");
            string eqAntes = PlayerPrefs.GetString(kEq, "");
            PlayerPrefs.SetString(kColBackup, colAntes);
            PlayerPrefs.SetString(kEqBackup, eqAntes);
            PlayerPrefs.Save();
            var sb = new StringBuilder();
            try
            {
                GameFlow gf = GameFlow.Activo;
                if (gf == null)
                {
                    Report(sb.AppendLine("SETUP FAIL: sin GameFlow"));
                    yield break;
                }
                if (gf.EnMenu) gf.IniciarCampana(1);
                GameFeel.Pausa = false;
                Time.timeScale = 1f;
                Application.runInBackground = true;
                yield return new WaitForSeconds(1.5f);

                BabosaSalvaje slug = Object.FindFirstObjectByType<BabosaSalvaje>();
                if (slug == null)
                {
                    Report(sb.AppendLine("SETUP FAIL: sin BabosaSalvaje"));
                    yield break;
                }

                FieldInfo campo = typeof(BabosaSalvaje).GetField("_jugador",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Transform jugador = campo != null ? campo.GetValue(slug) as Transform : null;
                if (jugador == null)
                {
                    Report(sb.AppendLine("SETUP FAIL: salvaje sin _jugador"));
                    yield break;
                }

                PlayerController pc = jugador.GetComponentInParent<PlayerController>();
                if (pc != null) pc.enabled = false;

                string id = slug.slugId;
                bool yaColeccion = GameFlow.EstaDescubierta(id);
                string[] eqInicial = GameFlow.Equipo();
                Vector3 puesto = slug.transform.position;

                Mover(jugador, puesto, 11f);
                yield return new WaitForSeconds(1.5f);
                bool detectada = slug.Ciclo.Actual == Vinculo.Etapa.Detectada;
                sb.AppendLine("P1 detectada=" + detectada + " etapa=" + slug.Ciclo.Actual
                    + " dist=" + slug.DistanciaJugador.ToString("F1"));

                Mover(jugador, puesto, 7f);
                float t = 0f;
                while (t < 6f && !slug.Ciclo.Aprendida)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                bool aprendida = slug.Ciclo.Aprendida;
                sb.AppendLine("P2 aprendida=" + aprendida + " etapa=" + slug.Ciclo.Actual
                    + " mensaje=" + slug.Ciclo.Mensaje);

                t = 0f;
                while (t < 2.6f)
                {
                    t += Time.deltaTime;
                    float d = Mathf.Lerp(7f, 3.4f, Mathf.Clamp01(t / 2.6f));
                    Mover(jugador, puesto, d);
                    yield return null;
                }

                t = 0f;
                while (t < 7f && !slug.Ciclo.Vinculada)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                bool vinculada = slug.Ciclo.Vinculada;
                bool registrada = GameFlow.EstaDescubierta(id);
                bool enEquipo = System.Array.IndexOf(GameFlow.Equipo(), id) >= 0;
                sb.AppendLine("P3 vinculada=" + vinculada + " etapa=" + slug.Ciclo.Actual
                    + " mensaje=" + slug.Ciclo.Mensaje);
                sb.AppendLine("P4 registrada=" + registrada + " enEquipo=" + enEquipo
                    + " yaColeccion=" + yaColeccion + " id=" + id
                    + " equipoAntes=" + string.Join(",", eqInicial)
                    + " equipoDespues=" + string.Join(",", GameFlow.Equipo()));

                int pass = (detectada ? 1 : 0) + (aprendida ? 1 : 0) + (vinculada ? 1 : 0)
                    + ((registrada && enEquipo) ? 1 : 0);
                sb.AppendLine("RESUMEN " + pass + "/4");

                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Assets/Screenshots/vinculo_escena.png");
                Report(sb);
            }
            finally
            {
                Restaurar();
            }
            Destroy(gameObject);
        }

        private static void Mover(Transform jugador, Vector3 puesto, float distancia)
        {
            float x = puesto.x;
            float z = puesto.z + distancia;
            jugador.position = new Vector3(x, Terrain3D.Altura(x, z) + 0.1f, z);
        }

        private static void Report(StringBuilder sb)
        {
            string texto = sb.ToString();
            File.WriteAllText("C:/Users/USUARIO/tools/unity-pipeline/vinculo_escena.txt", texto);
            Debug.Log("[ArneltVinculoEscena] " + texto);
        }
    }
}
#endif
