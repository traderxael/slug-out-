using NUnit.Framework;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    /// La campaña de 28 niveles se juega en las cinco cuevas: cinco arcos de seis (el ultimo
    /// queda en cuatro). Es una regla pura para poder probarla sin cargar la escena.
    public class CampanaRegionesTests
    {
        [Test]
        public void LaCampanaReparteLosVeintiochoNivelesEnCincoArcos()
        {
            Assert.AreEqual(0, GameFlow.RegionParaNivel(1));
            Assert.AreEqual(0, GameFlow.RegionParaNivel(6));
            Assert.AreEqual(1, GameFlow.RegionParaNivel(7));
            Assert.AreEqual(1, GameFlow.RegionParaNivel(12));
            Assert.AreEqual(2, GameFlow.RegionParaNivel(13));
            Assert.AreEqual(2, GameFlow.RegionParaNivel(18));
            Assert.AreEqual(3, GameFlow.RegionParaNivel(19));
            Assert.AreEqual(3, GameFlow.RegionParaNivel(24));
            Assert.AreEqual(4, GameFlow.RegionParaNivel(25));
            Assert.AreEqual(4, GameFlow.RegionParaNivel(28));
        }

        [Test]
        public void UnNivelFueraDeRangoCaeEnLaPrimeraOEnLaUltimaCueva()
        {
            Assert.AreEqual(0, GameFlow.RegionParaNivel(0));
            Assert.AreEqual(0, GameFlow.RegionParaNivel(-5));
            Assert.AreEqual(4, GameFlow.RegionParaNivel(99));
        }
    }
}
