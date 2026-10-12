/**
 * Mas niveles y ruta de historia — RED primero.
 *
 * La campana tenia 12 niveles y la historia cerraba en el 12. Este test fija lo
 * que se pide: mas niveles Y que la historia los recorra como una ruta (un
 * fragmento por combate, en orden, que se pueda releer).
 *
 * Lo que se verifica, todo desde el comportamiento:
 *   1. la campana llega a 20 niveles y cada uno esta bien formado;
 *   2. hay un fragmento de historia por nivel, y la ruta no se repite;
 *   3. el arco cierra en el ultimo (no queda colgado);
 *   4. la dificultad escala sin romperse hasta el 20 (pisos, sin NaN);
 *   5. el final tardio agrega una decision nueva, no solo mas HP;
 *   6. el logro de completar apunta al nivel correcto;
 *   7. existe la pantalla de la ruta y muestra solo lo ya vivido.
 *
 * Para correr: node test-niveles.cjs
 */
const { buildGame } = require('./harness.cjs');

let fallos = 0;
const check = (n, ok, d) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${n}` + (d ? `   [${d}]` : ''));
  if (!ok) fallos++;
};
const COFRES_VALIDOS = ['madera','hierro','dorado','cristal','mitico'];
const ELKEYS = ['fuego','agua','tierra','aire','hielo','rayo','planta','sombra'];

const G = buildGame({});
const esperar = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  console.log('=== 1. la campana tiene 20 niveles, todos bien formados ===\n');
  check('la campana llega al nivel 20', G.NIVELES.length === 20, `hay ${G.NIVELES.length}`);

  const malos = G.NIVELES.map((nv, i) => ({nv, L: i+1})).filter(({nv}) =>
    typeof nv.nombre !== 'string' || !nv.nombre.trim() ||
    typeof nv.n !== 'number' || nv.n < 3 || nv.n > 8 ||
    !Array.isArray(nv.pool) || !nv.pool.length ||
    !COFRES_VALIDOS.includes(nv.cofre) ||
    nv.pool.some(el => !ELKEYS.includes(el)));
  check('cada nivel tiene nombre, cantidad, elementos y cofre validos',
        malos.length === 0, malos.length ? 'malos: ' + malos.map(m => m.L).join(',') : `${G.NIVELES.length} niveles`);

  const nuevos = G.NIVELES.slice(12).filter(nv => nv.pool.length >= 4);
  check('los niveles nuevos ofrecen variedad de elementos',
        nuevos.length === G.NIVELES.length - 12, `${nuevos.length}/${G.NIVELES.length-12}`);

  console.log('=== 2. hay un fragmento por nivel y la ruta no se repite ===\n');
  const n = Object.keys(G.HISTORIA).length;
  check('hay un fragmento de historia por cada nivel',
        n === G.NIVELES.length, `esperado ${G.NIVELES.length}, hay ${n}`);

  const incompletos = Object.entries(G.HISTORIA).filter(([, h]) =>
    !h || typeof h.titulo !== 'string' || typeof h.texto !== 'string'
    || !h.titulo.trim() || !h.texto.trim() || h.texto.trim().length < 60);
  check('cada fragmento tiene titulo y texto que cuenten algo',
        incompletos.length === 0, incompletos.length ? 'incompletos: ' + incompletos.map(([k]) => k).join(',') : `${n} fragmentos`);

  const titulos = Object.values(G.HISTORIA).map(h => h.titulo);
  const textos  = Object.values(G.HISTORIA).map(h => h.texto.trim());
  check('la ruta no repite titulos',
        new Set(titulos).size === titulos.length, `repetidos: ${titulos.length - new Set(titulos).size}`);
  check('la ruta no repite textos',
        new Set(textos).size === textos.length, `repetidos: ${textos.length - new Set(textos).size}`);

  console.log('=== 3. el arco cierra en el ultimo nivel ===\n');
  const ult = G.HISTORIA[G.NIVELES.length];
  check('el ultimo fragmento cierra el arco (no queda colgado)',
        !!ult && ult.texto.length > 120, ult ? `${ult.texto.length} caracteres` : 'no existe');
  const fin = G.HISTORIA[G.NIVELES.length - 1];
  check('el penultimo todavia deja algo por resolver',
        !!fin && fin.texto.length > 60, fin ? `${fin.texto.length} caracteres` : 'no existe');

  console.log('=== 4. la dificultad escala sin romperse hasta el 20 ===\n');
  let roto = null;
  for (let L = 1; L <= G.NIVELES.length; L++){
    const st = G.statsNivel(L);
    if (!st || [st.hpMul, st.dmgMul, st.intMul, st.spdMul].some(v => typeof v !== 'number' || isNaN(v) || v <= 0)
        || st.intMul < 0.5 || st.rafaga < 1 || st.evasion < 0){
      roto = L; break;
    }
  }
  check('statsNivel da numeros sanos en los 20 niveles', roto === null, roto ? `se rompe en ${roto}` : '1..20 OK');
  const s1 = G.statsNivel(1), s20 = G.statsNivel(20);
  check('el nivel 20 es mas duro que el 1 en vida y dano',
        s20.hpMul > s1.hpMul && s20.dmgMul > s1.dmgMul,
        `hp ${s1.hpMul.toFixed(2)}->${s20.hpMul.toFixed(2)}, dmg ${s1.dmgMul.toFixed(2)}->${s20.dmgMul.toFixed(2)}`);
  check('el intervalo de ataque tiene piso (no se vuelve imposible)',
        G.statsNivel(20).intMul >= 0.5, `intMul=${s20.intMul}`);
  check('la evasion tiene techo (la esquiva sigue siendo leible)',
        G.statsNivel(20).evasion <= 0.42, `evasion=${s20.evasion}`);

  console.log('=== 5. el final tardio agrega una decision, no solo mas HP ===\n');
  const rafagas = [];
  for (const L of [5, 10, 15, 20]) rafagas.push(`L${L}=${G.statsNivel(L).rafaga}`);
  check('la rafaga sube de escalon en el final (mas tiros por oleada)',
        G.statsNivel(20).rafaga > G.statsNivel(10).rafaga, rafagas.join(' '));

  console.log('=== 6. el logro de completar apunta al nivel correcto ===\n');
  const l12 = (G.LOGROS || []).find(l => l.id === 'nivel_12');
  const l20 = (G.LOGROS || []).find(l => l.id === 'nivel_20');
  check('sigue habiendo logro por llegar al 12', !!l12, l12 ? l12.nombre : 'no existe');
  check('el logro del 12 ya no dice "completaste el juego" si hay 20 niveles',
        !!l12 && !/completaste el juego/i.test(l12.nombre), l12 ? l12.nombre : '');
  check('hay un logro por llegar al final', !!l20, l20 ? l20.nombre : 'no existe');
  check('el logro del final se gana recien al llegar al ultimo nivel',
        !!l20 && l20.check({nivel: G.NIVELES.length}) && !l20.check({nivel: G.NIVELES.length - 1}),
        l20 ? `pide nivel >= ${G.NIVELES.length}` : '');

  console.log('=== 7. la ruta de historia se puede volver a leer ===\n');
  check('existe la pantalla de la ruta de historia',
        typeof G.renderHistoria === 'function', `es ${typeof G.renderHistoria}`);
  G.renderMenu();
  check('el menu ofrece entrar a la historia',
        /btnHistoria/.test(G.overlayHTML), 'no hay boton de historia');

  if (typeof G.renderHistoria === 'function'){
    // Solo lo ya vivido se muestra: el que gano los niveles 1 y 2 ve 2 fragmentos.
    G.save.estrellas = {1:3, 2:2};
    G.renderHistoria();
    await esperar(30);
    const html = G.overlayHTML;
    check('la ruta muestra los fragmentos ya vividos',
          html.includes(G.HISTORIA[1].titulo) && html.includes(G.HISTORIA[2].titulo), '');
    check('y esconde los que todavia no',
          !html.includes(G.HISTORIA[5].titulo), 'se ve un fragmento no jugado');
    check('lo no jugado aparece tapado, no como hueco',
          html.includes('???'), 'no hay marcador de bloqueado');
    check('la ruta se anuncia como historia', /historia/i.test(html), '');
  }

  console.log('\n' + (fallos === 0
    ? '=== OK: 20 niveles y una ruta de historia que los recorre ==='
    : `=== ${fallos} FALLA(S) ===`));
  process.exit(fallos ? 1 : 0);
})();
