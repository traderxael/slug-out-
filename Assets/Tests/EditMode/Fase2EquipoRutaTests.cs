using NUnit.Framework;
using SlugOut.Core;
using SlugOut.Slugs;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    /// Fase 2: filtros de equipo/coleccion y estados de la ruta. Son funciones puras
    /// sobre SlugData; no tocan PlayerPrefs, IDs ni la seleccion.
    public class Fase2EquipoRutaTests
    {
        private static SlugData PrimeroDe(ElementType e)
        {
            SlugData[] t = SlugRoster.Todos;
            if (t == null) return null;
            for (int i = 0; i < t.Length; i++)
                if (t[i] != null && t[i].element == e) return t[i];
            return null;
        }

        [Test] public void ElFiltroDeElementoNoRenombraIds()
        {
            SlugData fuego = PrimeroDe(ElementType.Fire);
            SlugData agua = PrimeroDe(ElementType.Water);
            Assert.IsNotNull(fuego, "el roster no tiene ninguna babosa de Fuego");
            Assert.IsNotNull(agua, "el roster no tiene ninguna babosa de Agua");

            Assert.IsTrue(GameFlow.CoincideElemento(fuego, (int)ElementType.Fire));
            Assert.IsFalse(GameFlow.CoincideElemento(fuego, (int)ElementType.Water));
            Assert.IsTrue(GameFlow.CoincideElemento(fuego, GameFlow.FiltroTodas));
            Assert.IsTrue(GameFlow.CoincideElemento(fuego, GameFlow.FiltroEquipadas));
            Assert.IsFalse(GameFlow.CoincideElemento(null, GameFlow.FiltroTodas));

            // El id es el de siempre; la etiqueta es solo presentacion.
            Assert.IsNotNull(fuego.slugId);
            Assert.AreEqual("Fuego", SlugTexto.NombreElemento(ElementType.Fire));
            Assert.AreEqual("Agua", SlugTexto.NombreElemento(ElementType.Water));
            Assert.AreEqual("Electrico", SlugTexto.NombreElemento(ElementType.Electric));
            string desc = SlugTexto.TraducirTexto("Demoledora tipo Earth blindada");
            StringAssert.Contains("Tierra", desc);
            Assert.IsFalse(desc.Contains("Earth"));
        }

        [Test] public void LaBusquedaCubreNombreElementoYHabitat()
        {
            SlugData fuego = PrimeroDe(ElementType.Fire);
            Assert.IsNotNull(fuego);
            Assert.IsTrue(GameFlow.CoincideBusqueda(fuego, ""));
            Assert.IsTrue(GameFlow.CoincideBusqueda(fuego, "fuego"));
            Assert.IsTrue(GameFlow.CoincideBusqueda(fuego, fuego.slugName));
            if (!string.IsNullOrEmpty(fuego.habitat))
                Assert.IsTrue(GameFlow.CoincideBusqueda(fuego, fuego.habitat));
            Assert.IsFalse(GameFlow.CoincideBusqueda(fuego, "zzzz-no-existe"));
            Assert.IsFalse(GameFlow.CoincideBusqueda(null, ""));
        }

        [Test] public void LaRutaDistingueCompletadoDisponibleBloqueado()
        {
            Assert.AreEqual(GameFlow.EstadoNivel.Completado, GameFlow.EstadoDeNivel(1, 3));
            Assert.AreEqual(GameFlow.EstadoNivel.Disponible, GameFlow.EstadoDeNivel(3, 3));
            Assert.AreEqual(GameFlow.EstadoNivel.Bloqueado, GameFlow.EstadoDeNivel(4, 3));
            Assert.AreEqual(GameFlow.EstadoNivel.Disponible, GameFlow.EstadoDeNivel(1, 1));

            Assert.IsEmpty(GameFlow.CondicionDeNivel(1));
            StringAssert.Contains("nivel 3", GameFlow.CondicionDeNivel(4));
            Assert.IsFalse(GameFlow.CondicionDeNivel(4).Contains("???"));
        }
    }
}
