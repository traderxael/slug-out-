using System.Collections.Generic;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;

/// El pedido deja el PvP online para despues y pide "preparar la separacion limpia" con una lista
/// precisa de lo que falta. Estas pruebas fijan las dos piezas de esa preparacion que si se pueden
/// cerrar sin red: (1) que un duelo se pueda describir con ids + semilla + jugadas y reconstruir
/// idéntico en otra instancia — o sea, que el estado NO tiene que viajar por el cable — y (2) que
/// exista un unico predicado donde el host descarte la accion ilegal de un cliente.
/// No prueban transporte: no hay sockets, salas ni reconexion en ningun lado.
public class DueloCheckpointTests
{
    const int Semilla = 4242;

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

    static Duelo Armar(float escala)
    {
        Duelo d = new Duelo();
        d.Cargar(Equipo(0), Equipo(3), escala, Semilla);
        return d;
    }

    /// Juega un plan corto validando antes de resolver, que es el patron de host: accion ilegal ->
    /// se descarta y no se registra.
    static Duelo JugarValidando(Duelo d, AccionDuelo[] plan)
    {
        for (int i = 0; i < plan.Length && !d.Terminado; i++)
        {
            string motivo;
            if (!d.AccionLegal(plan[i], 1, out motivo)) continue;
            d.TurnoJugador(plan[i], 1);
        }
        return d;
    }

    static DueloCheckpoint Jugado(float escala)
    {
        Duelo d = JugarValidando(Armar(escala), new[] {
            AccionDuelo.Atacar, AccionDuelo.Atacar, AccionDuelo.Cambiar,
            AccionDuelo.Atacar, AccionDuelo.Atacar });
        Assert.That(d.jugadas.Count, Is.GreaterThanOrEqualTo(3),
            "el plan no jugo lo suficiente: la prueba no estaria comparando nada");
        return DueloCheckpoint.Desde(d);
    }

    [Test] public void ElCheckpointSobreviveAlCable()
    {
        DueloCheckpoint c = Jugado(1.25f);
        string json = c.AJson();
        DueloCheckpoint v = DueloCheckpoint.DesdeJson(json);

        Assert.That(v, Is.Not.Null, "el JSON no vuelve a un checkpoint");
        Assert.That(json, Does.Contain("\"semilla\":4242"), "la semilla no esta en el cable");
        Assert.That(v.semilla, Is.EqualTo(c.semilla));
        Assert.That(v.escalaRival, Is.EqualTo(c.escalaRival).Within(0.0001f));
        Assert.That(v.equipoJugador, Is.EqualTo(c.equipoJugador));
        Assert.That(v.equipoRival, Is.EqualTo(c.equipoRival));
        Assert.That(v.jugadas.Count, Is.EqualTo(c.jugadas.Count));
        for (int i = 0; i < v.jugadas.Count; i++)
        {
            Assert.That(v.jugadas[i].accion, Is.EqualTo(c.jugadas[i].accion));
            Assert.That(v.jugadas[i].indiceCambio, Is.EqualTo(c.jugadas[i].indiceCambio));
        }
    }

    [Test] public void ElOtroLadoReconstruyeElMismoDueloSinEnviarElEstado()
    {
        Duelo original = JugarValidando(Armar(1f), new[] {
            AccionDuelo.Atacar, AccionDuelo.Atacar, AccionDuelo.Cambiar,
            AccionDuelo.Atacar, AccionDuelo.Atacar });
        DueloCheckpoint c = DueloCheckpoint.Desde(original);

        Assert.That(c.AJson(), Does.Not.Contain("registro"),
            "el checkpoint esta mandando el historial: el estado deberia reproducirse, no viajar");

        string motivo;
        Duelo remoto = c.Reconstruir(out motivo, true);
        Assert.That(remoto, Is.Not.Null, "no se pudo reconstruir: " + motivo);
        Assert.That(DueloCheckpoint.Huella(remoto), Is.EqualTo(DueloCheckpoint.Huella(original)),
            "el lado reconstruido divergio del original");
    }

    [Test] public void UnaJugadaDeMenosDivideLasHuellas()
    {
        DueloCheckpoint c = Jugado(1f);
        string descarte;
        string completa = DueloCheckpoint.Huella(c.Reconstruir(out descarte));
        Assert.That(descarte, Is.Null, "el replay propio no paso la validacion: " + descarte);

        c.jugadas.RemoveAt(c.jugadas.Count - 1);
        string recortada = DueloCheckpoint.Huella(c.Reconstruir(out descarte));

        Assert.That(recortada, Is.Not.EqualTo(completa),
            "con una jugada menos el duelo sale igual: la comparacion de arriba no prueba nada");
    }

    [Test] public void UnIdFantasmaNoPasaElValidador()
    {
        DueloCheckpoint c = Jugado(1f);
        c.equipoRival[0] = "babosa-que-no-existe";
        string motivo;
        Assert.That(c.EquiposValidos(out motivo), Is.False);
        Assert.That(motivo, Does.Contain("babosa-que-no-existe"));
        Assert.That(c.Reconstruir(out motivo), Is.Null, "Reconstruir dejo pasar el id fantasma");

        // Un hueco (id vacio) si se tolera: es lo que Duelo.Cargar ya acepta para equipos cortos.
        DueloCheckpoint h = Jugado(1f);
        h.equipoJugador[2] = null;
        Assert.That(h.EquiposValidos(out motivo), Is.True, "un hueco del equipo no deberia rechazar: " + motivo);
        Assert.That(h.Reconstruir(out motivo), Is.Not.Null, "no se pudo reconstruir con hueco: " + motivo);
    }

    [Test] public void AccionLegalRechazaLoQueLaPantallaTambienRechaza()
    {
        Duelo d = Armar(1f);
        string m;

        Assert.That(d.AccionLegal(AccionDuelo.Atacar, -1, out m), Is.True, m);

        d.jugador[0].enfriamiento = 2;
        Assert.That(d.AccionLegal(AccionDuelo.Habilidad, -1, out m), Is.False,
            "la habilidad enfriada paso como legal");
        Assert.That(m, Does.Contain("2"), "el motivo no dice cuantos turnos faltan: " + m);
        d.jugador[0].enfriamiento = 0;

        Assert.That(d.AccionLegal(AccionDuelo.Cambiar, 0, out m), Is.False,
            "cambiar a la babosa que ya actua paso como legal");
        Assert.That(d.AccionLegal(AccionDuelo.Cambiar, 9, out m), Is.False,
            "un indice fuera del equipo paso como legal");
        Assert.That(d.AccionLegal(AccionDuelo.Cambiar, -1, out m), Is.False,
            "un cambio sin indice paso como legal");
        Assert.That(d.AccionLegal(AccionDuelo.Cambiar, 1, out m), Is.True, m);

        d.jugador[2].hp = 0f;
        Assert.That(d.AccionLegal(AccionDuelo.Cambiar, 2, out m), Is.False,
            "cambiar a una babosa muerta paso como legal");

        d.jugador[0].hp = 0f;
        Assert.That(d.AccionLegal(AccionDuelo.Atacar, -1, out m), Is.False,
            "pudo actuar con la babosa activa fuera de combate");

        foreach (Combatiente r in d.rival) r.hp = 0f;
        d.jugador[0].hp = 999f;
        for (int i = 0; i < 8 && !d.Terminado; i++) d.TurnoJugador(AccionDuelo.Atacar);
        Assert.That(d.Terminado, Is.True, "el duelo no cerro: la ultima asercion no vale");
        Assert.That(d.AccionLegal(AccionDuelo.Atacar, -1, out m), Is.False,
            "se puede seguir jugando despues de terminado");
    }
}
