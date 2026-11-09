using NUnit.Framework;
using SlugOut.Core;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// Accesibilidad: la asistencia y el cono amplio son reglas puras y tienen que poder
    /// probarse sin depender del input ni de la escena.
    public class AccesibilidadTests
    {
        private GameObject _go;

        [TearDown]
        public void Limpiar()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            GameFlow.FijarAsistenciaPrueba(false);
            GameFlow.FijarAimAmplioPrueba(false);
            HudTheme.EscalaTexto = 1f;
        }

        private SlugActor CrearJugador(string nombre)
        {
            SlugData d = SlugRoster.Buscar("electroshock");
            if (d == null && SlugRoster.Todos != null && SlugRoster.Todos.Length > 0)
                d = SlugRoster.Todos[0];
            _go = new GameObject(nombre);
            SlugActor a = _go.AddComponent<SlugActor>();
            a.Configure(d, true);
            return a;
        }

        [Test]
        public void LaAsistenciaReduceElDanoDelJugadorUnVeinticincoPorCiento()
        {
            GameFlow.FijarAsistenciaPrueba(false);
            SlugActor sin = CrearJugador("jugador_sin_asistencia");
            ElementType elemento = sin.Data != null ? sin.Data.element : ElementType.Fire;
            float hp0 = sin.Hp;
            sin.TakeDamage(10f, elemento);
            float danoSin = hp0 - sin.Hp;

            Object.DestroyImmediate(_go);
            _go = null;
            GameFlow.FijarAsistenciaPrueba(true);
            SlugActor con = CrearJugador("jugador_con_asistencia");
            float hp1 = con.Hp;
            con.TakeDamage(10f, elemento);
            float danoCon = hp1 - con.Hp;

            Assert.AreEqual(10f, danoSin, 0.01f, "sin asistencia el golpe debe entrar entero");
            Assert.AreEqual(7.5f, danoCon, 0.01f, "con asistencia debe entrar el 75 %");
        }

        [Test]
        public void ElAimAmplioAbreElConoAOchentaYCincoGrados()
        {
            Assert.AreEqual(65f, PlayerShooter.GradosCono(false, 65f), 0.001f);
            Assert.AreEqual(85f, PlayerShooter.GradosCono(true, 65f), 0.001f);

            _go = new GameObject("enemigo_aim");
            SlugData d = SlugRoster.Buscar("electroshock");
            if (d == null && SlugRoster.Todos != null && SlugRoster.Todos.Length > 0)
                d = SlugRoster.Todos[0];
            SlugActor enemigo = _go.AddComponent<SlugActor>();
            enemigo.Configure(d, false);
            _go.transform.position = Quaternion.Euler(0f, 80f, 0f) * Vector3.forward * 5f;
            var lista = new System.Collections.Generic.List<SlugActor> { enemigo };

            Assert.IsNull(PlayerShooter.ElegirObjetivo(lista, Vector3.zero, Vector3.forward, 30f,
                Mathf.Cos(65f * Mathf.Deg2Rad)), "a 80 grados el cono normal no lo ve");
            Assert.AreSame(enemigo, PlayerShooter.ElegirObjetivo(lista, Vector3.zero, Vector3.forward, 30f,
                Mathf.Cos(85f * Mathf.Deg2Rad)), "el cono amplio si lo ve");
        }

        [Test]
        public void LaEscalaDeTextoTienePisoYRedondea()
        {
            float previa = HudTheme.EscalaTexto;
            HudTheme.EscalaTexto = 1f;
            Assert.AreEqual(12, HudTheme.Escalar(12));
            HudTheme.EscalaTexto = 1.15f;
            Assert.AreEqual(14, HudTheme.Escalar(12));
            HudTheme.EscalaTexto = 0.1f;
            Assert.AreEqual(8, HudTheme.Escalar(10), "el piso evita textos ilegibles");
            HudTheme.EscalaTexto = previa;
        }
    }
}
