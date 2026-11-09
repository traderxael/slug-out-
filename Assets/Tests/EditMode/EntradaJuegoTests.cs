using NUnit.Framework;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// La puerta de entrada es pura en su combinacion: se prueba sin escena ni gamepad fisico.
    public class EntradaJuegoTests
    {
        [Test]
        public void ElTecladoMandaEnElMovimientoYLosBotonesSeSuman()
        {
            EntradaFrame teclado = new EntradaFrame
            {
                mover = new Vector2(1f, 0f),
                disparo = true,
                dispositivo = DispositivoEntrada.Teclado
            };
            EntradaFrame mando = new EntradaFrame
            {
                mover = new Vector2(0f, 1f),
                rodada = true,
                habilidad = true,
                pasoCuidado = true,
                pausa = true,
                confirmar = true,
                cancelar = true,
                menuArriba = true,
                dispositivo = DispositivoEntrada.Mando
            };

            EntradaFrame r = EntradaJuego.Combinar(teclado, mando);
            Assert.AreEqual(teclado.mover, r.mover, "el movimiento del teclado tiene prioridad");
            Assert.IsTrue(r.disparo, "se perdio el disparo del teclado");
            Assert.IsTrue(r.rodada && r.habilidad && r.pasoCuidado, "se perdieron acciones del mando");
            Assert.IsTrue(r.pausa && r.confirmar && r.cancelar && r.menuArriba, "se perdieron flancos del mando");
            Assert.IsFalse(r.menuAbajo, "no debe inventar flancos");
        }

        [Test]
        public void SinMovimientoDeTecladoElMandoMueve()
        {
            EntradaFrame teclado = new EntradaFrame { mover = Vector2.zero, dispositivo = DispositivoEntrada.Teclado };
            EntradaFrame mando = new EntradaFrame { mover = new Vector2(-0.5f, 0.25f), dispositivo = DispositivoEntrada.Mando };
            Assert.AreEqual(mando.mover, EntradaJuego.Combinar(teclado, mando).mover);
        }

        [Test]
        public void LaAyudaCambiaDeGlifosSegunElDispositivo()
        {
            string teclado = EntradaJuego.TextoAyuda(DispositivoEntrada.Teclado);
            string mando = EntradaJuego.TextoAyuda(DispositivoEntrada.Mando);
            StringAssert.Contains("WASD", teclado);
            StringAssert.Contains("ESPACIO", teclado);
            StringAssert.Contains("LS", mando);
            StringAssert.Contains("RT", mando);
            StringAssert.Contains("START", mando);
        }

        [Test]
        public void ElFocoDeMenuDaLaVueltaEnLosDosSentidos()
        {
            Assert.AreEqual(0, NavegacionFoco.Mover(2, 1, 3), "del ultimo al primero");
            Assert.AreEqual(2, NavegacionFoco.Mover(0, -1, 3), "del primero al ultimo");
            Assert.AreEqual(1, NavegacionFoco.Mover(3, 1, 3), "un indice fuera de rango vuelve dentro");
            Assert.AreEqual(0, NavegacionFoco.Mover(0, 0, 0), "sin filas no hay foco");
        }
    }
}
