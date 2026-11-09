using System.Collections.Generic;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// Sin separacion, cuatro perseguidores eligen la misma linea y ocupan el mismo punto:
    /// el jugador ve una sola babosa y no puede leer cuantas lo estan mordiendo. La regla es
    /// pura, por distancia y sin azar, para que dos cuerpos coincidentes se abran en el mismo cuadro.
    public class SeparacionEnemigosTests
    {
        private readonly List<GameObject> _creados = new List<GameObject>();

        private SlugActor Crear(string nombre, Vector3 pos, bool jugador = false)
        {
            GameObject go = new GameObject(nombre);
            go.transform.position = pos;
            SlugActor a = go.AddComponent<SlugActor>();
            SlugData d = SlugRoster.Buscar("electroshock");
            if (d == null && SlugRoster.Todos != null && SlugRoster.Todos.Length > 0)
                d = SlugRoster.Todos[0];
            a.Configure(d, jugador);
            _creados.Add(go);
            return a;
        }

        [TearDown]
        public void Limpiar()
        {
            foreach (GameObject go in _creados) if (go != null) Object.DestroyImmediate(go);
            _creados.Clear();
        }

        [Test]
        public void DosEnemigosEnElMismoPuntoSeEmpujanALadosOpuestos()
        {
            SlugActor a = Crear("A", Vector3.zero);
            SlugActor b = Crear("B", Vector3.zero);
            var lista = new List<SlugActor> { a, b };

            Vector3 pa = EnemySlugAI.Separacion(Vector3.zero, lista, a, 1.2f);
            Vector3 pb = EnemySlugAI.Separacion(Vector3.zero, lista, b, 1.2f);

            Assert.Greater(pa.x, 0.5f, "el primero no se abre hacia un lado");
            Assert.Less(pb.x, -0.5f, "el segundo no se abre hacia el lado contrario");
            Assert.AreEqual(0f, pa.z, 0.0001f, "la separacion no debe tener componente vertical ni Z rara");
        }

        [Test]
        public void ElEmpujeCreceAlAcercarseYSeApagaFueraDelRadio()
        {
            SlugActor a = Crear("A", Vector3.zero);
            SlugActor b = Crear("B", new Vector3(1f, 0f, 0f));
            var lista = new List<SlugActor> { a, b };

            float lejos = EnemySlugAI.Separacion(Vector3.zero, lista, a, 1.2f).magnitude;
            b.transform.position = new Vector3(0.4f, 0f, 0f);
            float cerca = EnemySlugAI.Separacion(Vector3.zero, lista, a, 1.2f).magnitude;
            Assert.Greater(cerca, lejos, "el empuje no crece al acercarse");

            b.transform.position = new Vector3(2f, 0f, 0f);
            Assert.AreEqual(0f, EnemySlugAI.Separacion(Vector3.zero, lista, a, 1.2f).magnitude, 0.0001f,
                "fuera del radio la separacion tiene que ser cero");
        }

        [Test]
        public void ElJugadorNoEmpujaNiEsEmpujado()
        {
            SlugActor a = Crear("A", Vector3.zero);
            SlugActor jugador = Crear("Jugador", new Vector3(0f, 0f, 0.3f), true);
            var lista = new List<SlugActor> { a, jugador };

            Vector3 p = EnemySlugAI.Separacion(Vector3.zero, lista, a, 1.2f);
            Assert.AreEqual(0f, p.magnitude, 0.0001f,
                "el jugador no es un enemigo: no puede empujar a la IA ni recibir este empuje");
        }
    }
}
