using System.Collections.Generic;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// Mejoras de jugabilidad del 2026-10-25:
    ///  - el auto-aim deja de disparar a un enemigo que esta a la espalda,
    ///  - el HUD recibe el umbral de captura para poder marcar la linea del 35 %,
    ///  - el nivel pide refuerzos cuando la cuota de capturas ya no es alcanzable.
    public class MejoraCombateTests
    {
        private readonly List<GameObject> _creados = new List<GameObject>();

        private SlugActor Enemigo(Vector3 posicion)
        {
            GameObject go = new GameObject("objetivo_" + _creados.Count);
            go.transform.position = posicion;
            SlugActor a = go.AddComponent<SlugActor>();
            SlugData d = SlugRoster.Buscar("electroshock");
            if (d == null) d = SlugRoster.Todos != null && SlugRoster.Todos.Length > 0 ? SlugRoster.Todos[0] : null;
            a.Configure(d, false);
            _creados.Add(go);
            return a;
        }

        [TearDown]
        public void Limpiar()
        {
            foreach (GameObject go in _creados) if (go != null) Object.DestroyImmediate(go);
            _creados.Clear();
        }

        private static readonly float CosCono = Mathf.Cos(65f * Mathf.Deg2Rad);

        [Test]
        public void ElAutoAimNoApuntaAUnEnemigoQueEstaAtras()
        {
            SlugActor delante = Enemigo(new Vector3(0f, 0f, 6f));
            SlugActor atras = Enemigo(new Vector3(0f, 0f, -3f));   // mas cerca, pero a la espalda

            var todos = new List<SlugActor> { delante, atras };
            SlugActor elegido = PlayerShooter.ElegirObjetivo(
                todos, Vector3.zero, Vector3.forward, 30f, CosCono);

            Assert.AreSame(delante, elegido,
                "el auto-aim eligio a un enemigo que el jugador no esta mirando");

            var soloAtras = new List<SlugActor> { atras };
            Assert.IsNull(PlayerShooter.ElegirObjetivo(
                    soloAtras, Vector3.zero, Vector3.forward, 30f, CosCono),
                "con el unico enemigo a la espalda el tiro tiene que salir derecho, no girar el cuerpo");
        }

        [Test]
        public void DentroDelConoEligeAlMasCercanoYRespetaElAlcance()
        {
            SlugActor lejos = Enemigo(new Vector3(0f, 0f, 12f));
            SlugActor cerca = Enemigo(new Vector3(2f, 0f, 3f));

            var todos = new List<SlugActor> { lejos, cerca };
            Assert.AreSame(cerca, PlayerShooter.ElegirObjetivo(
                todos, Vector3.zero, Vector3.forward, 30f, CosCono), "dentro del cono gana el mas cercano");

            Assert.IsNull(PlayerShooter.ElegirObjetivo(
                    todos, Vector3.zero, Vector3.forward, 2f, CosCono),
                "fuera de alcance no hay objetivo, aunque este adelante");

            Assert.AreSame(cerca, PlayerShooter.ElegirObjetivo(
                    todos, Vector3.zero, new Vector3(0.6f, 0f, 1f), 30f, CosCono),
                "mirando en diagonal el enemigo de x=2 sigue dentro del cono");
        }

        [Test]
        public void LaLineaDeCapturaEsEl35PorCientoYSeExponeParaElHud()
        {
            SlugActor a = Enemigo(Vector3.zero);

            Assert.AreEqual(0.35f, a.UmbralCaptura, 0.0001f,
                "el HUD dibuja la marca con este valor: si cambia la regla, cambia la marca");

            // Se prueba a un 1 % de cada lado, no en el borde exacto: el HP de un enemigo real no
            // cae en un decimal redondo y el borde con floats no es una regla de juego.
            a.FijarVidaPrueba(a.MaxHp * 0.34f);
            Assert.IsTrue(a.Capturable, "el enemigo de prueba no es capturable");
            Assert.IsTrue(a.ListaParaCapturar, "por debajo del 35 % ya se captura");

            a.FijarVidaPrueba(a.MaxHp * 0.36f);
            Assert.IsFalse(a.ListaParaCapturar, "por encima del 35 % todavia no");
        }

        [Test]
        public void LosRefuerzosSoloSalenCuandoLaCuotaYaNoEsAlcanzable()
        {
            // infinito: no hay cuota que sostener
            Assert.IsFalse(SlugArenaGame.NecesitaRefuerzo(int.MaxValue, 50, 4, 0, 0));

            Assert.IsFalse(SlugArenaGame.NecesitaRefuerzo(6, 6, 0, 1, 1),
                "con la cuota cumplida no hace falta repetir el nivel");
            Assert.IsTrue(SlugArenaGame.NecesitaRefuerzo(6, 6, 0, 1, 0),
                "seis bajas sin capturas: sin refuerzo el nivel queda sin enemigos y sin final");

            Assert.IsFalse(SlugArenaGame.NecesitaRefuerzo(6, 3, 2, 2, 0),
                "tres por nacer mas dos vivos alcanzan para dos capturas");
            Assert.IsTrue(SlugArenaGame.NecesitaRefuerzo(6, 3, 0, 4, 0),
                "tres por nacer no alcanzan para cuatro capturas");
            Assert.IsTrue(SlugArenaGame.NecesitaRefuerzo(6, 0, 0, 8, 0),
                "una cuota mayor que el total tambien exige refuerzos");
        }
    }
}
