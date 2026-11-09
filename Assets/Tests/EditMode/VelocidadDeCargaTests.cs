using NUnit.Framework;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    /// Mantener ESPACIO no costaba nada: cargar al maximo era la jugada dominante. Ahora la
    /// carga se paga con velocidad (0,7x) y el paso cuidado se aplica encima. La formula es
    /// pura; el cableado del input se prueba en el build (las teclas simuladas no llegan sin foco).
    public class VelocidadDeCargaTests
    {
        [Test]
        public void CargarCuestaVelocidadSobreLaCarreraYElPasoCuidado()
        {
            Assert.AreEqual(6f, PlayerController.VelocidadDeMarcha(6f, 1.8f, false, false, 0.7f), 0.001f,
                "sin cargar la carrera no puede cambiar");
            Assert.AreEqual(4.2f, PlayerController.VelocidadDeMarcha(6f, 1.8f, false, true, 0.7f), 0.001f,
                "cargar tiene que costar un 30 % de velocidad");
            Assert.AreEqual(1.8f, PlayerController.VelocidadDeMarcha(6f, 1.8f, true, false, 0.7f), 0.001f,
                "el paso cuidado sigue siendo 1,8 sin cargar");
            Assert.AreEqual(1.26f, PlayerController.VelocidadDeMarcha(6f, 1.8f, true, true, 0.7f), 0.001f,
                "cargar y paso cuidado se componen");
        }

        [Test]
        public void ElFactorNoPuedeCongelarAlJugadorNiAcelerarloDeMas()
        {
            Assert.AreEqual(0.6f, PlayerController.VelocidadDeMarcha(6f, 1.8f, false, true, 0f), 0.001f,
                "un factor en cero no puede dejar al jugador clavado");
            Assert.AreEqual(6f, PlayerController.VelocidadDeMarcha(6f, 1.8f, false, true, 3f), 0.001f,
                "un factor mayor que uno no puede regalar velocidad");
        }
    }
}
