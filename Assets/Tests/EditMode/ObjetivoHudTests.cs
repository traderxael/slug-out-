using NUnit.Framework;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    /// El objetivo del nivel tiene dos fases (capturar y despues despejar) y un singular que
    /// antes mentia ("1 BABOSAS"). Es texto puro: se prueba sin escena.
    public class ObjetivoHudTests
    {
        [Test]
        public void LaCuotaEnSingularNoDiceBABOSAS()
        {
            string t = GameHUD.TextoObjetivo(1, 0, 2, false, 1);
            StringAssert.Contains("1 BABOSA", t);
            StringAssert.DoesNotContain("1 BABOSAS", t);
        }

        [Test]
        public void ConLaCuotaCumplidaElHudPideDespejarLaArena()
        {
            string t = GameHUD.TextoObjetivo(2, 2, 3, false, 5);
            StringAssert.Contains("DESPEJÁ LA ARENA", t);
            StringAssert.Contains("3 VIVOS", t);
            StringAssert.DoesNotContain("CAPTURÁ", t);
        }

        [Test]
        public void ElUltimoVivoVaEnSingular()
        {
            StringAssert.Contains("1 VIVO", GameHUD.TextoObjetivo(2, 2, 1, false, 5));
            StringAssert.DoesNotContain("1 VIVOS", GameHUD.TextoObjetivo(2, 2, 1, false, 5));
        }

        [Test]
        public void InfinitoYModoBajaATodosConservanSuTexto()
        {
            StringAssert.Contains("OLEADA 7", GameHUD.TextoObjetivo(0, 0, 2, true, 7));
            StringAssert.Contains("BAJÁ A TODOS", GameHUD.TextoObjetivo(0, 0, 2, false, 1));
        }

        [Test]
        public void SinVivosYCuotaCumplidaMuestraObjetivoCumplido()
        {
            StringAssert.Contains("OBJETIVO CUMPLIDO", GameHUD.TextoObjetivo(2, 2, 0, false, 5));
        }

        [Test]
        public void LaAyudaDeCapturaSoloSaleEnElNivel1SinCapturas()
        {
            Assert.IsTrue(GameHUD.MostrarAyudaCaptura(1, 1, 0));
            Assert.IsFalse(GameHUD.MostrarAyudaCaptura(1, 1, 1),
                "despues de la primera captura del nivel ya no hace falta");
            Assert.IsFalse(GameHUD.MostrarAyudaCaptura(2, 1, 0),
                "los demas niveles ya tienen la muesca de la barra");
            Assert.IsFalse(GameHUD.MostrarAyudaCaptura(1, 0, 0),
                "sin cuota de capturas no hay nada que explicar");

            StringAssert.Contains("35 %", GameHUD.TextoAyudaCaptura);
            StringAssert.Contains("CAPTUR", GameHUD.TextoAyudaCaptura);
        }
    }
}
