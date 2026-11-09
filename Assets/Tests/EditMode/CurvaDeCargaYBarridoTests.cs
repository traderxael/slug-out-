using NUnit.Framework;
using UnityEngine;
using SlugOut.World;

namespace SlugOut.Tests
{
    /// Dos aserciones que existen porque SIN ellas el bug vuelve. No cubren "que el codigo ande":
    /// cubren las dos invariantes que se rompieron silenciosamente y que ninguna sonda del roadmap
    /// veia, porque las sondas miraban contadores de archivos y no la curva.
    public class CurvaDeCargaYBarridoTests
    {
        const float Tolerancia = 1e-4f;

        [Test]
        public void DpsNoTieneMaximoInteriorEnElTramoAlto()
        {
            // El diseno castiga la carga con cadencia (RecargaEscala), y eso estuvo a punto de
            // devolverle la razon al que soltaba en la muesca: si el cociente dano/recarga tuviera
            // un maximo ANTES del tope, soltar a full pasa a estar dominado y el ultimo tercio de
            // la barra queda inutil, que es exactamente el bug que se arreglo.
            float maximo = 0f;
            float donde = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                float t = i / 1000f;
                float dps = PlayerShooter.DanoEscalaPara(t) / PlayerShooter.RecargaEscala(t);
                if (dps > maximo) { maximo = dps; donde = t; }
            }
            Assert.AreEqual(1f, donde, 0.001f,
                "el DPS maximo debe estar en el tope de la barra (t=1), no en el medio");

            float dpsTope = PlayerShooter.DanoEscalaPara(1f) / PlayerShooter.RecargaEscala(1f);
            Assert.Greater(dpsTope, PlayerShooter.DanoEscalaPara(0f) / PlayerShooter.RecargaEscala(0f),
                "cargar tiene que render mas de lo que cuesta");

            // En el tramo alto (desde la muesca hacia arriba) la curva NO baja en ningun paso:
            // ahi es donde antes habia una meseta y soltar en la marca era igual de bueno.
            float muesca = PlayerShooter.MuescaUmbral(44.7f, 0.6f, 20f, 62f);
            float anterior = PlayerShooter.DanoEscalaPara(muesca) / PlayerShooter.RecargaEscala(muesca);
            for (int i = 1; i <= 400; i++)
            {
                float t = Mathf.Min(1f, muesca + (1f - muesca) * i / 400f);
                float dps = PlayerShooter.DanoEscalaPara(t) / PlayerShooter.RecargaEscala(t);
                Assert.GreaterOrEqual(dps, anterior - Tolerancia,
                    "el DPS cayo dentro del tramo alto en t=" + t.ToString("F4"));
                anterior = dps;
            }
            Assert.AreEqual(1f, PlayerShooter.DanoEscalaPara(0f), Tolerancia, "t=0 no multiplica el dano");
        }

        [Test]
        public void MuescaSigueSiendoCruzarElUmbral()
        {
            // La marca del HUD se resuelve invirtiendo la curva de velocidad; si alguien cambia una
            // de las dos leyes sin la otra, la muesca miente y el jugador carga de mas.
            const float cargaMax = 0.6f, min = 20f, max = 62f, umbral = 44.7f;
            float muesca = PlayerShooter.MuescaUmbral(umbral, cargaMax, min, max);
            float velocidadEnMuesca = PlayerShooter.VelocidadPara(muesca * cargaMax, cargaMax, min, max);
            // Con tolerancia: MuescaUmbral resuelve por biseccion (32 pasos) y devuelve
            // 44,6999969 para un umbral de 44,7. La marca del HUD y la ley de fase concuerdan a
            // 1/1000 de MPH, que es menos de lo que se puede apretar el boton.
            Assert.GreaterOrEqual(velocidadEnMuesca, umbral - 0.001f, "en la muesca ya se cruzo el umbral");
            Assert.LessOrEqual(velocidadEnMuesca, umbral + 0.001f, "la muesca no se paso del umbral");
            float unPocoAntes = PlayerShooter.VelocidadPara((muesca - 0.01f) * cargaMax, cargaMax, min, max);
            Assert.Less(unPocoAntes, umbral, "un pelo antes de la muesca todavia no se cruza");
        }

        [Test]
        public void BarridoAcierraCuandoElPasoDelFrameSuperaElRadio()
        {
            // Un tiro a 62 m/s con el timestep maximo (1/3 s) barre mas de 20 metros: el test de
            // "punto contra disco despues de mover" lo dejaba pasar de largo y el disparo mas
            // fuerte era el que mas fallaba. Ningun extremo del segmento esta dentro del radio,
            // asi que si esto falla, volvio la prueba por punto.
            Vector3 desde = new Vector3(0f, 0f, 0f);
            Vector3 hasta = new Vector3(20.73f, 0f, 0f);
            Vector3 objetivo = new Vector3(10f, 0f, 0.4f);
            const float radio = 0.8f;

            Assert.Greater((objetivo - desde).magnitude, radio, "control: el nacimiento esta afuera");
            Assert.Greater((objetivo - hasta).magnitude, radio, "control: el final esta afuera");
            Assert.IsTrue(SlugShot.AciertaBarrida(desde, hasta, objetivo, radio),
                "el segmento pasa por al lado del cuerpo y tiene que contar como impacto");

            // y los falsos positivos: un cuerpo apartado del corredor NO cuenta, aunque el paso
            // lo tenga cerca en una dimension
            Assert.IsFalse(SlugShot.AciertaBarrida(desde, hasta, new Vector3(10f, 0f, 5f), radio),
                "el corredor no pasa por ese cuerpo");
            Assert.IsFalse(SlugShot.AciertaBarrida(desde, hasta, new Vector3(-4f, 0f, 0.2f), radio),
                "detras del nacimiento no se puede haber chocado");
        }

        [Test]
        public void VentanaDeEsquivaDependeDeLosStatsYNoDelAzar()
        {
            const float margen = 0.9f, ventana = 0.46f;

            // bicho lento: el paso lateral no saca medio cuerpo del corredor, jamas esquiva
            float pasoLento = 1.0f * 2.2f * 0.26f;
            for (int i = 1; i <= 30; i++)
            {
                float d = i * 0.5f;
                Assert.IsFalse(EnemySlugAI.VentanaDeEsquiva(d, 30f, pasoLento, ventana, margen),
                    "un bicho con paso de " + pasoLento + " m no puede esquivar a " + d + " m");
            }

            float paso = 3.2f * 2.2f * 0.26f;
            Assert.IsTrue(EnemySlugAI.VentanaDeEsquiva(6f, 30f, paso, ventana, margen), "dentro del alcance");
            Assert.IsFalse(EnemySlugAI.VentanaDeEsquiva(30f, 30f, paso, ventana, margen), "fuera del alcance");
            Assert.IsFalse(EnemySlugAI.VentanaDeEsquiva(0.5f, 30f, paso, ventana, margen), "ya encima");

            // determinista: misma entrada, misma salida, mil veces
            bool primera = EnemySlugAI.VentanaDeEsquiva(6f, 30f, paso, ventana, margen);
            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(primera, EnemySlugAI.VentanaDeEsquiva(6f, 30f, paso, ventana, margen));
        }
    }
}
