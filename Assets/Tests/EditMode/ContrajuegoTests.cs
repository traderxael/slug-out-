using System.Collections.Generic;
using NUnit.Framework;
using SlugOut.Core;
using SlugOut.Slugs;

namespace SlugOut.EditModeTests
{
    /// El pedido dice que cada habilidad tiene "un contrarrestador y una limitacion". Hasta hoy
    /// el contrarrestador no estaba en ningun dato: el torneo rotulaba la `limitacion` de la
    /// babosa como "EN EL CAMPO" y eso era todo. Estas pruebas fijan la tabla nueva y, sobre
    /// todo, que ninguna babosa del roster quede con una habilidad sin contrajuego cargado.
    public class ContrajuegoTests
    {
        [Test] public void TodaHabilidadTieneFilaYTexto()
        {
            Assert.That(Contrajuego.Filas, Is.EqualTo(Contrajuego.Todas.Length),
                "la tabla y la lista de habilidades se desincronizaron");
            foreach (SlugAbilityType h in Contrajuego.Todas)
            {
                Assert.That(Contrajuego.Tiene(h), Is.True, "falta la fila de " + h);
                Assert.That(Contrajuego.De(h).texto, Is.Not.Null.And.Not.Empty, "" + h);
            }
        }

        [Test] public void NingunContrajuegoSeContrarrestaASiMismo()
        {
            foreach (SlugAbilityType h in Contrajuego.Todas)
            {
                ContrajuegoDato c = Contrajuego.De(h);
                Assert.That(c.contrarresta, Is.Not.EqualTo(h), "" + h + " no puede cortarse a si misma");
                Assert.That(Contrajuego.Tiene(c.contrarresta), Is.True,
                    "" + h + " dice que lo corta " + c.contrarresta + ", que no es una habilidad del juego");
            }
        }

        [Test] public void TodaHabilidadDelRosterEstaCubierta()
        {
            SlugData[] roster = SlugRoster.Todos;
            Assert.That(roster, Is.Not.Null.And.Not.Empty);
            var vistas = new HashSet<SlugAbilityType>();
            foreach (SlugData s in roster)
            {
                if (s == null) continue;
                vistas.Add(s.abilityType);
                Assert.That(Contrajuego.Tiene(s.abilityType), Is.True,
                    s.slugId + " trae " + s.abilityType + " y esa habilidad no tiene contrajuego");
            }
            Assert.That(vistas.Count, Is.GreaterThan(1),
                "el roster usa una sola habilidad: la tabla no se esta ejercitando");
        }

        [Test] public void ElContrajuegoNoEsLaMismaLineaQueLaLimitacion()
        {
            foreach (SlugData s in SlugRoster.Todos)
            {
                if (s == null || string.IsNullOrEmpty(s.limitacion)) continue;
                string c = Contrajuego.De(s.abilityType).texto;
                Assert.That(c, Is.Not.EqualTo(s.limitacion),
                    s.slugId + ": el contrajuego vuelve a ser la limitacion");
                Assert.That(c.Length, Is.GreaterThan(12),
                    s.slugId + ": el texto del contrajuego esta vacio o cortado");
            }
        }
    }
}
