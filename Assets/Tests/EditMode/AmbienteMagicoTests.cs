using NUnit.Framework;
using UnityEngine;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    /// Cada cueva configura su propio tipo de ambientación mágica y crea sus partículas.
    /// Corre en EditMode: Aplicar inicializa perezosamente, así no hace falta Play.
    public class AmbienteMagicoTests
    {
        private GameObject _go;
        private AmbienteMagico _amb;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("test_ambiente");
            _amb = _go.AddComponent<AmbienteMagico>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void CadaRegionMapeaASuTipoDeAmbiente()
        {
            _amb.Aplicar(Regiones.De(0), null, 0);
            Assert.AreEqual(AmbienteMagico.TipoAmbiente.PolvoCalido, _amb.Tipo, "Cueva Seca");

            _amb.Aplicar(Regiones.De(1), null, 1);
            Assert.AreEqual(AmbienteMagico.TipoAmbiente.EsporasEco, _amb.Tipo, "Galerias del Eco");

            _amb.Aplicar(Regiones.De(2), null, 2);
            Assert.AreEqual(AmbienteMagico.TipoAmbiente.PolenVerde, _amb.Tipo, "Vena Verde");

            _amb.Aplicar(Regiones.De(3), null, 3);
            Assert.AreEqual(AmbienteMagico.TipoAmbiente.Ceniza, _amb.Tipo, "Cañon de Ceniza");

            _amb.Aplicar(Regiones.De(4), null, 4);
            Assert.AreEqual(AmbienteMagico.TipoAmbiente.Petalos, _amb.Tipo, "Templo Olvidado");
        }

        [Test]
        public void AplicarCreaMotasYBrillos()
        {
            _amb.Aplicar(Regiones.De(0), null, 0);
            Assert.Greater(_amb.MotasActivas, 0, "tiene que haber motas");
            Assert.Greater(_amb.BrillosActivos, 0, "tiene que haber brillos");
        }

        [Test]
        public void AplicarEsIdempotenteYNoDuplicaParticulas()
        {
            _amb.Aplicar(Regiones.De(2), null, 2);
            int motas = _amb.MotasActivas;
            int brillos = _amb.BrillosActivos;
            _amb.Aplicar(Regiones.De(2), null, 2);
            Assert.AreEqual(motas, _amb.MotasActivas, "no duplicó motas");
            Assert.AreEqual(brillos, _amb.BrillosActivos, "no duplicó brillos");
            Assert.AreEqual(AmbienteMagico.TipoAmbiente.PolenVerde, _amb.Tipo);
        }

        [Test]
        public void AplicarConDefinicionNulaNoRompe()
        {
            Assert.DoesNotThrow(() => _amb.Aplicar(null, null));
        }

        [Test]
        public void LasCuatroRegionesTienenNombreYPaletaPropios()
        {
            // El ambiente cambia de color por región: la niebla de cada una es distinta.
            Color niebla0 = Regiones.De(0).niebla;
            Color niebla3 = Regiones.De(3).niebla;
            Assert.AreNotEqual(niebla0, niebla3, "Cueva Seca y Cañón de Ceniza no comparten niebla");
            for (int i = 0; i < Regiones.Todas.Length; i++)
                Assert.IsNotEmpty(Regiones.De(i).nombre, "region " + i + " tiene nombre");
        }
    }
}
