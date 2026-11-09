using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// La mordida cuerpo a cuerpo era instantanea: a rango de contacto no habia aviso y la
    /// rodada no tenia nada que esquivar. Ahora avisa 0,22 s con el pulso naranja del
    /// embestidor y el golpe se resuelve al final; salirse del alcance lo hace fallar.
    public class MordidaTelegrafiadaTests
    {
        private GameObject _enemigo;
        private GameObject _jugador;
        private EnemySlugAI _ai;
        private SlugActor _actorJugador;

        [SetUp]
        public void Crear()
        {
            SlugData data = SlugRoster.Buscar("electroshock");
            if (data == null && SlugRoster.Todos != null && SlugRoster.Todos.Length > 0)
                data = SlugRoster.Todos[0];

            _jugador = new GameObject("JugadorMordida");
            _jugador.transform.position = Vector3.zero;
            _actorJugador = _jugador.AddComponent<SlugActor>();
            _actorJugador.Configure(data, true);

            _enemigo = new GameObject("EnemigoMordida");
            _enemigo.transform.position = new Vector3(0f, 0f, 1.2f);   // dentro de attackRange (1.8)
            SlugActor actor = _enemigo.AddComponent<SlugActor>();
            actor.Configure(data, false);
            _ai = _enemigo.AddComponent<EnemySlugAI>();
            _ai.SetTarget(_jugador.transform);
        }

        [TearDown]
        public void Limpiar()
        {
            if (_enemigo != null) Object.DestroyImmediate(_enemigo);
            if (_jugador != null) Object.DestroyImmediate(_jugador);
        }

        [Test]
        public void LaMordidaAvisaAntesDePegarYElGolpeLlegaAlFinal()
        {
            float hpInicial = _actorJugador.Hp;
            _ai.EmpezarMordida();
            Assert.IsTrue(_ai.Mordiendo, "el aviso no arranco");

            for (int i = 0; i < 6; i++) _ai.PeleaMordida(0.02f);   // 0,12 s: mitad del aviso
            Assert.AreEqual(hpInicial, _actorJugador.Hp, 0.001f,
                "la mordida pego durante el aviso: no hay nada que esquivar");
            Assert.Greater(_ai.ProgresoAviso, 0.3f, "el aviso no se esta dibujando sobre el cuerpo");

            for (int i = 0; i < 10; i++) _ai.PeleaMordida(0.02f);  // 0,32 s: paso el aviso
            Assert.Less(_actorJugador.Hp, hpInicial, "el golpe no entro al terminar el aviso");
            Assert.AreEqual(1, _ai.Mordidas, "la mordida que llega no se conto");
            Assert.IsFalse(_ai.Mordiendo, "el aviso quedo encendido despues del golpe");
            Assert.AreEqual(0f, _ai.ProgresoAviso, 0.001f, "el aviso no se apago");
        }

        [Test]
        public void SalirDelAlcanceDuranteElAvisoHaceFallaLaMordida()
        {
            float hpInicial = _actorJugador.Hp;
            _ai.EmpezarMordida();
            _ai.PeleaMordida(0.1f);

            // La rodada recorre 3,6 m en 0,3 s: moverse 3,8 m en lo que queda del aviso es
            // exactamente lo que hace la rodada real, no un teletransporte de prueba.
            _jugador.transform.position = new Vector3(0f, 0f, 5f);

            for (int i = 0; i < 10; i++) _ai.PeleaMordida(0.02f);
            Assert.AreEqual(hpInicial, _actorJugador.Hp, 0.001f,
                "la mordida pego aunque el jugador estaba fuera de alcance");
            Assert.AreEqual(1, _ai.MordidasAlAire, "la mordida fallida no se conto");
            Assert.AreEqual(0, _ai.Mordidas, "una mordida al aire no puede contar como golpe");
            Assert.IsFalse(_ai.Mordiendo, "el aviso no se cerro al fallar");
        }
    }
}
