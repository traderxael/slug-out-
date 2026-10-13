/**
 * Mejoras: arma, babosas, escudo y fuego rapido — RED primero.
 *
 * Hoy las gemas solo compran cofres. Se pide poder MEJORAR: el arma, cada
 * babosa, y dos habilidades que se USAN en combate (un escudo y x2 de
 * velocidad de disparo).
 *
 * Lo que se verifica, todo desde el comportamiento:
 *   1. la estructura de mejoras existe y arranca en cero;
 *   2. el costo sube con el nivel y nunca es gratis ni infinito;
 *   3. el arma da mas dano y recarga mas rapido, sin pasarse de rosca;
 *   4. cada babosa da mas dano, y solo si la tenes;
 *   5. las dos habilidades dan usos por combate, y ninguno sin comprarlas;
 *   6. comprar gasta gemas, y no deja comprar sin gemas ni pasado el tope;
 *   7. las mejoras sobreviven guardar/cargar;
 *   8. en combate: el fuego rapido recarga al doble de velocidad y el escudo
 *      frena el dano (y se gasta al frenarlo);
 *   9. es legible: la pantalla anuncia las habilidades y sus teclas.
 *
 * Para correr: node test-mejoras.cjs
 */
const { buildGame } = require('./harness.cjs');

let fallos = 0;
const check = (n, ok, d) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${n}` + (d ? `   [${d}]` : ''));
  if (!ok) fallos++;
};
const len = (e, k) => (e && typeof e[k] === 'number') ? e[k] : -1;
const ELKEYS = ['fuego','agua','tierra','aire','hielo','rayo','planta','sombra'];

const G = buildGame({});
const esperar = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  console.log('=== 1. la estructura de mejoras existe y arranca en cero ===\n');
  const m = G.save.mejoras;
  check('el save trae el bloque de mejoras', !!m, `es ${typeof m}`);
  check('el arma arranca sin mejorar', len(m, 'arma') === 0, `arma=${len(m,'arma')}`);
  check('el escudo arranca sin comprar', len(m, 'escudo') === 0, `escudo=${len(m,'escudo')}`);
  check('el fuego rapido arranca sin comprar', len(m, 'rapidfire') === 0, `rapidfire=${len(m,'rapidfire')}`);
  check('cada babosa arranca sin mejorar',
        !!m.babosas && ELKEYS.every(el => len(m.babosas, el) === 0),
        m && m.babosas ? ELKEYS.map(el => el+'='+len(m.babosas,el)).join(' ') : 'no hay babosas');
  check('hay un tope de mejora', typeof G.MEJORA_MAX === 'number' && G.MEJORA_MAX > 0, `max=${G.MEJORA_MAX}`);

  console.log('=== 2. el costo sube con el nivel ===\n');
  if (typeof G.costoMejora === 'function'){
    const costosArma = [], costosHab = [];
    for (let n = 0; n < G.MEJORA_MAX; n++){ costosArma.push(G.costoMejora('arma', n)); costosHab.push(G.costoMejora('escudo', n)); }
    check('mejorar el arma cuesta cada vez mas',
          costosArma.every((c, i) => c > 0 && (i === 0 || c > costosArma[i-1])), costosArma.join(' '));
    check('las habilidades cuestan mas que el arma al mismo nivel',
          costosHab.every((c, i) => c > costosArma[i]), costosHab.join(' '));
    check('ningun costo es absurdo (cabe en una campana larga)',
          costosArma[G.MEJORA_MAX-1] < 5000 && costosHab[G.MEJORA_MAX-1] < 20000,
          `arma tope ${costosArma[G.MEJORA_MAX-1]}, hab tope ${costosHab[G.MEJORA_MAX-1]}`);
  } else {
    check('existe la funcion de costo', false, `es ${typeof G.costoMejora}`);
  }

  console.log('=== 3. el arma da mas dano y recarga mas rapido ===\n');
  if (typeof G.armaDano === 'function' && typeof G.armaCd === 'function'){
    const danos = [], cds = [];
    for (let n = 0; n <= G.MEJORA_MAX; n++){ danos.push(G.armaDano(n)); cds.push(G.armaCd(n)); }
    check('el dano del arma sube nivel a nivel',
          danos.every((d, i) => d > 0 && (i === 0 || d > danos[i-1])), danos.map(d => d.toFixed(2)).join(' '));
    check('sin mejorar, el arma es la de siempre (multiplicador 1)',
          Math.abs(danos[0] - 1) < 1e-9, `nivel 0 = ${danos[0]}`);
    check('la recarga baja nivel a nivel',
          cds.every((c, i) => c > 0 && (i === 0 || c < cds[i-1])), cds.map(c => c.toFixed(2)).join(' '));
    check('sin mejorar, la recarga es la de siempre (multiplicador 1)',
          Math.abs(cds[0] - 1) < 1e-9, `nivel 0 = ${cds[0]}`);
    check('y nunca deja de haber recarga (no es disparo infinito)',
          cds[G.MEJORA_MAX] >= 0.5, `tope ${cds[G.MEJORA_MAX].toFixed(2)}`);
    check('el dano del arma al tope no rompe el balance',
          danos[G.MEJORA_MAX] <= 1.6, `tope ${danos[G.MEJORA_MAX].toFixed(2)}`);
  } else {
    check('existen las funciones del arma', false, `${typeof G.armaDano}/${typeof G.armaCd}`);
  }

  console.log('=== 4. cada babosa da mas dano, y solo si la tenes ===\n');
  if (typeof G.babosaDano === 'function'){
    // Sin comprar nada, ninguna babosa da bonus.
    const base = G.babosaDano('fuego');
    check('una babosa sin mejorar no da bonus', Math.abs(base - 1) < 1e-9, `= ${base}`);
    // Se le sube el nivel a una babosa que SI se tiene y a una que no.
    G.save.mejoras.babosas.fuego = G.MEJORA_MAX;
    G.save.mejoras.babosas.sombra = G.MEJORA_MAX;
    const tenida = G.babosaDano('fuego'), noTenida = G.babosaDano('sombra');
    check('mejorar una babosa que tenes sube su dano',
          tenida > 1, `fuego al tope = ${tenida.toFixed(2)}`);
    check('mejorar una babosa que NO tenes no hace nada',
          Math.abs(noTenida - 1) < 1e-9, `sombra (no tenida) = ${noTenida.toFixed(2)}`);
    check('el bonus de babosa no se pasa de rosca', tenida <= 1.6, `tope ${tenida.toFixed(2)}`);
    G.save.mejoras.babosas.fuego = 0; G.save.mejoras.babosas.sombra = 0;
  } else {
    check('existe la funcion de dano de babosa', false, `es ${typeof G.babosaDano}`);
  }

  console.log('=== 5. las dos habilidades dan usos por combate ===\n');
  if (typeof G.usosEscudo === 'function' && typeof G.usosRapid === 'function'){
    check('sin comprar el escudo no hay usos', G.usosEscudo() === 0, `usos=${G.usosEscudo()}`);
    check('sin comprar el fuego rapido no hay usos', G.usosRapid() === 0, `usos=${G.usosRapid()}`);
    const usosEsc = [], usosRap = [];
    for (let n = 1; n <= G.MEJORA_MAX; n++){
      G.save.mejoras.escudo = n; G.save.mejoras.rapidfire = n;
      usosEsc.push(G.usosEscudo()); usosRap.push(G.usosRapid());
    }
    check('cada nivel de escudo da mas usos por combate',
          usosEsc.every((u, i) => u > 0 && (i === 0 || u > usosEsc[i-1])), usosEsc.join(' '));
    check('cada nivel de fuego rapido da mas usos por combate',
          usosRap.every((u, i) => u > 0 && (i === 0 || u > usosRap[i-1])), usosRap.join(' '));
    check('y duran mas por nivel (el escudo se lee en el HUD)',
          G.escudoDur(G.MEJORA_MAX) > G.escudoDur(1),
          `${G.escudoDur(1)}ms -> ${G.escudoDur(G.MEJORA_MAX)}ms`);
    check('el fuego rapido dura lo bastante para notarse',
          G.rapidDur(1) >= 2000, `${G.rapidDur(1)}ms`);
    G.save.mejoras.escudo = 0; G.save.mejoras.rapidfire = 0;
  } else {
    check('existen las funciones de usos', false, `${typeof G.usosEscudo}/${typeof G.usosRapid}`);
  }

  console.log('=== 6. comprar gasta gemas y respeta los limites ===\n');
  if (typeof G.comprarMejora === 'function'){
    G.save.gemas = 100000;
    const antes = G.save.gemas, nivelAntes = G.save.mejoras.arma;
    const costo = G.costoMejora('arma', nivelAntes);
    const ok = G.comprarMejora('arma');
    check('se puede comprar una mejora', ok === true, `devolvio ${ok}`);
    check('y sube el nivel del arma', G.save.mejoras.arma === nivelAntes + 1, `${nivelAntes} -> ${G.save.mejoras.arma}`);
    check('y cobra las gemas', G.save.gemas === antes - costo, `${antes} -> ${G.save.gemas} (costo ${costo})`);

    // Al tope no se puede seguir.
    G.save.mejoras.arma = G.MEJORA_MAX;
    const gemasAntes = G.save.gemas;
    const alTope = G.comprarMejora('arma');
    check('pasado el tope no se puede mejorar mas', alTope === false, `devolvio ${alTope}`);
    check('y no cobra por intentarlo', G.save.gemas === gemasAntes, `${gemasAntes} -> ${G.save.gemas}`);

    // Sin gemas no se puede.
    G.save.mejoras.arma = 0; G.save.gemas = 0;
    const sinGemas = G.comprarMejora('arma');
    check('sin gemas no se puede comprar', sinGemas === false, `devolvio ${sinGemas}`);
    check('y el nivel no se mueve', G.save.mejoras.arma === 0, `arma=${G.save.mejoras.arma}`);
    check('y no quedan gemas negativas', G.save.gemas === 0, `gemas=${G.save.gemas}`);

    // Una clave que no existe no revienta.
    let basura = null;
    try { basura = G.comprarMejora('no-existe'); } catch(e){ basura = 'exploto: '+e.message; }
    check('una mejora inexistente no revienta el juego', basura === false, `devolvio ${basura}`);

    G.save.mejoras.arma = 0; G.save.gemas = 100000;
  } else {
    check('existe la funcion de compra', false, `es ${typeof G.comprarMejora}`);
  }

  console.log('=== 7. las mejoras sobreviven guardar y cargar ===\n');
  if (typeof G.normalizeSave === 'function'){
    const guardado = JSON.parse(JSON.stringify({
      gemas: 500, nivel: 3,
      mejoras: { arma: 3, escudo: 2, rapidfire: 1, babosas: {fuego: 4, sombra: 2} }
    }));
    const cargado = G.normalizeSave(guardado);
    check('el arma sobrevive la carga', len(cargado.mejoras, 'arma') === 3, `arma=${len(cargado.mejoras,'arma')}`);
    check('el escudo sobrevive la carga', len(cargado.mejoras, 'escudo') === 2, `escudo=${len(cargado.mejoras,'escudo')}`);
    check('el fuego rapido sobrevive la carga', len(cargado.mejoras, 'rapidfire') === 1, `rapidfire=${len(cargado.mejoras,'rapidfire')}`);
    check('las babosas mejoradas sobreviven la carga',
          len(cargado.mejoras.babosas, 'fuego') === 4 && len(cargado.mejoras.babosas, 'sombra') === 2,
          `fuego=${len(cargado.mejoras.babosas,'fuego')} sombra=${len(cargado.mejoras.babosas,'sombra')}`);
    // Un save viejo, sin mejoras, no debe romper nada.
    const viejo = G.normalizeSave({gemas: 10, nivel: 1});
    check('un save viejo sin mejoras sigue cargando',
          !!viejo.mejoras && len(viejo.mejoras, 'arma') === 0, `arma=${len(viejo.mejoras,'arma')}`);
    // Un save corrupto (nivel absurdo) se sanea al tope, no se acepta.
    const corrupto = G.normalizeSave({mejoras: {arma: 99, escudo: -5, rapidfire: 'tres'}});
    check('un nivel de mejora corrupto se sanea al tope',
          len(corrupto.mejoras, 'arma') === G.MEJORA_MAX && len(corrupto.mejoras, 'escudo') === 0
          && len(corrupto.mejoras, 'rapidfire') === 0,
          `arma=${len(corrupto.mejoras,'arma')} escudo=${len(corrupto.mejoras,'escudo')} rapidfire=${len(corrupto.mejoras,'rapidfire')}`);
  } else {
    check('existe normalizeSave', false, `es ${typeof G.normalizeSave}`);
  }

  console.log('=== 8. en combate: fuego rapido y escudo ===\n');
  if (typeof G.iniciar === 'function' && typeof G.usarEscudo === 'function' && typeof G.usarRapid === 'function'){
    // --- fuego rapido ---
    G.save.mejoras.rapidfire = G.MEJORA_MAX;
    G.iniciar('nivel', 1);
    const usosAntes = G.state.usos.rapidfire;
    const cdNormal = G.cooldownFor(1);
    const activo = G.usarRapid();
    check('se puede activar el fuego rapido', activo === true, `devolvio ${activo}`);
    check('y gasta un uso', G.state.usos.rapidfire === usosAntes - 1, `${usosAntes} -> ${G.state.usos.rapidfire}`);
    check('y recarga al doble de velocidad mientras dura',
          G.cooldownFor(1) < cdNormal * 0.75, `cooldown ${cdNormal} -> ${G.cooldownFor(1)}`);
    // Se termina solo: no es para siempre.
    const dur = G.state.rapidLeft;
    G.update(dur + 50);
    check('y se termina solo (vuelve la recarga normal)',
          G.state.rapidLeft <= 0 && Math.abs(G.cooldownFor(1) - cdNormal) < 1e-9,
          `rapidLeft=${G.state.rapidLeft}`);
    // Sin usos no se puede volver a activar.
    G.state.usos.rapidfire = 0;
    check('sin usos no se puede volver a activar', G.usarRapid() === false, '');
    G.save.mejoras.rapidfire = 0;

    // --- escudo ---
    G.save.mejoras.escudo = G.MEJORA_MAX;
    G.iniciar('nivel', 1);
    const uAntes = G.state.usos.escudo;
    check('se puede activar el escudo', G.usarEscudo() === true, '');
    check('y gasta un uso', G.state.usos.escudo === uAntes - 1, `${uAntes} -> ${G.state.usos.escudo}`);
    // El escudo frena el dano: se mide contra un golpe real.
    G.state.player.hp = 100; G.state.player.maxHp = 100;
    G.state.enemy.atkInterval = 1e9;              // el rival no ataca por su cuenta
    const hpAntes = G.state.player.hp;
    if (typeof G.danoAlJugador === 'function'){
      const recibido = G.danoAlJugador(12);
      check('con el escudo puesto el dano no pasa', recibido === 0 && G.state.player.hp === hpAntes,
            `recibio ${recibido}, hp ${hpAntes} -> ${G.state.player.hp}`);
      check('y el escudo se gasta al frenar un golpe',
            G.state.escudoLeft < G.escudoDur(G.MEJORA_MAX), `quedan ${G.state.escudoLeft}ms`);
      // Se termina: despues el dano vuelve a pasar. danoAlJugador DEVUELVE
      // cuanto pasa y no lo aplica — lo aplica el que llama (la roca, el
      // escupitajo), igual que en produccion.
      G.update(G.state.escudoLeft + 60);
      const recibido2 = G.danoAlJugador(12);
      G.state.player.hp = Math.max(0, G.state.player.hp - recibido2);
      check('terminado el escudo el dano vuelve a pasar',
            recibido2 === 12 && G.state.player.hp === hpAntes - 12,
            `recibio ${recibido2}, hp ${G.state.player.hp}`);
    } else {
      check('existe la funcion de dano al jugador', false, `es ${typeof G.danoAlJugador}`);
    }
    // Sin el escudo comprado no hay usos.
    G.save.mejoras.escudo = 0;
    G.iniciar('nivel', 1);
    check('sin comprar el escudo no se puede usar', G.usarEscudo() === false, `usos=${G.state.usos.escudo}`);
    check('y sin usos el escudo no esta puesto', G.state.escudoLeft <= 0, `escudoLeft=${G.state.escudoLeft}`);
  } else {
    check('existen las funciones de combate de las habilidades', false,
          `${typeof G.usarEscudo}/${typeof G.usarRapid}`);
  }

  console.log('=== 9. es legible: se anuncia y se ve ===\n');
  if (typeof G.renderMejoras === 'function'){
    G.save.gemas = 5000; G.save.mejoras.arma = 1; G.save.mejoras.escudo = 2;
    G.renderMejoras();
    await esperar(30);
    const html = G.overlayHTML;
    check('hay una pantalla de mejoras', /mejora/i.test(html), '');
    check('y se entra desde el menu',
          (() => { G.renderMenu(); return /btnMejoras/.test(G.overlayHTML); })(), 'no hay boton');
    check('la pantalla muestra las gemas', html.includes(String(G.save.gemas)), '');
    check('anuncia el escudo como habilidad', /escudo/i.test(html), '');
    check('anuncia el fuego rapido como habilidad', /r[áa]pido|rapidfire/i.test(html), '');
    check('dice con que tecla se usa cada una',
          /E\b/.test(html) && /F\b/.test(html), 'faltan las teclas E y F');
    check('y muestra el nivel actual de cada cosa',
          /Arma/i.test(html) && /Babosa/i.test(html), '');
  } else {
    check('existe la pantalla de mejoras', false, `es ${typeof G.renderMejoras}`);
  }

  console.log('\n' + (fallos === 0
    ? '=== OK: arma, babosas, escudo y fuego rapido ==='
    : `=== ${fallos} FALLA(S) ===`));
  process.exit(fallos ? 1 : 0);
})();
