using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;

namespace SlugOut.EditModeTests
{
    /// Invariante entre los dos modos: las babosas que se ligaron en la campana son las que
    /// pelean en el Modo Duelo, con los mismos datos. Se prueba contra la version pura de
    /// Torneo.EquipoJugador, que es la que recibe el equipo guardado ya leido.
    public class EquipoDelTorneoTests
    {
        static SlugData[] Roster { get { return SlugRoster.Todos; } }

        [Test] public void JuegaConElEquipoArmadoEnEseOrden()
        {
            SlugData[] roster = Roster;
            SlugData[] equipo = Torneo.EquipoJugador(roster,
                new string[] { "infierno", "abisal", "abismo" }, null);

            Assert.AreEqual(3, equipo.Length);
            AssertIds(equipo, "infierno", "abisal", "abismo");
        }

        /// El defecto que esta prueba impide repetir: las salvajes del mundo sembraban ids que no
        /// estaban en el roster. Un id fantasma no puede colar un null en el equipo del torneo,
        /// porque un Combatiente con datos null se arma igual y despues no dispara.
        [Test] public void UnIdFantasmaNoDejaHuecosVacios()
        {
            SlugData[] roster = Roster;
            SlugData[] equipo = Torneo.EquipoJugador(roster,
                new string[] { "fuego", "agua", "rayo" }, null);

            Assert.AreEqual(3, equipo.Length);
            for (int i = 0; i < equipo.Length; i++)
                Assert.That(equipo[i], Is.Not.Null, "slot " + i + " quedo vacio");
            Verificacion.SinRepetidos(equipo);
        }

        [Test] public void CompletaConElRosterSinRepetirLoYaElegido()
        {
            SlugData[] roster = Roster;
            SlugData[] equipo = Torneo.EquipoJugador(roster, new string[] { "infierno" }, null);

            Assert.AreEqual(3, equipo.Length);
            Assert.AreEqual("infierno", equipo[0].slugId);
            for (int i = 0; i < equipo.Length; i++)
                Assert.That(equipo[i], Is.Not.Null, "slot " + i);
            Verificacion.SinRepetidos(equipo);
        }

        [Test] public void SinEquipoUsaLaBabosaElegida()
        {
            SlugData roster0 = Roster[0];
            SlugData[] equipo = Torneo.EquipoJugador(Roster, new string[0], roster0);
            Assert.AreEqual(3, equipo.Length);
            Assert.AreEqual(roster0.slugId, equipo[0].slugId);
            Verificacion.SinRepetidos(equipo);
        }

        [Test] public void RosterVacioNoDevuelveNullsSueltos()
        {
            // Sin roster no hay de donde completar: se espera que devuelva tres slots, no un
            // array mas corto (el llamador indexa hasta 3 sin revisar).
            SlugData[] equipo = Torneo.EquipoJugador(new SlugData[0],
                new string[] { "infierno", "abisal", "abismo" }, null);
            Assert.AreEqual(3, equipo.Length);
            Assert.AreEqual("infierno", equipo[0].slugId);
            Assert.AreEqual("abisal", equipo[1].slugId);
        }

        [Test] public void TodosLosIdsDelRosterResuelven()
        {
            SlugData[] roster = Roster;
            Assert.That(roster.Length, Is.GreaterThanOrEqualTo(64),
                "el mazo announced es de 64 babosas");
            for (int i = 0; i < roster.Length; i++)
            {
                Assert.That(roster[i], Is.Not.Null, "slot " + i + " del roster vacio");
                SlugData otra = SlugRoster.Buscar(roster[i].slugId);
                Assert.That(otra, Is.SameAs(roster[i]),
                    roster[i].slugId + ": Buscar no devuelve el mismo dato");
            }
        }

        static void AssertIds(SlugData[] equipo, string a, string b, string c)
        {
            Assert.AreEqual(a, equipo[0] != null ? equipo[0].slugId : "<null>");
            Assert.AreEqual(b, equipo[1] != null ? equipo[1].slugId : "<null>");
            Assert.AreEqual(c, equipo[2] != null ? equipo[2].slugId : "<null>");
        }
    }

    /// Helper propio: se llama distinto de NUnit.Framework.Assert a proposito, porque una clase
    /// `Assert` en este namespace taparia la de NUnit y el archivo no compilaria.
    static class Verificacion
    {
        /// Los tres slots del torneo tienen que ser babosas distintas: con repetidas el jugador
        /// ve "3 babosas" pero en realidad tiene dos, y el rival puede tumbarle el equipo entero
        /// con un solo contragolpe elemental.
        public static void SinRepetidos(SlugData[] equipo)
        {
            for (int i = 0; i < equipo.Length; i++)
                for (int j = i + 1; j < equipo.Length; j++)
                    if (equipo[i] != null && equipo[j] != null)
                        Assert.AreNotEqual(equipo[i].slugId, equipo[j].slugId,
                            "el equipo repite " + equipo[i].slugId);
        }
    }
}
