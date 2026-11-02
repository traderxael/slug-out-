using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SlugOut.World;
using SlugOut.Slugs;

namespace SlugOut.EditModeTests
{
    /// Invariantes del mundo de tres regiones. Son datos puros, asi que se pueden probar sin
    /// escena ni Play: es la capa donde se rompio el vinculo (las salvajes sembradas eran ids que
    /// no existian en el roster) y donde se puede volver a romper con el refactor de regiones.
    public class RegionesTests
    {
        [Test] public void HayCincoRegionesConTresMicrohabitatsCadaUna()
        {
            RegionDefinicion[] todas = Regiones.Todas;
            Assert.AreEqual(5, todas.Length, "el mundo anunciado es de cinco regiones");
            for (int i = 0; i < todas.Length; i++)
            {
                Assert.That(todas[i], Is.Not.Null);
                Assert.That(todas[i].nombre, Is.Not.Null.And.Not.Empty);
                Assert.AreEqual(3, todas[i].habitats.Length,
                    todas[i].nombre + ": cada region se anuncia con tres microhabitats");
            }
        }

        [Test] public void ElTunelDeCadaRegionLlevaAotraDistintaYValida()
        {
            RegionDefinicion[] todas = Regiones.Todas;
            for (int i = 0; i < todas.Length; i++)
            {
                int[] destinos = todas[i].destinosTunel;
                Assert.That(destinos, Is.Not.Null.And.Length.GreaterThan(0),
                    todas[i].nombre + ": sin boca de tunel esta cueva no se puede abandonar");
                for (int b = 0; b < destinos.Length; b++)
                {
                    Assert.That(destinos[b], Is.InRange(0, todas.Length - 1),
                        todas[i].nombre + ": la boca " + b + " apunta a una region que no existe");
                    Assert.AreNotEqual(i, destinos[b],
                        todas[i].nombre + ": la boca " + b + " lleva a la propia region");
                }
            }
        }

        /// Lo que importa de la topologia no es que cada boca sea valida, es que no haya cuevas a
        /// las que no se pueda llegar. Con una sola boca por region esto era facil de romper: un
        /// anillo 0->1->2 deja a la 2 sin camino de entrada.
        [Test] public void ElGrafoDeTunelesDejaAlcanzarTodasLasRegiones()
        {
            RegionDefinicion[] todas = Regiones.Todas;
            for (int origen = 0; origen < todas.Length; origen++)
            {
                var vistos = new HashSet<int> { origen };
                var pila = new Stack<int>();
                pila.Push(origen);
                while (pila.Count > 0)
                {
                    int actual = pila.Pop();
                    foreach (int destino in todas[actual].destinosTunel)
                        if (vistos.Add(destino)) pila.Push(destino);
                }
                Assert.AreEqual(todas.Length, vistos.Count,
                    "desde " + todas[origen].nombre + " solo se pueden visitar " + vistos.Count
                    + " de " + todas.Length + " cuevas");
            }
        }

        /// Cada region tiene una boca por vecina (asi el viaje sale por la boca que apunta de
        /// vuelta a donde uno entro) y los angulos no chocan ni entre bocas ni con los caminos.
        [Test] public void CadaRegionTieneUnaBocaPorVecinaYSinChocarConLasZonas()
        {
            RegionDefinicion[] todas = Regiones.Todas;
            for (int i = 0; i < todas.Length; i++)
            {
                RegionDefinicion d = todas[i];
                Assert.AreEqual(todas.Length - 1, d.destinosTunel.Length,
                    d.nombre + ": tiene que haber una boca por cada otra cueva");
                Assert.AreEqual(d.destinosTunel.Length, d.angulosTunel.Length,
                    d.nombre + ": cada boca necesita su angulo");
                for (int a = 0; a < d.angulosTunel.Length; a++)
                {
                    for (int b = a + 1; b < d.angulosTunel.Length; b++)
                        Assert.That(DistanciaAngulo(d.angulosTunel[a], d.angulosTunel[b]),
                            Is.GreaterThan(40f), d.nombre + ": dos bocas pegadas se leen como una");
                    for (int h = 0; h < d.habitats.Length; h++)
                        Assert.That(DistanciaAngulo(d.angulosTunel[a], d.habitats[h].angulo),
                            Is.GreaterThan(40f), d.nombre + ": la boca del tunel " + a
                            + " tapa el microhabitat " + d.habitats[h].nombre);
                }
                for (int v = 0; v < todas.Length; v++)
                {
                    if (v == i) continue;
                    bool hayBocaHacia = false;
                    foreach (int destino in d.destinosTunel) if (destino == v) hayBocaHacia = true;
                    Assert.That(hayBocaHacia, Is.True,
                        d.nombre + ": no tiene boca hacia " + todas[v].nombre);
                }
            }
        }

        /// El defecto real que encontro esta prueba: Fumarolas/Pozo/Cristales sembraban
        /// "fuego"/"agua"/"rayo", ids que no estaban en el roster ni tenian malla, y el vinculo
        /// los registraba como especies sin datos.
        [Test] public void TodaSalvajeSembradaTieneNombreDeRosterYMalla()
        {
            RegionDefinicion[] todas = Regiones.Todas;
            for (int i = 0; i < todas.Length; i++)
            {
                HabitatDef[] hs = todas[i].habitats;
                for (int h = 0; h < hs.Length; h++)
                {
                    Assert.That(hs[h].slugId, Is.Not.Null.And.Not.Empty,
                        todas[i].nombre + "/" + hs[h].nombre + ": sin id no hay babosa");
                    Assert.That(hs[h].legible, Is.Not.Null.And.Not.Empty);
                    Assert.That(hs[h].letra, Is.Not.Null.And.Not.Empty);
                    SlugData d = SlugRoster.Buscar(hs[h].slugId);
                    Assert.That(d, Is.Not.Null,
                        todas[i].nombre + ": la salvaje '" + hs[h].slugId + "' no existe en el roster");
                    Assert.That(d.slugId, Is.EqualTo(hs[h].slugId));
                    // "y malla" antes no se comprobaba: el id existia pero Resources/Slugs/<id>
                    // podia no estar, y la salvaje salia sin cuerpo.
                    Assert.That(Resources.Load<GameObject>("Slugs/" + hs[h].slugId), Is.Not.Null,
                        todas[i].nombre + ": la salvaje '" + hs[h].slugId
                        + "' no tiene modelo en Resources/Slugs");
                }
            }
        }

        [Test] public void TodasLasRegionesTraenCoberturasFueraDeLaPlazaYSinTaparZonas()
        {
            RegionDefinicion[] todas = Regiones.Todas;
            for (int r = 0; r < todas.Length; r++)
            {
                RegionDefinicion d = todas[r];
                Assert.That(d.coberturas, Is.Not.Null.And.Length.GreaterThanOrEqualTo(3),
                    d.nombre + ": el Escupidor puede salir aca y la cobertura es su contrapartida");
                for (int i = 0; i < d.coberturas.Length; i++)
                {
                    CoberturaDef c = d.coberturas[i];
                    Assert.That(c.radio, Is.GreaterThan(4.5f), d.nombre + ": la cobertura no puede nacer dentro de la plaza");
                    Assert.That(c.radio, Is.LessThan(18f), d.nombre + ": la cobertura tiene que estar en la arena, no en el borde");
                    Assert.That(c.tam.x, Is.GreaterThan(1f), d.nombre + ": un pilar sin ancho no tapa el cuerpo");
                    Assert.That(c.tam.y, Is.GreaterThan(2f), d.nombre + ": una cobertura baja no tapa ni corta el tiro");
                    for (int h = 0; h < d.habitats.Length; h++)
                        Assert.That(DistanciaAngulo(c.angulo, d.habitats[h].angulo), Is.GreaterThan(25f),
                            d.nombre + ": la cobertura " + i + " se mete en el habitat " + d.habitats[h].nombre);
                    for (int b = 0; b < d.angulosTunel.Length; b++)
                        Assert.That(DistanciaAngulo(c.angulo, d.angulosTunel[b]), Is.GreaterThan(25f),
                            d.nombre + ": la cobertura " + i + " tapa la boca " + b);
                    for (int j = i + 1; j < d.coberturas.Length; j++)
                        Assert.That(DistanciaAngulo(c.angulo, d.coberturas[j].angulo), Is.GreaterThan(25f),
                            d.nombre + ": dos coberturas pegadas no dejan corredor entre ellas");
                }
            }
        }

        [Test]
        public void LasQuinceZonasUsanQuinceEspeciesDistintas()
        {
            var vistas = new HashSet<string>();
            foreach (RegionDefinicion d in Regiones.Todas)
                foreach (HabitatDef h in d.habitats)
                    Assert.That(vistas.Add(h.slugId), Is.True,
                        d.nombre + ": la especie " + h.slugId + " ya estaba en otra cueva");
            Assert.AreEqual(15, vistas.Count);
        }

        [Test] public void LasTresZonasDeUnaRegionNoCompartenAnguloNiLetra()
        {
            RegionDefinicion[] todas = Regiones.Todas;
            for (int i = 0; i < todas.Length; i++)
            {
                HabitatDef[] hs = todas[i].habitats;
                for (int a = 0; a < hs.Length; a++)
                    for (int b = a + 1; b < hs.Length; b++)
                    {
                        Assert.AreNotEqual(hs[a].letra, hs[b].letra,
                            todas[i].nombre + ": dos zonas con la misma letra se leen igual en el mapa");
                        Assert.AreNotEqual(hs[a].tipo, hs[b].tipo,
                            todas[i].nombre + ": la misma silueta dos veces no son dos zonas");
                        Assert.That(Mathf.Abs(hs[a].angulo - hs[b].angulo), Is.GreaterThan(30f),
                            todas[i].nombre + ": " + hs[a].nombre + " y " + hs[b].nombre
                                + " estan demasiado juntas para leerse separadas");
                    }
            }
        }

        /// Ninguna silueta se repite en dos cuevas distintas: si una region reusara Cristales o
        /// CharcoEco, el mundo tendria menos formas que zonas. Con la quinta cueva son 15.
        [Test] public void LasQuinceZonasDelMundoSonQuinceSiluetasDistintas()
        {
            var vistas = new HashSet<TipoHabitat>();
            foreach (RegionDefinicion d in Regiones.Todas)
                foreach (HabitatDef h in d.habitats)
                    Assert.That(vistas.Add(h.tipo), Is.True,
                        d.nombre + ": la silueta " + h.tipo + " ya estaba en otra cueva");
            Assert.AreEqual(15, vistas.Count);
        }

        /// Guarda del refactor: la Cueva Seca tiene que seguir siendo la cueva verificada en Play
        /// y en el build. Si alguien mueve un angulo o un color, esta prueba lo dice en vez de
        /// descubrirlo una captura dos semanas despues.
        [Test] public void LaCuevaSecaConservaLosAngulosYLaPaletaMedidos()
        {
            RegionDefinicion c = Regiones.De(0);
            Assert.AreEqual("LA CUEVA SECA", c.nombre);
            Assert.AreEqual(12f, c.habitats[0].angulo, 0.01f);
            Assert.AreEqual(140f, c.habitats[1].angulo, 0.01f);
            Assert.AreEqual(252f, c.habitats[2].angulo, 0.01f);
            Assert.AreEqual("F", c.habitats[0].letra);
            Assert.AreEqual("A", c.habitats[1].letra);
            Assert.AreEqual("C", c.habitats[2].letra);
            Assert.AreEqual(200f, c.angulosTunel[0], 0.01f);
            Assert.AreEqual(1, c.destinosTunel[0],
                "la primera boca de La Cueva Seca tiene que seguir yendo a las Galerias: es la que "
                + "esta verificada caminando en el build");
            Assert.AreEqual(0f, c.anguloBase, 0.01f);
            // Estos son los valores que LookCuevaPixar aplicaba por encima de la region. Ahora
            // viven en la definicion, para que viajar no vuelva a pintar todo de azul.
            AssertColor(new Color(0.10f, 0.13f, 0.18f), c.roca, "roca");
            AssertColor(new Color(0.16f, 0.20f, 0.25f), c.camino, "camino");
            AssertColor(new Color(0.24f, 0.30f, 0.39f), c.ambiente, "ambiente");
            AssertColor(new Color(0.015f, 0.04f, 0.07f), c.niebla, "niebla");
            Assert.AreEqual(80f, c.nieblaFin, 0.01f);
            Assert.AreEqual(0.80f, c.intensidadLuz, 0.01f);
        }

        [Test] public void CadaRegionTieneAnguloBaseYRadioPropios()
        {
            RegionDefinicion[] t = Regiones.Todas;
            for (int i = 0; i < t.Length; i++)
            {
                Assert.That(t[i].radioHabitat, Is.InRange(18f, 22f),
                    t[i].nombre + ": un radio fuera de la arena deja habitats en el muro");
                for (int j = i + 1; j < t.Length; j++)
                    Assert.AreNotEqual(t[i].anguloBase, t[j].anguloBase,
                        t[i].nombre + " y " + t[j].nombre + ": misma orientacion de cueva");
            }
            Assert.AreEqual(0f, t[0].anguloBase, 0.01f);
            Assert.AreEqual(20f, t[0].radioHabitat, 0.01f);
        }

        [Test] public void LasTresRegionesSeDistinguenEnPaletayNiebla()
        {
            RegionDefinicion[] t = Regiones.Todas;
            for (int a = 0; a < t.Length; a++)
                for (int b = a + 1; b < t.Length; b++)
                {
                    Assert.That(Distancia(t[a].roca, t[b].roca), Is.GreaterThan(0.02f),
                        t[a].nombre + " y " + t[b].nombre + ": misma roca, se leen iguales");
                    Assert.That(Distancia(t[a].camino, t[b].camino), Is.GreaterThan(0.02f),
                        t[a].nombre + " y " + t[b].nombre + ": mismo camino");
                    Assert.That(Distancia(t[a].ambiente, t[b].ambiente), Is.GreaterThan(0.02f),
                        t[a].nombre + " y " + t[b].nombre + ": mismo ambiente");
                    Assert.AreNotEqual(t[a].nieblaFin, t[b].nieblaFin,
                        "la niebla es la senal mas barata de que cambio de zona");
                    Assert.AreNotEqual(t[a].nombre, t[b].nombre);
                }
        }

        /// Las dos cuevas se veian con el MISMO contorno de piso: el relieve era una funcion
        /// global. Cada region trae ahora su desfase y su amplitud, y esto fija que sean
        /// distintos sin mover el relieve ya verificado de La Cueva Seca.
        [Test] public void CadaRegionTraeSuPropioRelieve()
        {
            RegionDefinicion[] t = Regiones.Todas;
            Assert.That(t[0].relieveDesfase, Is.EqualTo(Vector2.zero),
                "La Cueva Seca tiene que conservar el relieve medido en Play y en el build");
            Assert.That(t[0].relieveAmplitud, Is.EqualTo(1f));
            for (int a = 0; a < t.Length; a++)
                for (int b = a + 1; b < t.Length; b++)
                {
                    Assert.That(t[b].relieveDesfase, Is.Not.EqualTo(t[a].relieveDesfase),
                        t[a].nombre + " y " + t[b].nombre
                            + ": sin desfase el contorno es el mismo calcado");
                    Assert.That(t[b].relieveAmplitud, Is.Not.EqualTo(t[a].relieveAmplitud),
                        t[a].nombre + " y " + t[b].nombre + ": misma amplitud de rompimiento");
                }
        }

        [Test] public void ElDesfaseCambiaElAnilloPeroNoLaPlaza()
        {
            Vector2 desfaseAntes = Terrain3D.DesfaseRegion;
            float amplitudAntes = Terrain3D.AmplitudRegion;
            try
            {
                Terrain3D.DesfaseRegion = Vector2.zero;
                Terrain3D.AmplitudRegion = 1f;
                float anillo = Terrain3D.Altura(12f, 7f);

                for (int i = 1; i < Regiones.Todas.Length; i++)
                {
                    RegionDefinicion d = Regiones.De(i);
                    Terrain3D.DesfaseRegion = d.relieveDesfase;
                    Terrain3D.AmplitudRegion = d.relieveAmplitud;

                    Assert.That(Terrain3D.Altura(0f, 0f), Is.EqualTo(0f).Within(0.0001f),
                        d.nombre + ": la plaza tiene que seguir plana, ahi aparecen el jugador y "
                        + "los enemigos");
                    Assert.That(Terrain3D.Altura(12f, 7f), Is.Not.EqualTo(anillo).Within(0.05f),
                        d.nombre + ": el anillo no se movio con el desfase, tendria la misma silueta");
                }
            }
            finally
            {
                Terrain3D.DesfaseRegion = desfaseAntes;
                Terrain3D.AmplitudRegion = amplitudAntes;
            }
        }

        /// Los caminos salen de la plaza hacia las zonas de la cueva que esta de pie. Antes los
        /// angulos estaban hardcodeados con los de La Cueva Seca, o sea que en Galerias del Eco el
        /// camino apuntaba 120 grados al lado de su microhabitat.
        [Test] public void CaminosApuntanALasZonasDeSuCueva()
        {
            foreach (RegionDefinicion d in Regiones.Todas)
            {
                float[] caminos = RegionCuevaSeca.AngulosCamino(d);
                Assert.AreEqual(d.habitats.Length + d.destinosTunel.Length, caminos.Length,
                    d.nombre + ": un camino por zona y uno por boca");
                for (int i = 0; i < d.habitats.Length; i++)
                    Assert.That(caminos[i],
                        Is.EqualTo(d.habitats[i].angulo + d.anguloBase).Within(0.01f),
                        d.nombre + ": el camino " + i + " no va a " + d.habitats[i].nombre);
                for (int i = 0; i < d.angulosTunel.Length; i++)
                    Assert.That(caminos[d.habitats.Length + i],
                        Is.EqualTo(d.angulosTunel[i] + d.anguloBase).Within(0.01f),
                        d.nombre + ": no hay camino hasta la boca " + i);
            }
        }

        /// El disparador del tunel cacheaba `FindFirstObjectByType<SlugActor>()`; con enemigos
        /// vivos eso devuelve un enemigo y el guard `!IsPlayer` dejaba la boca muerta para el resto
        /// de la partida (medido en Play: cache "Ascua (enemigo)" con el jugador sobre la boca).
        [Test] public void ElDisparadorEligeAlJugadorAunqueElEnemigoVayaPrimero()
        {
            var gobjetos = new List<GameObject>();
            try
            {
                var enemigos = new List<SlugActor>();
                for (int i = 0; i < 2; i++)
                {
                    var go = new GameObject("enemigo_" + i);
                    gobjetos.Add(go);
                    SlugActor s = go.AddComponent<SlugActor>();
                    s.Configure(null, false);
                    enemigos.Add(s);
                }
                var goJ = new GameObject("jugador");
                gobjetos.Add(goJ);
                SlugActor jugador = goJ.AddComponent<SlugActor>();
                jugador.Configure(null, true);

                Assert.That(TunelPasadizo.ElegirJugador(enemigos), Is.Null,
                    "sin jugador en la lista tiene que devolver null, no el primero");
                enemigos.Add(null);
                enemigos.Add(jugador);
                Assert.That(TunelPasadizo.ElegirJugador(enemigos), Is.SameAs(jugador),
                    "con enemigos primero, el disparador tiene que encontrar al jugador igual");
                Assert.That(TunelPasadizo.ElegirJugador(null), Is.Null);
            }
            finally
            {
                foreach (GameObject go in gobjetos) if (go != null) Object.DestroyImmediate(go);
            }
        }

        static float Distancia(Color a, Color b)
        {
            return Vector4.Distance(a, b);
        }

        /// Distancia angular corta (0..180), no la resta cruda: 350 y 10 estan a 20 grados.
        static float DistanciaAngulo(float a, float b)
        {
            float d = Mathf.Abs(a - b) % 360f;
            return d > 180f ? 360f - d : d;
        }

        static void AssertColor(Color esperado, Color real, string que)
        {
            Assert.That(Distancia(esperado, real), Is.LessThan(0.005f),
                "La Cueva Seca cambio " + que + " respecto de lo verificado en Play");
        }
    }
}
