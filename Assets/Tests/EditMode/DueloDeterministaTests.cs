using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;

/// El pedido deja el PvP online para despues y pide "preparar la separacion limpia". El paso
/// concreto de esa preparacion es que el duelo sea reproducible: si la IA decide con el RNG global
/// de Unity, la semilla del torneo no alcanza para reconstruir la partida y un cliente remoto no
/// tiene como verificar nada. Estas pruebas fijan esa propiedad.
public class DueloDeterministaTests
{
    static SlugData[] Equipo(int offset)
    {
        SlugData[] r = SlugRoster.Todos;
        var l = new List<SlugData>();
        for (int i = 0; i < 3; i++)
        {
            SlugData s = r[(offset + i * 7) % r.Length];
            Assert.That(s, Is.Not.Null);
            l.Add(s);
        }
        return l.ToArray();
    }

    /// Juega el duelo entero con una secuencia fija de acciones del jugador y devuelve el
    /// registro completo mas el estado final, que es lo que tiene que ser identico.
    static string Jugar(Duelo d)
    {
        AccionDuelo[] plan = {
            AccionDuelo.Atacar, AccionDuelo.Habilidad, AccionDuelo.Atacar, AccionDuelo.Cambiar,
            AccionDuelo.Atacar, AccionDuelo.Habilidad, AccionDuelo.Atacar, AccionDuelo.Atacar,
            AccionDuelo.Habilidad, AccionDuelo.Cambiar, AccionDuelo.Atacar, AccionDuelo.Habilidad,
            AccionDuelo.Atacar, AccionDuelo.Atacar, AccionDuelo.Habilidad, AccionDuelo.Atacar
        };
        var sb = new StringBuilder();
        for (int i = 0; i < plan.Length && !d.Terminado; i++)
        {
            d.TurnoJugador(plan[i], 1);
            sb.Append(plan[i]).Append('|');
        }
        sb.Append("terminado=").Append(d.Terminado).Append(" gano=").Append(d.GanoJugador);
        foreach (Combatiente c in d.jugador) sb.Append(" J:").Append(c.Nombre).Append('=').Append(c.hp.ToString("F2"));
        foreach (Combatiente c in d.rival) sb.Append(" R:").Append(c.Nombre).Append('=').Append(c.hp.ToString("F2"));
        return sb.ToString();
    }

    static Duelo Armar(int semilla)
    {
        Duelo d = new Duelo();
        d.Cargar(Equipo(0), Equipo(3), 1f, semilla);
        return d;
    }

    [Test] public void LaMismaSemillaReproduceElDueloEnter()
    {
        Duelo a = Armar(4242);
        Duelo b = Armar(4242);
        string ra = Jugar(a), rb = Jugar(b);
        Assert.That(rb, Is.EqualTo(ra), "dos duelos con la misma semilla jugaron distinto");
        Assert.That(a.registro.Count, Is.GreaterThan(6), "el duelo ni siquiera se desarrollo");
    }

    [Test] public void LaIA_noMiraElRandomGlobalDeUnity()
    {
        UnityEngine.Random.InitState(1);
        string primero = Jugar(Armar(777));

        UnityEngine.Random.InitState(987654);
        float antes = UnityEngine.Random.value;
        string segundo = Jugar(Armar(777));
        float despues = UnityEngine.Random.value;

        Assert.That(segundo, Is.EqualTo(primero),
            "el resultado dependio del RNG global: el torneo no es reproducible");
        Assert.That(despues, Is.Not.EqualTo(antes),
            "el random global no se movio: la prueba no estaria tocando nada");
    }

    [Test] public void SemillasDistintasDanSecuenciasDistintas()
    {
        var vistas = new HashSet<string>();
        foreach (int s in new[] { 1, 2, 3, 5, 8, 13, 21, 34 })
            vistas.Add(Jugar(Armar(s)));
        Assert.That(vistas.Count, Is.GreaterThanOrEqualTo(2),
            "ocho semillas dieron todas el mismo duelo: la semilla no entra en la IA");
    }

    [Test] public void ElTorneoPasaSuSemillaAlDueloYLaAvanzaPorRonda()
    {
        SlugData[] equipo = Equipo(0);
        Torneo t = new Torneo(equipo, 3, 4242);
        Assert.That(t.DueloActual, Is.Not.Null);
        Assert.That(t.DueloActual.Semilla, Is.EqualTo(4242 + 1 * 31),
            "la ronda 1 no recibo la semilla del torneo");

        // Se gana la ronda dejando a los rivales en un pelo de vida y tirando hasta que cierre:
        // el torneo tiene que pasar a la ronda 2 con otra semilla, no repetir la misma secuencia.
        foreach (Combatiente c in t.DueloActual.rival) c.hp = 0.5f;
        foreach (Combatiente c in t.DueloActual.jugador) { c.hpMax = 9999f; c.hp = 9999f; }
        for (int i = 0; i < 8 && !t.DueloActual.Terminado; i++)
            t.DueloActual.TurnoJugador(AccionDuelo.Atacar);
        Assert.That(t.DueloActual.Terminado, Is.True, "no cerro el duelo");
        Assert.That(t.DueloActual.GanoJugador, Is.True, "no conto como victoria");
        t.Continuar();
        Assert.That(t.Ronda, Is.EqualTo(2));
        Assert.That(t.DueloActual.Semilla, Is.Not.EqualTo(4242 + 31),
            "la ronda 2 juega con la misma semilla que la 1");
    }
}
