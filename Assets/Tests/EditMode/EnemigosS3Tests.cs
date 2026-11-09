using System.Collections.Generic;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// S3 (enemigos nuevos y elites): los tres arquetipos nuevos tienen que existir de verdad
    /// dentro de la campana (si no, son codigo muerto), el auto-aim no puede apuntar a lo que
    /// esta bajo tierra (invulnerable y escondido) y el si filo del tejedor no puede contar
    /// como captura (si contara, el tejedor "ganaria" niveles sin que el jugador capture).
    public class EnemigosS3Tests
    {
        private readonly List<GameObject> _creados = new List<GameObject>();

        [TearDown]
        public void Limpiar()
        {
            foreach (GameObject go in _creados) if (go != null) Object.DestroyImmediate(go);
            _creados.Clear();
        }

        private GameObject Nuevo(string nombre)
        {
            GameObject go = new GameObject(nombre);
            _creados.Add(go);
            return go;
        }

        private static SlugData Datos()
        {
            SlugData d = SlugRoster.Buscar("electroshock");
            if (d == null && SlugRoster.Todos != null && SlugRoster.Todos.Length > 0) d = SlugRoster.Todos[0];
            return d;
        }

        [Test]
        public void LaCampanaReparteLosTresRolesNuevos()
        {
            // Antes del nivel 8 la cueva es la de siempre: no entran roles nuevos.
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(SlugData.RolEnemigo.Normal, SlugArenaGame.RolDeCampana(7, i),
                    "antes del nivel 8 no hay roles nuevos");

            int voladores = 0, tejedores = 0, excavadores = 0, normales = 0;
            for (int i = 0; i < 40; i++)
            {
                SlugData.RolEnemigo rol = SlugArenaGame.RolDeCampana(20, i);
                if (rol == SlugData.RolEnemigo.Volador) voladores++;
                else if (rol == SlugData.RolEnemigo.Tejedor) tejedores++;
                else if (rol == SlugData.RolEnemigo.Excavador) excavadores++;
                else if (rol == SlugData.RolEnemigo.Normal) normales++;
                else Assert.Fail("un rol que no existe: " + rol);
            }
            Assert.Greater(normales, 0, "todas las apariciones tienen rol: no queda cueva de siempre");
            Assert.Greater(voladores, 0, "nunca sale un volador: el arquetipo queda como codigo muerto");
            Assert.Greater(tejedores, 0, "nunca sale un tejedor: el arquetipo queda como codigo muerto");
            Assert.Greater(excavadores, 0, "nunca sale un excavador: el arquetipo queda como codigo muerto");

            // Ni tan seguido ni tan raro: si se repiten mucho, el nivel deja de ser jugable;
            // si casi no salen, el trabajo de los arquetipos no se nota.
            Assert.LessOrEqual(voladores, 12, "demasiados voladores: un nivel normal se vuelve imposible");
            Assert.LessOrEqual(tejedores, 8, "demasiados tejedores: la pantalla se llena de sifilos");
            Assert.LessOrEqual(excavadores, 8, "demasiados excavadores: no queda nadie a quien disparar");

            // Cada rol entra en su nivel: el volador abre la tanda, el tejedor y el excavador
            // llegan mas tarde (la cueva se complica poco a poco).
            for (int i = 0; i < 40; i++)
            {
                Assert.AreNotEqual(SlugData.RolEnemigo.Excavador, SlugArenaGame.RolDeCampana(12, i),
                    "el excavador entra antes de su nivel");
                Assert.AreNotEqual(SlugData.RolEnemigo.Tejedor, SlugArenaGame.RolDeCampana(8, i),
                    "el tejedor entra antes de su nivel");
            }

            // El reparto es el mismo en dos niveles distintos: la campana es reproducible.
            for (int i = 0; i < 40; i++)
                Assert.AreEqual(SlugArenaGame.RolDeCampana(20, i), SlugArenaGame.RolDeCampana(20, i),
                    "el reparto de roles tiene que ser determinista");
        }

        [Test]
        public void ElAutoAimNoApuntaAUnEnemigoEnterrado()
        {
            GameObject go = Nuevo("enterrado");
            go.transform.position = new Vector3(0f, 0f, 4f);
            SlugActor a = go.AddComponent<SlugActor>();
            a.Configure(Datos(), false);
            a.SetEnterrado(true);

            var todos = new List<SlugActor> { a };
            float cosCono = Mathf.Cos(65f * Mathf.Deg2Rad);
            Assert.IsNull(PlayerShooter.ElegirObjetivo(todos, Vector3.zero, Vector3.forward, 30f, cosCono),
                "el auto-aim apunto a un enemigo enterrado: es invulnerable y esta escondido");

            a.SetEnterrado(false);
            Assert.AreSame(a, PlayerShooter.ElegirObjetivo(todos, Vector3.zero, Vector3.forward, 30f, cosCono),
                "al salir a la superficie vuelve a ser un objetivo normal");
        }

        [Test]
        public void ElSifiloNoSePuedeCapturar()
        {
            GameObject go = Nuevo("sifilo");
            SlugActor a = go.AddComponent<SlugActor>();
            SlugArenaGame.ConfigurarSifilo(a);

            Assert.IsFalse(a.Capturable,
                "el si filo es capturable: contaria para la cuota de capturas del nivel");
            Assert.IsFalse(a.ListaParaCapturar,
                "a vida completa el si filo ya se podria capturar");
        }

        [Test]
        public void LaEliteMuestraSuBadge()
        {
            GameObject go = Nuevo("elite");
            SlugActor a = go.AddComponent<SlugActor>();
            a.Configure(Datos(), false);
            EliteEstado elite = go.AddComponent<EliteEstado>();
            elite.modificadores = new EliteModificador[]
            {
                new EliteModificador { id = "elite_vigor", nombre = "Vigor", hpMultiplicador = 2f, badgeColor = Color.green },
            };
            elite.Aplicar(a, null);

            MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            Assert.Greater(renderers.Length, 0,
                "la elite no tiene badge visible: el jugador no puede saber a que se enfrenta");
        }
    }
}
