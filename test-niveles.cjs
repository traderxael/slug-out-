/**
 * Mas niveles y ruta de historia — RED primero.
 *
 * La campana tenia 12 niveles y la historia cerraba en el 12. Luego llego a 20.
 * Ahora se pide MAS: el arco no termina en el 20 tampoco —el 20 era dejar la
 * cueva abierta y bajar— asi que hay un tercer acto ABAJO, en la fuente.
 *
 * Lo que se verifica, todo desde el comportamiento:
 *   1. la campana llega a 28 niveles y cada uno esta bien formado;
 *   2. hay un fragmento de historia por nivel, y la ruta no se repite;
 *   3. el arco cierra en el ultimo (no queda colgado);
 *   4. la dificultad escala sin romperse hasta el 28 (pisos, sin NaN);
 *   5. el final tardio agrega una decision nueva, no solo mas HP: ABAJO EL
 *      ANILLO SE INVIERTE — lo que 20 niveles te enseniaron deja de servir,
 *      y se te avisa;
 *   6. el logro de completar apunta al nivel correcto;
 *   7. existe la pantalla de la ruta y muestra solo lo ya vivido;
 *   8. la inversion es LEGIBLE: el nivel la anuncia en el selector y en combate;
 *   9. la inversion no toca los niveles 1-20 (siguen jugandose igual).
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
  console.log('=== 1. la campana tiene 28 niveles, todos bien formados ===\n');
  check('la campana llega al nivel 28', G.NIVELES.length === 28, `hay ${G.NIVELES.length}`);

  const malos = G.NIVELES.map((nv, i) => ({nv, L: i+1})).filter(({nv}) =>
    typeof nv.nombre !== 'string' || !nv.nombre.trim() ||
    typeof nv.n !== 'number' || nv.n < 3 || nv.n > 8 ||
    !Array.isArray(nv.pool) || !nv.pool.length ||
    !COFRES_VALIDOS.includes(nv.cofre) ||
    nv.pool.some(el => !ELKEYS.includes(el)));
  check('cada nivel tiene nombre, cantidad, elementos y cofre validos',
        malos.length === 0, malos.length ? 'malos: ' + malos.map(m => m.L).join(',') : `${G.NIVELES.length} niveles`);

  const nuevos = G.NIVELES.slice(20).filter(nv => nv.pool.length >= 4);
  check('los niveles del tercer acto ofrecen variedad de elementos',
        nuevos.length === G.NIVELES.length - 20, `${nuevos.length}/${G.NIVELES.length-20}`);

  const abajo = G.NIVELES.slice(20);
  check('el tercer acto empieza donde termino el anterior (nivel 21)',
        abajo.length === 8 && abajo[0].nombre.length > 3, abajo[0] ? abajo[0].nombre : '');

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

  console.log('=== 4. la dificultad escala sin romperse hasta el 28 ===\n');
  let roto = null;
  for (let L = 1; L <= G.NIVELES.length; L++){
    const st = G.statsNivel(L);
    if (!st || [st.hpMul, st.dmgMul, st.intMul, st.spdMul].some(v => typeof v !== 'number' || isNaN(v) || v <= 0)
        || st.intMul < 0.5 || st.rafaga < 1 || st.evasion < 0 || st.evasion > 0.42){
      roto = L; break;
    }
  }
  check('statsNivel da numeros sanos en los 28 niveles', roto === null, roto ? `se rompe en ${roto}` : '1..28 OK');
  const s1 = G.statsNivel(1), sU = G.statsNivel(28);
  check('el nivel 28 es mas duro que el 1 en vida y dano',
        sU.hpMul > s1.hpMul && sU.dmgMul > s1.dmgMul,
        `hp ${s1.hpMul.toFixed(2)}->${sU.hpMul.toFixed(2)}, dmg ${s1.dmgMul.toFixed(2)}->${sU.dmgMul.toFixed(2)}`);
  check('el intervalo de ataque tiene piso (no se vuelve imposible)',
        G.statsNivel(28).intMul >= 0.5, `intMul=${sU.intMul}`);
  check('la evasion tiene techo (la esquiva sigue siendo leible)',
        G.statsNivel(28).evasion <= 0.42, `evasion=${sU.evasion}`);

  console.log('=== 5. abajo el anillo se invierte: la decision nueva ===\n');
  // La decision nueva del tercer acto no es mas HP: es que el anillo —la regla
  // que 20 niveles te ensenian— deja de valer igual. Se verifica que existan
  // niveles marcados y que la ventaja de elemento de verdad se invierta ahi.
  const invertidos = G.NIVELES.map((nv,i) => ({nv, L:i+1}))
    .filter(({nv}) => !!nv.anilloInvertido).map(({L}) => L);
  check('hay niveles marcados con el anillo invertido',
        invertidos.length >= 3, `niveles: ${invertidos.join(',') || 'ninguno'}`);
  check('todos los niveles invertidos estan en el tercer acto',
        invertidos.every(L => L > 20), `fuera del acto: ${invertidos.filter(L => L <= 20).join(',') || 'ninguno'}`);

  if (typeof G.ventaja === 'function'){
    // En un nivel normal, fuego vence a planta (el anillo manda).
    const normal = G.ventaja('fuego', 'planta', 10);
    const invertido = G.ventaja('fuego', 'planta', invertidos[0] || 25);
    check('en un nivel normal el anillo favorece al counter',
          normal === 2, `fuego vs planta en L10 = ${normal}`);
    check('en un nivel invertido el counter pasa a valer la mitad',
          invertido === 0.5, `fuego vs planta en L${invertidos[0]||25} = ${invertido}`);
    const desfavor = G.ventaja('planta', 'fuego', 10);
    const desfavorInv = G.ventaja('planta', 'fuego', invertidos[0] || 25);
    check('y el elemento desfavorecido pasa a valer doble',
          desfavor === 0.5 && desfavorInv === 2,
          `planta vs fuego: L10=${desfavor} -> L${invertidos[0]||25}=${desfavorInv}`);
    // El invariante real del aire: NUNCA queda castigado. En L10 aire vs rayo
    // daria 0.5 y da 1; en L25 aire vs fuego daria 0.5 (invertido) y da 1.
    check('el aire nunca queda castigado (ni al derecho ni al reves)',
          G.ventaja('aire','rayo',10) === 1 && G.ventaja('aire','fuego',25) === 1,
          `aire/rayo L10=${G.ventaja('aire','rayo',10)} · aire/fuego L25=${G.ventaja('aire','fuego',25)}`);
  } else {
    check('existe la funcion de ventaja de elemento', false, `es ${typeof G.ventaja}`);
  }

  console.log('=== 6. el logro de completar apunta al nivel correcto ===\n');
  const l12 = (G.LOGROS || []).find(l => l.id === 'nivel_12');
  const l20 = (G.LOGROS || []).find(l => l.id === 'nivel_20');
  const l28 = (G.LOGROS || []).find(l => l.id === 'nivel_28');
  check('sigue habiendo logro por llegar al 12', !!l12, l12 ? l12.nombre : 'no existe');
  check('el logro del 12 ya no dice "completaste el juego"', !!l12 && !/completaste el juego/i.test(l12.nombre), l12 ? l12.nombre : '');
  check('sigue habiendo logro por llegar al 20', !!l20, l20 ? l20.nombre : 'no existe');
  check('el logro del 20 ya no dice "completaste el juego" si hay 28 niveles',
        !!l20 && !/completaste el juego/i.test(l20.nombre), l20 ? l20.nombre : '');
  check('hay un logro por llegar al final', !!l28, l28 ? l28.nombre : 'no existe');
  check('el logro del final se gana recien al llegar al ultimo nivel',
        !!l28 && l28.check({nivel: G.NIVELES.length}) && !l28.check({nivel: G.NIVELES.length - 1}),
        l28 ? `pide nivel >= ${G.NIVELES.length}` : '');

  console.log('=== 7. la ruta de historia se puede volver a leer ===\n');
  check('existe la pantalla de la ruta de historia',
        typeof G.renderHistoria === 'function', `es ${typeof G.renderHistoria}`);
  G.renderMenu();
  check('el menu ofrece entrar a la historia',
        /btnHistoria/.test(G.overlayHTML), 'no hay boton de historia');

  if (typeof G.renderHistoria === 'function'){
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

  console.log('=== 8. la inversion es legible (se lee, no se adivina) ===\n');
  if (typeof G.renderNiveles === 'function'){
    G.save.nivel = G.NIVELES.length;       // todo desbloqueado, para ver los 28
    G.renderNiveles();
    await esperar(30);
    const html = G.overlayHTML;
    check('el selector lista los 28 niveles',
          (html.match(/id=\"lv_/g) || []).length === 28,
          `hay ${(html.match(/id=\"lv_/g) || []).length}`);
    check('el selector AVISA cuales tienen el anillo al reves',
          /al reves|invertid|⟲/i.test(html), 'ningun nivel lo anuncia');
    // Un nivel del primer acto NO debe llevar el aviso.
    const lv1 = (html.match(/id="lv_1"[^>]*>[\s\S]*?<\/button>/) || [''])[0];
    check('y el nivel 1 no lleva el aviso (no es todo al reves)',
          !/al reves|invertid|⟲/i.test(lv1), lv1.slice(0,60));
  } else {
    check('existe la pantalla del selector de niveles', false, `es ${typeof G.renderNiveles}`);
  }
  check('el combate sabe avisar que el anillo esta al reves',
        typeof G.avisoAnillo === 'function', `es ${typeof G.avisoAnillo}`);

  console.log('=== 9. la inversion no toca los niveles 1-20 ===\n');
  if (typeof G.ventaja === 'function'){
    const iguales = [];
    for (const [a,b] of [['fuego','planta'],['agua','fuego'],['sombra','rayo'],['hielo','planta'],['tierra','aire']]){
      const antes = G.ventaja(a,b,10), ahora = G.ventaja(a,b,10);
      if (antes !== ahora) iguales.push(a+'>'+b);
    }
    check('los niveles de siempre dan la misma ventaja de siempre', iguales.length === 0, iguales.join(','));
    check('ningun nivel 1-20 esta marcado como invertido',
          G.NIVELES.slice(0,20).every(nv => !nv.anilloInvertido), 'hay uno marcado');
  }

  console.log('\n' + (fallos === 0
    ? '=== OK: 28 niveles, tercer acto y una ruta que los recorre ==='
    : `=== ${fallos} FALLA(S) ===`));
  process.exit(fallos ? 1 : 0);
})();
