using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// La cobertura tiene que ser la misma ley para todos: corta tiros, frena cuerpos y
    /// tambien corta la mordida. Estos casos son los que se rompian al agregarla.
    public class CoberturaIaTests
    {
        private GameObject _jugador;
        private GameObject _enemigo;
        private GameObject _muros;
        private SlugActor _actorJugador;
        private EnemySlugAI _ai;

        [SetUp]
        public void Crear()
        {
            SlugData data = SlugRoster.Buscar("electroshock");
            if (data == null && SlugRoster.Todos != null && SlugRoster.Todos.Length > 0)
                data = SlugRoster.Todos[0];

            _jugador = new GameObject("JugadorCobertura");
            _jugador.transform.position = Vector3.zero;
            _actorJugador = _jugador.AddComponent<SlugActor>();
            _actorJugador.Configure(data, true);

            _enemigo = new GameObject("EnemigoCobertura");
            _enemigo.transform.position = new Vector3(0f, 0f, 1.2f);
            _enemigo.AddComponent<SlugActor>().Configure(data, false);
            _ai = _enemigo.AddComponent<EnemySlugAI>();
            _ai.SetTarget(_jugador.transform);
            _muros = new GameObject("MurosCoberturaTest");
        }

        [TearDown]
        public void Limpiar()
        {
            if (_muros != null) Object.DestroyImmediate(_muros);
            if (_enemigo != null) Object.DestroyImmediate(_enemigo);
            if (_jugador != null) Object.DestroyImmediate(_jugador);
        }

        [Test]
        public void LaMordidaNoEntraATravesDeUnaCobertura()
        {
            Muro(0.6f, 0.25f, 3f);
            float hp = _actorJugador.Hp;

            _ai.EmpezarMordida();
            for (int i = 0; i < 10; i++) _ai.PeleaMordida(0.04f);

            Assert.AreEqual(hp, _actorJugador.Hp, 0.001f,
                "la mordida atraveso el pilar: la cobertura solo estaba cortando tiros");
            Assert.IsFalse(_ai.Mordiendo, "el aviso quedo encendido despues del intento");
        }

        [Test]
        public void ElEscupidorSaleDeUnaCoberturaAlMoverse()
        {
            // Nace dentro del proxy (spawn/aterrizaje): el barrido no resuelve solape inicial.
            _jugador.transform.position = new Vector3(0f, 0f, 10f);
            _enemigo.transform.position = new Vector3(0f, 0f, 8f);
            _ai.SetArquetipo(EnemySlugAI.Arquetipo.Escupidor);
            MuroEn(8f, 2f, 2f);

            for (int i = 0; i < 8; i++) _ai.PeleaEscupidor(0.05f);

            Vector3 centro = _enemigo.transform.position + Vector3.up * 1f;
            Assert.IsFalse(ColisionJugador.SobreMuro(centro, 0.5f, 2f),
                "el Escupidor quedo dentro del pilar y el barrido no lo saco");
        }

        void Muro(float z, float grosor, float alto)
        {
            MurosCueva.Caja(_muros.transform, "muro_test",
                new Vector3(0f, alto * 0.5f, z), Quaternion.identity,
                new Vector3(4f, alto, grosor), 8);
            Physics.SyncTransforms();
        }

        void MuroEn(float z, float ancho, float alto)
        {
            MurosCueva.Caja(_muros.transform, "muro_test",
                new Vector3(0f, alto * 0.5f, z), Quaternion.identity,
                new Vector3(ancho, alto, ancho), 8);
            Physics.SyncTransforms();
        }
    }
}
