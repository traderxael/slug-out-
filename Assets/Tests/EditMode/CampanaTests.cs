using NUnit.Framework;
using UnityEngine;
using SlugOut.World;
using SlugOut.Slugs;
using SlugOut.Core;

namespace SlugOut.EditModeTests
{
    /// Barrido de los 28 niveles de campana, de punta a punta y de una sola vez. Antes no existia:
    /// la textura que no cargaba en el nivel 22 se encontro por casualidad paseando por ahi, y
    /// cada nivel tiene su propia fila de dificultad, su propio tema y su propia imagen. Este
    /// archivo mira las 28 filas contra las otras dos tablas de 28 (historia y texturas).
    public class CampanaTests
    {
        const int Niveles = 28;

        [Test] public void LasTresTablasTienenElMismoLargo()
        {
            Assert.That(NivelConfig.TotalNiveles, Is.EqualTo(Niveles),
                "la tabla de dificultad cambio de largo y la campana anunciada es de 28");
            Assert.That(SlugStory.TotalCampana, Is.EqualTo(Niveles));
            Assert.That(SlugStory.Campana().Length, Is.EqualTo(NivelConfig.TotalNiveles),
                "historia y dificultad se desincronizan: el nivel N mostraria el texto de otro");
        }

        [Test] public void CadaNivelCargaSuTexturaDeHistoria()
        {
            for (int n = 1; n <= Niveles; n++)
            {
                Texture2D t = Resources.Load<Texture2D>("Story/campana/" + n);
                Assert.That(t, Is.Not.Null,
                    "el nivel " + n + " no tiene textura cargable (meta truncado, nombre o carpeta)");
                Assert.That(t.width, Is.GreaterThanOrEqualTo(200), "nivel " + n);
                Assert.That(t.height, Is.GreaterThanOrEqualTo(120), "nivel " + n);
            }
        }

        [Test] public void NingunNivelRepiteConfiguracion()
        {
            NivelConfig[] f = NivelConfig.Campana();
            for (int i = 0; i < f.Length; i++)
                for (int j = i + 1; j < f.Length; j++)
                    Assert.That(Clave(f[i]), Is.Not.EqualTo(Clave(f[j])),
                        "los niveles " + (i + 1) + " y " + (j + 1) + " son la misma pelea");
        }

        [Test] public void LaCurvaSoloSubeYElRitmoSoloAprieta()
        {
            NivelConfig[] f = NivelConfig.Campana();
            for (int i = 1; i < f.Length; i++)
            {
                Assert.That(f[i].total, Is.GreaterThanOrEqualTo(f[i - 1].total),
                    "nivel " + (i + 1) + ": menos enemigos que en el anterior");
                Assert.That(f[i].dano, Is.GreaterThanOrEqualTo(f[i - 1].dano),
                    "nivel " + (i + 1) + ": menos dano que en el anterior");
                Assert.That(f[i].velocidad, Is.GreaterThanOrEqualTo(f[i - 1].velocidad),
                    "nivel " + (i + 1) + ": mas lento que el anterior");
                Assert.That(f[i].intervalo, Is.LessThanOrEqualTo(f[i - 1].intervalo),
                    "nivel " + (i + 1) + ": respira mas que el anterior");
                Assert.That(f[i].capturas, Is.GreaterThanOrEqualTo(f[i - 1].capturas),
                    "nivel " + (i + 1) + ": exige menos capturas que el anterior");
                Assert.That(f[i].maxVivos, Is.GreaterThanOrEqualTo(f[i - 1].maxVivos),
                    "nivel " + (i + 1) + ": menos presion simultanea");
                Assert.That(f[i].maxVivos, Is.LessThanOrEqualTo(f[i].total),
                    "nivel " + (i + 1) + ": vivos simultaneos mayores que el total");
            }
        }

        /// El tema del nivel tiene que ser el elemento del fragmento que se lee en ese mismo
        /// nivel, y tiene que haber babosas de ese elemento en el roster: si no, el reordenado
        /// por tema no pone nada adelante y la promesa del texto no se ve en pantalla.
        [Test] public void ElTemaDelNivelCoincideConSuFragmentoYExisteEnElRoster()
        {
            SlugData[] roster = SlugRoster.Todos;
            Assert.That(roster, Is.Not.Null.And.Not.Empty);
            for (int n = 1; n <= Niveles; n++)
            {
                ElementType esperado = SlugStory.ElementoDe(SlugStory.DeCampana(n).elemento);
                Assert.That(NivelConfig.Campana(n).tema, Is.EqualTo(esperado),
                    "nivel " + n + ": el anillo del banner (" + SlugStory.DeCampana(n).elemento
                    + ") no coincide con el tema de los enemigos");

                SlugData[] conTema = System.Array.FindAll(roster,
                    delegate (SlugData s) { return s != null && s.element == esperado; });
                Assert.That(conTema.Length, Is.GreaterThan(0),
                    "nivel " + n + ": no hay ninguna babosa " + esperado + " en el roster");
            }
        }

        [Test] public void ElObjetivoDelHudDiceLosNumerosRealesDelNivel()
        {
            for (int n = 1; n <= Niveles; n++)
            {
                NivelConfig c = NivelConfig.Campana(n);
                string o = c.Objetivo;
                Assert.That(o, Does.Contain(c.total.ToString()), "nivel " + n + ": '" + o + "'");
                Assert.That(o, Does.Contain(c.capturas.ToString()), "nivel " + n + ": '" + o + "'");
                bool invertido = n >= NivelConfig.AnilloAlRevesDesde;
                Assert.That(o.Contains("anillo al reves"), Is.EqualTo(invertido),
                    "nivel " + n + ": el cartel del anillo no sigue la regla (" + o + ")");
            }
        }

        [Test] public void LasRocasYElAnilloInvertidoEntranCuandoDiceLaTabla()
        {
            Assert.That(NivelConfig.Campana(NivelConfig.RocasDesde - 1).rocas, Is.False);
            Assert.That(NivelConfig.Campana(NivelConfig.RocasDesde).rocas, Is.True);
            Assert.That(NivelConfig.Campana(NivelConfig.AnilloAlRevesDesde - 1).anilloInvertido,
                Is.False, "el anillo se da vuelta antes de lo que se anuncia");
            Assert.That(NivelConfig.Campana(NivelConfig.AnilloAlRevesDesde).anilloInvertido, Is.True);
            for (int n = 1; n <= Niveles; n++)
            {
                Assert.That(NivelConfig.Campana(n).rocas, Is.EqualTo(n >= NivelConfig.RocasDesde),
                    "nivel " + n + " con rocas inconsistentes");
            }
        }

        [Test] public void NingunNivelFueraDeRangoRompeLaTabla()
        {
            NivelConfig primero = NivelConfig.Campana(1);
            Assert.That(NivelConfig.Campana(0).total, Is.EqualTo(primero.total));
            Assert.That(NivelConfig.Campana(-5).total, Is.EqualTo(primero.total));
            Assert.That(NivelConfig.Campana(Niveles + 3).total,
                Is.EqualTo(NivelConfig.Campana(Niveles).total),
                "pedir el nivel 31 en campana tiene que quedar en el ultimo, no reventar");
            Assert.That(SlugStory.DeCampana(0).nivel, Is.EqualTo(1));
            Assert.That(SlugStory.DeCampana(Niveles + 9).nivel, Is.EqualTo(Niveles));
        }

        static string Clave(NivelConfig c)
        {
            return c.total + "|" + c.maxVivos + "|" + c.intervalo.ToString("F2") + "|"
                 + c.velocidad.ToString("F2") + "|" + c.dano + "|" + c.capturas;
        }
    }
}
