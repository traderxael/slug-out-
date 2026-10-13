// Modo infinito: que deje de sentirse un extra y sea un modo propio (issue #4).
// Lo que lo hace modo propio, en orden de importancia:
//   1. Los arquetipos se PRESENTAN de a uno y se avisan (se lee, no se descubre).
//   2. Tiene su propia mini historia, en olas concretas, y CIERRA EN BUCLE
//      porque el modo no termina nunca.
//   3. La dificultad no es solo mas HP: la rafaga crece y el anillo se invierte
//      por ciclos, que es una decision nueva y no un numero mas grande.
//   4. Cierra el lazo con las mejoras: gemas crecientes y un resumen final que
//      dice cuanto te dio cada mejora y cuanto falta para la siguiente.
const h = require('./harness.cjs');
const { buildGame } = h;
let ok = 0, mal = 0;
const num = (v, d) => (typeof v === 'number' && isFinite(v)) ? v : d;

function check(nombre, cond, detalle){
  if(cond){ ok++; console.log('  OK  ', nombre); }
  else { mal++; console.log('  FALLA', nombre, ' — ', detalle || ''); }
}
function seccion(t){ console.log('\n=== ' + t + ' ==='); }

// El RED tiene que ser legible: si faltan funciones, que se vean TODAS de una,
// no que el test muera en la primera. (El harness devuelve undefined para los
// exports que el juego todavia no tiene.)
{
  const G = buildGame({});
  const faltan = ['anilloAlReves','arquetipoInfinito','avisoArquetipoInfinito',
                  'enemigoInfinito','OLAS_HISTORIA','historiaInfinito',
                  'fragmentoInfinito','mostrarHistoriaInfinito','gemasPorOla',
                  'resumenMejoras','LOGROS','histInfVistas','renderHistoriaInfinito']
                 .filter(n => G[n] === undefined);
  if(faltan.length){
    console.log('RED: el modo infinito no existe todavia. Faltan ' + faltan.length + ':');
    faltan.forEach(n => console.log('     - ' + n));
    console.log('');
  }
}

// ---------- 1. El anillo se invierte por ciclos ----------
seccion('1. el anillo se invierte por ciclos (no es un numero mas grande)');
{
  const G = buildGame({});
  // En la campana manda el nivel; del 25 al 28 queda igual (no se toca).
  check('la campana no cambia: el 25 al 28 sigue al reves',
        G.anilloAlReves(25) === true && G.anilloAlReves(28) === true,
        `25=${G.anilloAlReves(25)} 28=${G.anilloAlReves(28)}`);
  check('el nivel 20 no se toca',
        G.anilloAlReves(20) === false, `20=${G.anilloAlReves(20)}`);
  // En infinito, del 25 en adelante se invierte por ciclos de 8.
  check('en infinito la ola 25 arranca al reves',
        G.anilloAlReves(25) === true, `25=${G.anilloAlReves(25)}`);
  check('y el ciclo dura 8 olas: 25 a 32 al reves',
        [25,26,27,28,29,30,31,32].every(w => G.anilloAlReves(w) === true),
        [25,26,27,28,29,30,31,32].map(w => w+':'+G.anilloAlReves(w)).join(' '));
  check('33 a 40 de vuelta al derecho',
        [33,34,35,36,37,38,39,40].every(w => G.anilloAlReves(w) === false),
        [33,34,35,36,37,38,39,40].map(w => w+':'+G.anilloAlReves(w)).join(' '));
  check('41 a 48 se vuelve a invertir (es un ciclo, no un evento)',
        [41,42,43,44,45,46,47,48].every(w => G.anilloAlReves(w) === true),
        [41,42,43,44,45,46,47,48].map(w => w+':'+G.anilloAlReves(w)).join(' '));
  check('antes de la 25 nunca se invierte',
        [1,5,10,15,20,24].every(w => G.anilloAlReves(w) === false),
        [1,5,10,15,20,24].map(w => w+':'+G.anilloAlReves(w)).join(' '));
  // Y la inversion TIENE que cambiar el dano, si no es solo un aviso.
  // Ojo con el sentido: en el anillo AGUA le gana a FUEGO (y el aire al agua).
  const derecho = G.ventaja('agua', 'fuego', 33);  // al derecho: agua vence a fuego
  const reves   = G.ventaja('agua', 'fuego', 25);  // al reves: eso se da vuelta
  check('la inversion cambia de verdad el dano',
        derecho === 2 && reves === 0.5, `derecho=${derecho} reves=${reves}`);
  // El invariante de siempre, verificado en todos los pares y en los dos
  // sentidos del anillo: el AIRE nunca queda castigado. Si se rompiera, habria
  // un elemento siempre peor que los otros, y eso no es una decision.
  let aireCastigado = false;
  G.ELKEYS.forEach(a => G.ELKEYS.forEach(b => [25, 33, 41].forEach(L => {
    if(a === 'aire' && G.ventaja(a, b, L) === 0.5) aireCastigado = true;
  })));
  check('el aire nunca queda castigado, en ningun sentido del anillo',
        !aireCastigado, 'aire recibio 0.5 en algun par');
}

// ---------- 2. Los arquetipos se presentan de a uno ----------
seccion('2. los arquetipos se presentan de a uno, y se avisan');
{
  const G = buildGame({});
  // Las primeras olas tienen que ser aprendibles: un solo arquetipo.
  check('las olas 1 y 2 son solo lo basico',
        G.arquetipoInfinito(1) === 'normal' && G.arquetipoInfinito(2) === 'normal',
        `1=${G.arquetipoInfinito(1)} 2=${G.arquetipoInfinito(2)}`);
  check('la 3 PRESENTA el escudado (uno nuevo, no dos)',
        G.arquetipoInfinito(3) === 'escudado', `3=${G.arquetipoInfinito(3)}`);
  check('la 5 es el jefe, que ya existia como frenetico',
        G.arquetipoInfinito(5) === 'frenetico', `5=${G.arquetipoInfinito(5)}`);
  check('la 7 PRESENTA el saltarin',
        G.arquetipoInfinito(7) === 'saltarin', `7=${G.arquetipoInfinito(7)}`);
  check('despues de presentarlos se mezclan',
        new Set([9,11,13,15,17].map(w => G.arquetipoInfinito(w))).size > 1,
        [9,11,13,15,17].map(w => w+':'+G.arquetipoInfinito(w)).join(' '));

  // El aviso: tiene que decir QUE es y QUE hacer, o no se lee.
  const a3 = G.avisoArquetipoInfinito(3);
  const a7 = G.avisoArquetipoInfinito(7);
  check('la presentacion del escudado avisa y dice que hacer',
        typeof a3 === 'string' && /escud/i.test(a3) && /baja|escudo|rompe|carga/i.test(a3),
        JSON.stringify(a3));
  check('la presentacion del saltarin avisa y dice que hacer',
        typeof a7 === 'string' && /saltar/i.test(a7) && /quieto|espera|no|tiro|cae/i.test(a7),
        JSON.stringify(a7));
  check('una ola sin presentacion no avisa nada',
        G.avisoArquetipoInfinito(4) === '' && G.avisoArquetipoInfinito(10) === '',
        `4=${JSON.stringify(G.avisoArquetipoInfinito(4))} 10=${JSON.stringify(G.avisoArquetipoInfinito(10))}`);
  // El banner unico del modo: presentacion de arquetipo o ciclo invertido, uno
  // a la vez. Dos avisos juntos no se leen, se pierden los dos.
  check('el banner avisa la presentacion de arquetipo',
        /escudado/i.test(G.avisoInfinito(3)), JSON.stringify(G.avisoInfinito(3)));
  check('el banner avisa al ENTRAR en el ciclo invertido (ola 25)',
        /anillo/i.test(G.avisoInfinito(25)), JSON.stringify(G.avisoInfinito(25)));
  check('y no repite el aviso en cada ola del ciclo',
        G.avisoInfinito(26) === '' && G.avisoInfinito(32) === '',
        `26=${JSON.stringify(G.avisoInfinito(26))} 32=${JSON.stringify(G.avisoInfinito(32))}`);
  check('vuelve a avisar cuando entra el SIGUIENTE ciclo invertido (ola 41)',
        /anillo/i.test(G.avisoInfinito(41)), JSON.stringify(G.avisoInfinito(41)));
  check('la ola 33 (de vuelta al derecho) no avisa nada',
        G.avisoInfinito(33) === '', JSON.stringify(G.avisoInfinito(33)));
  check('la ola 1 no avisa nada: el arranque tiene que ser tranquilo',
        G.avisoInfinito(1) === '' && G.avisoInfinito(2) === '',
        `1=${JSON.stringify(G.avisoInfinito(1))} 2=${JSON.stringify(G.avisoInfinito(2))}`);
  // Cada arquetipo se presenta UNA sola vez: si se repitiera el aviso, dejaria
  // de ser una presentacion y pasaria a ser ruido.
  const presentaciones = [];
  for(let w = 1; w <= 60; w++){
    const a = G.avisoArquetipoInfinito(w);
    if(a) presentaciones.push(w + ':' + a.slice(0, 12));
  }
  const nombres = presentaciones.map(s => s.split(':')[1]);
  check('cada arquetipo se presenta una sola vez en 60 olas',
        nombres.length === new Set(nombres).size, presentaciones.join(' | '));
}

// ---------- 3. La dificultad no es solo mas HP ----------
seccion('3. la dificultad no es solo mas HP');
{
  const G = buildGame({});
  // La rafaga tiene que crecer: si no, el infinito es "un bicho mas gordo".
  check('al principio la rafaga es 1 (como ahora)',
        num(G.enemigoInfinito(1).rafaga, -1) === 1, `${G.enemigoInfinito(1).rafaga}`);
  check('mas adelante la rafaga crece',
        num(G.enemigoInfinito(40).rafaga, -1) > num(G.enemigoInfinito(1).rafaga, -1),
        `ola 1=${G.enemigoInfinito(1).rafaga} ola 40=${G.enemigoInfinito(40).rafaga}`);
  check('y tiene tope honesto: nunca pasa de 4',
        [1,10,30,60,120].every(w => G.enemigoInfinito(w).rafaga <= 4),
        [1,10,30,60,120].map(w => w+':'+G.enemigoInfinito(w).rafaga).join(' '));
  // Los topes del infinito no pueden ser los de la campana: la campana termina
  // en 28, el infinito no.
  check('la evasion sigue creciendo pero con techo',
        G.enemigoInfinito(60).evasion > G.enemigoInfinito(10).evasion
        && G.enemigoInfinito(200).evasion <= 0.5,
        `ola 10=${G.enemigoInfinito(10).evasion} ola 60=${G.enemigoInfinito(60).evasion} ola 200=${G.enemigoInfinito(200).evasion}`);
  check('el HP crece pero no sin limite',
        G.enemigoInfinito(60).hp > G.enemigoInfinito(10).hp
        && G.enemigoInfinito(300).hp <= G.enemigoInfinito(400).hp,
        `ola 10=${G.enemigoInfinito(10).hp} ola 60=${G.enemigoInfinito(60).hp}`);
  // El intervalo de ataque no puede llegar a 0: sin pausa entre andanadas no
  // hay nada que leer.
  check('siempre queda una pausa entre andanadas',
        [1,20,60,200].every(w => G.enemigoInfinito(w).atkInterval >= 900),
        [1,20,60,200].map(w => w+':'+G.enemigoInfinito(w).atkInterval).join(' '));
}

// ---------- 4. Historia propia del infinito ----------
seccion('4. el infinito tiene su propia historia, y cierra en bucle');
{
  const G = buildGame({});
  const olas = G.OLAS_HISTORIA;
  check('hay fragmentos y estan en olas concretas',
        Array.isArray(olas) && olas.length >= 8, JSON.stringify(olas));
  check('las olas de historia suben (se leen en orden)',
        olas.every((v, i) => i === 0 || v > olas[i-1]), JSON.stringify(olas));
  check('hay un fragmento temprano, para que no sea un extra desde el arranque',
        olas[0] <= 5, `primero en la ola ${olas[0]}`);
  const ult = G.historiaInfinito(olas[olas.length-1]);
  check('el ultimo fragmento existe y tiene texto',
        ult && typeof ult.titulo === 'string' && ult.texto.length > 20,
        ult ? ult.titulo : 'sin fragmento');
  // El modo no termina: la historia no puede "acabarse" y dejar al jugador sin
  // nada que leer. Dar una vuelta completa (los 8 fragmentos x sus 7 olas) tiene
  // que devolverte al primero.
  const VUELTA = olas.length * 7;
  const primeraVez  = G.fragmentoInfinito(olas[0]);
  const unaVuelta   = G.fragmentoInfinito(olas[0] + VUELTA);
  check('dar una vuelta completa te devuelve al primer fragmento (es un bucle)',
        unaVuelta && unaVuelta.titulo === primeraVez.titulo,
        `vuelta=${VUELTA} olas -> ${unaVuelta ? unaVuelta.titulo : 'nada'} (esperaba ${primeraVez.titulo})`);
  check('y el fragmento que sigue al ultimo sigue siendo del bucle, no null',
        G.fragmentoInfinito(olas[olas.length-1] + 1) !== null,
        JSON.stringify(G.fragmentoInfinito(olas[olas.length-1] + 1)));
  // Y los fragmentos se muestran tras superar la ola, como en la campana.
  check('la ola con fragmento se puede mostrar y saltar',
        typeof G.mostrarHistoriaInfinito === 'function',
        typeof G.mostrarHistoriaInfinito);
}

// ---------- 5. El lazo con las mejoras ----------
seccion('5. el lazo con las mejoras: gemas crecientes y un resumen que sirve');
{
  const G = buildGame({});
  check('las gemas por ola crecen (farmear hondo rinde mas)',
        G.gemasPorOla(30) > G.gemasPorOla(5) && G.gemasPorOla(5) >= G.gemasPorOla(1),
        `ola 1=${G.gemasPorOla(1)} ola 5=${G.gemasPorOla(5)} ola 30=${G.gemasPorOla(30)}`);
  // El resumen de fin de partida tiene que cerrar el lazo: no alcanza con decir
  // "perdiste en la ola X", tiene que decir en que se convirtio lo que juntaste.
  const res = G.resumenMejoras({ arma:3, escudo:2, rapidfire:0, babosas:{fuego:2} });
  check('el resumen dice cuanto te dio cada mejora',
        typeof res === 'string' && /arma/i.test(res) && /escudo/i.test(res),
        typeof res === 'string' ? res.slice(0,90) : typeof res);
  check('y cuanto falta para la siguiente',
        typeof res === 'string' && /falta|proxima|siguiente/i.test(res),
        typeof res === 'string' ? res.slice(0,90) : typeof res);
  check('sin mejoras el resumen no inventa nada',
        typeof G.resumenMejoras({arma:0, escudo:0, rapidfire:0, babosas:{}}) === 'string',
        typeof G.resumenMejoras({arma:0, escudo:0, rapidfire:0, babosas:{}}));
}

// ---------- 6. Logros de olas altas ----------
seccion('6. logros de olas altas');
{
  const G = buildGame({});
  const ids = G.LOGROS.map(l => l.id);
  check('hay logro de la ola 40', ids.includes('ola_40'), ids.filter(i => /^ola_/.test(i)).join(' '));
  check('hay logro de la ola 60', ids.includes('ola_60'), ids.filter(i => /^ola_/.test(i)).join(' '));
  check('los logros de ola vieja siguen ahi',
        ids.includes('ola_10') && ids.includes('ola_25'),
        ids.filter(i => /^ola_/.test(i)).join(' '));
}

// ---------- 7. Lo ya vivido sobrevive el save ----------
seccion('7. lo ya vivido sobrevive el guardado');
{
  const G = buildGame({});
  const s = G.baseSave();
  s.mejorOla = 42;
  const c = G.normalizeSave(s);
  check('el record de ola sobrevive la carga', num(c.mejorOla, -1) === 42, `${c.mejorOla}`);
  const viejo = G.normalizeSave({});
  check('un save viejo arranca en 0 y no rompe',
        num(viejo.mejorOla, -1) === 0, `${viejo.mejorOla}`);
}

console.log('\n' + (mal ? 'ROJO: ' + mal + ' de ' + (ok+mal) + ' checks fallan'
                     : 'VERDE: ' + ok + ' checks'));
if(mal) process.exit(1);
