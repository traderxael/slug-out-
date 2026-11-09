using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    /// El ciclo observar→vinculo se rompio por una distancia entre dos capas: la regla del
    /// vinculo pide entrar en la bola de acercamiento a menos de 2,2 m/s, y el jugador solo
    /// sabia moverse a 0 o a 6 m/s. Las pruebas sinteticas de la maquina pura pasaban igual,
    /// porque nadie alimentaba con las velocidades que el controlador realmente puede dar.
    /// Este archivo fija las dos cosas que faltaban: que el paso cuidado del jugador este
    /// por debajo del umbral, y que el ciclo cierre usando unicamente velocidades alcanzables.
    public class VinculoAlcanceTests
    {
        private GameObject _go;
        private PlayerController _pc;

        [SetUp] public void ArmarJugador()
        {
            _go = new GameObject("JugadorPrueba");
            _pc = _go.AddComponent<PlayerController>();
        }

        [TearDown] public void DesarmarJugador()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            _go = null; _pc = null;
        }

        private float Velocidad(string campo)
        {
            FieldInfo f = typeof(PlayerController).GetField(campo,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(f, Is.Not.Null, "PlayerController perdio el campo " + campo);
            return Convert.ToSingle(f.GetValue(_pc));
        }

        [Test] public void ElPasoCuidadoCabeBajoElUmbralDeConfianza()
        {
            Vinculo v = new Vinculo();
            float paso = Velocidad("pasoCuidadoSpeed");
            Assert.That(paso, Is.LessThan(v.velocidadCuidado),
                "con " + paso + " m/s el jugador no puede entrar en la bola sin asustarla");
            Assert.That(paso, Is.GreaterThan(v.velocidadQuieto),
                "el paso tiene que ser movible: si fuera 0 seria quedarse quieto dos veces");
            Assert.That(Vinculo.VelocidadPasoCuidado, Is.EqualTo(paso).Within(0.001f),
                "el control y la regla vuelven a tener cada uno su numero");
        }

        [Test] public void CorrerSigueAsustando()
        {
            Vinculo v = new Vinculo();
            Assert.That(Velocidad("speed"), Is.GreaterThan(v.velocidadCuidado),
                "si la carrera entrara en el umbral se pierde el error que hay que ensenar");
        }

        /// El arnés de alcance: recorre el ciclo con las dos unicas velocidades del controlador.
        private Vinculo Acechar(float velocidadDeAproximacion, int cuadros, out string traza)
        {
            var sb = new System.Text.StringBuilder();
            Vinculo v = new Vinculo();
            float corriendo = Velocidad("speed");
            float dist = 14f, ultima = dist;
            Vinculo.Etapa anterior = v.Actual;

            for (int i = 0; i < cuadros; i++)
            {
                float usar;
                if (dist > 6.2f) usar = corriendo;
                else if (v.Actual == Vinculo.Etapa.Conoce || v.Actual == Vinculo.Etapa.Confiando)
                    usar = velocidadDeAproximacion;
                else usar = 0f;

                dist = Math.Max(0.3f, dist - usar * Dt);
                float medida = Math.Abs(ultima - dist) / Dt;
                ultima = dist;
                v.Paso(Dt, dist, medida, true);
                if (v.Actual != anterior)
                {
                    sb.Append(v.Actual + "@d=" + dist.ToString("F2") + " v=" + medida.ToString("F2") + "  ");
                    anterior = v.Actual;
                }
                if (v.Vinculada) break;
            }
            traza = sb.ToString();
            return v;
        }

        const float Dt = 1f / 60f;

        [Test] public void ElCicloCierraACaminoSinAsustarNunca()
        {
            string traza;
            Vinculo v = Acechar(Velocidad("pasoCuidadoSpeed"), 4000, out traza);
            Assert.That(v.Vinculada, Is.True, "traza: " + traza);
            Assert.That(v.Aprendida, Is.True, "hay que haberla aprendido antes de vincularla");
        }

        [Test] public void ConSoloLaCarreraElCicloNoCierra()
        {
            string traza;
            Vinculo v = Acechar(Velocidad("speed"), 4000, out traza);
            Assert.That(v.Vinculada, Is.False,
                "el defecto volvio: sin paso cuidado tampoco se puede (" + traza + ")");
            Assert.That(traza, Does.Contain("Conoce"), "llegar a conocerla si debe funcionar");
        }

        [Test] public void RecuperarseEncimaDeEllaNoVuelveAsustarlaEnBucle()
        {
            Vinculo v = new Vinculo();
            v.Paso(Dt, 11f, 0f);
            for (int i = 0; i < 200; i++) v.Paso(Dt, 7f, 0.1f);
            Assert.That(v.Actual, Is.EqualTo(Vinculo.Etapa.Conoce), "primero hay que conocerla");

            v.Paso(Dt, 4f, 6f, true);
            v.Paso(Dt, 4f, 6f, true);
            Assert.That(v.Actual, Is.EqualTo(Vinculo.Etapa.Asustada));

            // El jugador se queda encima: antes esto reabria Detectada y volvia a asustar
            // cada 5 segundos para siempre. Ahora tiene que pedir espacio y sostenerse.
            int entradasAsustada = 0;
            Vinculo.Etapa anterior = v.Actual;
            for (int i = 0; i < 1200; i++)
            {
                v.Paso(Dt, 4f, 0f);
                if (v.Actual == Vinculo.Etapa.Asustada && anterior != Vinculo.Etapa.Asustada)
                    entradasAsustada++;
                anterior = v.Actual;
            }
            Assert.That(entradasAsustada, Is.EqualTo(0), "sustada dos veces sin que el jugador hiciera nada");
            Assert.That(v.Mensaje, Does.Contain("espacio"));

            // Darle espacio tiene que alcanzar para volver a intentarlo.
            for (int i = 0; i < 200; i++)
            {
                v.Paso(Dt, 8f, 0f);
                if (v.Actual == Vinculo.Etapa.Detectada) break;
            }
            Assert.That(v.Actual, Is.EqualTo(Vinculo.Etapa.Detectada),
                "tras alejarse deberia volver a estar disponible");
        }

        [Test] public void ElMensajeDiceQueTeclaHayQueApretar()
        {
            Vinculo v = new Vinculo();
            v.Paso(Dt, 11f, 0f);
            for (int i = 0; i < 200; i++) { v.Paso(Dt, 7f, 0.1f); if (v.Actual == Vinculo.Etapa.Conoce) break; }
            Assert.That(v.Mensaje, Does.Contain(Vinculo.TeclaPasoCuidado),
                "el paso cuidado no sirve si el juego no dice como darlo: '" + v.Mensaje + "'");

            v.Paso(Dt, 5f, 0f);
            Assert.That(v.Mensaje, Does.Contain(Vinculo.TeclaPasoCuidado),
                "el aviso permanente tampoco nombra la tecla: '" + v.Mensaje + "'");
        }

        /// Medido en Play el 2026-10-19: con el paso cuidado a 1,80 m/s, subiendo una pendiente
        /// de La Cueva Seca, el muestreo leyo 3,96 m/s y asusto a la salvaje. La distancia se
        /// medía en 3D y el controlador pega al piso en cada cuadro: el eje Y se sumaba al avance.
        [Test] public void SubirUnaPendienteEnPasoCuidadoNoCuentaComoCorrer()
        {
            // Un cuadro a 60 fps a 1,80 m/s: 0,03 m de avance y 0,09 m de subida (pendiente 3:1).
            Vector3 a = new Vector3(10f, 1f, 10f);
            Vector3 b = new Vector3(10.03f, 1.09f, 10f);

            Assert.That(Vector3.Distance(a, b), Is.GreaterThan(0.09f),
                "la prueba no esta armando una pendiente de verdad");
            Assert.That(BabosaSalvaje.PasoHorizontal(a, b), Is.EqualTo(0.03f).Within(0.001f),
                "el paso tiene que medir el avance horizontal, no la subida del terreno");
            Assert.That(BabosaSalvaje.PasoHorizontal(a, b) / 0.12f, Is.LessThan(Vinculo.VelocidadPasoCuidado),
                "ese cuadro tiene que quedar por debajo del umbral del paso cuidado");
            // Y un avance horizontal de carrera (0,72 m en la ventana de 0,12 s) sigue midiendo 6.
            Assert.That(BabosaSalvaje.PasoHorizontal(Vector3.zero, new Vector3(0.72f, 0f, 0f)) / 0.12f,
                Is.EqualTo(6f).Within(0.01f), "la guarda de carrera tiene que seguir viendo 6 m/s");
        }

        /// Lleva el estado a Conoce observando, y despues entra en paso cuidado hasta Confiando.
        private static Vinculo ConocerYConfiando()
        {
            Vinculo v = new Vinculo();
            v.Paso(Dt, 11f, 0f);
            for (int i = 0; i < 400 && v.Actual != Vinculo.Etapa.Conoce; i++) v.Paso(Dt, 7f, 0.1f);
            Assert.That(v.Actual, Is.EqualTo(Vinculo.Etapa.Conoce), "no llego a Conoce");
            // El juego, ya en Conoce, pasa cuadros observando desde 6 m (fuera de la bola) y eso
            // reinicia el reloj de confianza. Sin esto la prueba entraba con el reloj de la
            // observacion y "confiaba" en un cuadro.
            for (int i = 0; i < 5; i++) v.Paso(Dt, 6.3f, 0.1f);
            v.Paso(Dt, 4.2f, 1.8f, true);
            Assert.That(v.Actual, Is.EqualTo(Vinculo.Etapa.Confiando), "el paso cuidado no abrio Confiando");
            return v;
        }

        /// Una ventana de medicion con un pico (medido en Play: 6,31 m/s mientras el jugador
        /// avanzaba a 1,81) no puede costar el vinculo.
        [Test] public void UnaSolaVentanaRapidaNoAsusta()
        {
            Vinculo v = ConocerYConfiando();
            v.Paso(Dt, 4.1f, 6.3f, true);
            Assert.That(v.Actual, Is.EqualTo(Vinculo.Etapa.Confiando),
                "una sola ventana rapida asusto a la salvaje");
            for (int i = 0; i < 20; i++) v.Paso(Dt, 4.1f, 1.8f, true);
            Assert.That(v.Actual, Is.EqualTo(Vinculo.Etapa.Confiando),
                "la ventana aislada corto la confianza");
        }

        [Test] public void DosVentanasRapidasSeguidasAsustan()
        {
            Vinculo v = ConocerYConfiando();
            v.Paso(Dt, 4.1f, 6.0f, true);
            Assert.That(v.Actual, Is.EqualTo(Vinculo.Etapa.Confiando), "la primera ventana no asusta");
            v.Paso(Dt, 4.1f, 6.0f, true);
            Assert.That(v.Actual, Is.EqualTo(Vinculo.Etapa.Asustada),
                "correr sostenido dentro de 4,5 m tiene que asustar");
        }
    }
}
