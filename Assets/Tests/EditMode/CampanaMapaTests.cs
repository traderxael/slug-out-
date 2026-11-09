using NUnit.Framework;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    public class CampanaMapaTests
    {
        [Test] public void LosVeintiochoNivelesSonSietePorCueva()
        {
            int total = 0;
            for (int r = 0; r < Regiones.Todas.Length; r++)
            {
                int enRegion = 0;
                for (int nivel = 1; nivel <= 28; nivel++)
                    if (CampanaMapa.RegionDeNivel(nivel) == r) enRegion++;
                Assert.AreEqual(CampanaMapa.NivelesPorRegion, enRegion,
                    "la cueva " + r + " no tiene siete niveles");
                total += enRegion;
            }
            Assert.AreEqual(28, total);
        }

        [Test] public void LasFilasCrecenYElContenidoCubreLaUltima()
        {
            float anterior = float.MinValue;
            for (int nivel = 1; nivel <= 28; nivel++)
            {
                float y = CampanaMapa.YDeNivel(nivel);
                Assert.Greater(y, anterior, "las filas se pisan en el nivel " + nivel);
                anterior = y;
            }
            Assert.GreaterOrEqual(CampanaMapa.AlturaContenido(28),
                CampanaMapa.YDeNivel(28) + 52f,
                "el scroll no llega a mostrar el ultimo nivel");
            for (int r = 0; r < Regiones.Todas.Length; r++)
                Assert.That(CampanaMapa.YDeEncabezado(r), Is.LessThan(CampanaMapa.YDeNivel(
                    CampanaMapa.PrimerNivel(r))));
        }

        [Test] public void ElMapaSeleccionaDentroDeLaRegionSinDesbloquear()
        {
            int[] desbloqueados = { 1, 7, 8, 15, 22, 28 };
            for (int d = 0; d < desbloqueados.Length; d++)
                for (int r = 0; r < Regiones.Todas.Length; r++)
                {
                    int nivel = CampanaMapa.NivelInicial(r, desbloqueados[d]);
                    Assert.That(nivel, Is.InRange(CampanaMapa.PrimerNivel(r),
                        CampanaMapa.UltimoNivel(r)));
                    // En una cueva futura el nodo selecciona su primer nivel bloqueado para
                    // mostrar la condicion; nunca uno posterior al progreso dentro de la cueva.
                    int techo = desbloqueados[d] >= CampanaMapa.PrimerNivel(r)
                        ? desbloqueados[d] : CampanaMapa.PrimerNivel(r);
                    Assert.LessOrEqual(nivel, techo,
                        "el mapa no puede seleccionar un nivel que el jugador no abrio");
                }
        }

        [Test] public void ElProgresoPorCuevaNoSePasaDeSeis()
        {
            Assert.AreEqual(0, CampanaMapa.Completados(0, 1));
            Assert.AreEqual(3, CampanaMapa.Completados(0, 4));
            Assert.AreEqual(6, CampanaMapa.Completados(0, 8));
            Assert.AreEqual(0, CampanaMapa.Completados(1, 8));
            // Con el nivel 28 desbloqueado, la cueva 4 (Templo Olvidado) tiene 3 completados.
            Assert.AreEqual(3, CampanaMapa.Completados(4, 28));
            Assert.AreEqual(6, CampanaMapa.Completados(3, 29));
        }
    }
}
