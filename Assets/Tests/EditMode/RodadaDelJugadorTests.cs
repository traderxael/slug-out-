using NUnit.Framework;
using UnityEngine;
using SlugOut.World;

namespace SlugOut.Tests
{
    /// La rodada del jugador existio porque hasta ahora la unica respuesta contra un tiro en
    /// vuelo era "moverte y rezar": el diseno promete "esquiva o cobertura", y la cobertura no
    /// funciona (SlugShot detecta por barrido de distancia, no por linea de vision, asi que una
    /// roca no tapa nada). Estos tests son la razon de que la rodada sea geometria y no
    /// invulnerabilidad: si alguien le agrega cuadros de inmunidad, el test 2 deja de tener
    /// sentido porque el cuerpo ya no necesita salir del corredor.
    public class RodadaDelJugadorTests
    {
        private static GameObject CrearJugador()
        {
            GameObject go = new GameObject("JugadorDePrueba");
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;   // forward = +Z, la direccion del paso
            go.AddComponent<PlayerDodge>();
            return go;
        }

        [Test]
        public void RodadaCubreSoloTirosDentroDeSuPropiaVentana()
        {
            GameObject go = CrearJugador();
            PlayerDodge dodge = go.GetComponent<PlayerDodge>();

            // Con los valores de diseno (12 m/s x 0,3 s = 3,6 m de paso, margen 0,9 m), un tiro
            // de 24 m/s a 6 m cubre; a 9 m ya llega tarde; y a medio metro esta encima.
            Assert.GreaterOrEqual(dodge.PasoLateral, 3.5f, "el paso lateral tiene que sacar el cuerpo");
            Assert.IsTrue(dodge.CubreTiro(6f, 24f), "a 6 m de un tiro de 24 m/s la rodada alcanza");
            Assert.IsFalse(dodge.CubreTiro(9f, 24f), "a 9 m el tiro llega despues de terminar la rodada");
            Assert.IsFalse(dodge.CubreTiro(0.5f, 24f), "con el tiro encima avisar no sirve de nada");

            // Un tiro mas rapido amplía el alcance util: la ley es de tiempo, no de distancia fija.
            Assert.IsTrue(dodge.CubreTiro(9f, 62f), "un tiro de 62 m/s recorre 9 m en 0,15 s, cabe");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RodadaSacaElCuerpoDelCorredorDelTiro()
        {
            GameObject go = CrearJugador();
            PlayerDodge dodge = go.GetComponent<PlayerDodge>();

            // El corredor: un tiro que entra por -X y barre 20 m pasando justo por el jugador.
            Vector3 desde = new Vector3(-2f, 0f, 0f);
            Vector3 hasta = new Vector3(18f, 0f, 0f);
            const float radio = 0.8f;   // el radio real de SlugShot

            Assert.IsTrue(SlugShot.AciertaBarrida(desde, hasta, go.transform.position, radio),
                "control: sin rodar, ese tiro si lo agarra");

            Assert.IsTrue(dodge.IntentarRodada(), "la rodada arranca con el cuerpo en reposo");
            for (int i = 0; i < 20; i++) dodge.Paso(1f / 60f);   // 0,333 s > duracion de la rodada

            Assert.IsFalse(SlugShot.AciertaBarrida(desde, hasta, go.transform.position, radio),
                "despues de la rodada el cuerpo ya no esta en el corredor");
            Assert.AreEqual(1, dodge.Rodadas, "la rodada completa cuenta una");
            Assert.Greater(go.transform.position.z, 3f, "el desplazamiento es del orden de 3,6 m");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RodadaCuestaRecuperacionYEnfriamiento()
        {
            GameObject go = CrearJugador();
            PlayerDodge dodge = go.GetComponent<PlayerDodge>();

            dodge.IntentarRodada();
            for (int i = 0; i < 18; i++) dodge.Paso(1f / 60f);   // 0,30 s: termina el paso
            Assert.IsTrue(dodge.Expuesto, "al terminar el paso hay una ventana de recuperacion");
            Assert.IsFalse(dodge.IntentarRodada(), "en la recuperacion no se puede volver a rodar");

            for (int i = 0; i < 12; i++) dodge.Paso(1f / 60f);   // 0,20 s de recuperacion
            Assert.IsFalse(dodge.Expuesto, "la recuperacion se acaba sola");
            Assert.Greater(dodge.EnfriamientoRestante, 0.5f, "queda enfriamiento visible");
            Assert.IsFalse(dodge.IntentarRodada(), "con enfriamiento no se rueda");
            Assert.AreEqual(2, dodge.IntentosBloqueados, "los dos intentos bloqueados quedan contados");

            for (int i = 0; i < 72; i++) dodge.Paso(1f / 60f);   // 1,2 s > enfriamiento de 1,1 s
            Assert.LessOrEqual(dodge.EnfriamientoRestante, 0f, "el enfriamiento se agota");
            Assert.IsTrue(dodge.IntentarRodada(), "rodada lista de nuevo");
            Assert.IsTrue(dodge.Rodando, "la segunda rodada arranco");
            Assert.AreEqual(1, dodge.Rodadas, "Rodadas solo cuenta las terminadas");

            for (int i = 0; i < 20; i++) dodge.Paso(1f / 60f);
            Assert.AreEqual(2, dodge.Rodadas, "y al terminar el paso la segunda cuenta");

            Object.DestroyImmediate(go);
        }
    }
}
