/**
 * Selector de babosas: el jugador ELIGE con que babosas entra al combate, y
 * entran como maximo 3.
 *
 * Por que existe esta suite: `equipoActual()` devolvia TODAS las babosas de la
 * mochila (`ELKEYS.filter(...)`), asi que "elegir babosa" era imposible - la
 * eleccion no existia. Este test fija la regla desde el comportamiento:
 *   1. con las 8 en la mochila, al combate entran 3;
 *   2. cuales 3 se guardan en el save (y sobreviven cargar/guardar);
 *   3. el combate arranca con esas, no con todas;
 *   4. nunca quedas con 0 (el juego necesita al menos una).
 *
 * Lo que NO se toca: el counterplay de potencia ni la esquiva telegrafiada.
 *
 * Para correr: node test-equipo.cjs
 */
const { buildGame } = require('./harness.cjs');

const TODAS = ['fuego','agua','tierra','aire','hielo','rayo','planta','sombra'];
let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};
// Un campo que todavia no existe no debe tumbar el test: lo reporta, no crashea.
const eqs = (e) => Array.isArray(e) ? e.join(',') : String(e);
const len = (e) => Array.isArray(e) ? e.length : -1;
function saveTiene(els){
  const t = {};
  TODAS.forEach(el => { t[el] = [0,0,0,0,0]; });
  els.forEach(el => { t[el][0] = 1; });
  return t;
}
// Un save que tiene exactamente las babosas pedidas. Asi "que babosas entran"
// es una ELECCION y no una consecuencia de lo que tenes.
function saveCon(els){
  const G = buildGame({});
  const s = G.save;
  TODAS.forEach(el => { s.tiene[el] = [0,0,0,0,0]; });
  els.forEach(el => { s.tiene[el][0] = 1; });
  return G;
}
const saveConTodo = () => saveCon(TODAS);

console.log('=== 1. el equipo es una eleccion, no "todo lo que tengo" ===');
{
  const G = saveConTodo();
  const eq = G.equipoActual();
  check('con las 8 en la mochila entran 3 al combate',
        eq.length === 3, `entran ${eq.length} (${eqs(eq)})`);
}

console.log('=== 2. el equipo se guarda y aguanta cargar/guardar ===');
{
  const G = saveConTodo();
  const b = G.baseSave();
  check('el save tiene campo equipo', Array.isArray(b.equipo),
        `equipo = ${JSON.stringify(b.equipo)}`);
  check('el equipo por defecto son 3 babosas propias',
        Array.isArray(b.equipo) && b.equipo.length === 3,
        `son ${b.equipo && b.equipo.length}`);
}
{
  const G = saveConTodo();
  const n  = G.normalizeSave({ tiene: saveTiene(TODAS),           equipo: ['rayo','planta','sombra'] });
  const n2 = G.normalizeSave({ tiene: saveTiene(['fuego','agua']), equipo: ['fuego','agua','rayo'] });
  const n3 = G.normalizeSave({ tiene: saveTiene(TODAS),           equipo: ['fuego','agua','hielo','rayo','planta'] });
  const n4 = G.normalizeSave({ tiene: saveTiene(TODAS),           equipo: ['fuego','fuego','agua'] });
  const n5 = G.normalizeSave({ tiene: saveTiene(TODAS),           equipo: [] });
  const n6 = G.normalizeSave({ tiene: saveTiene(TODAS),           equipo: 'basura' });
  const n7 = G.normalizeSave(n);
  check('guarda el equipo elegido tal cual', eqs(n.equipo) === 'rayo,planta,sombra', eqs(n.equipo));
  check('descarta babosas que ya no tenes',  eqs(n2.equipo) === 'fuego,agua',        eqs(n2.equipo));
  check('recorta a 3 como maximo',            len(n3.equipo) === 3,                `son ${n3.equipo && n3.equipo.length}`);
  check('deduplica',                          eqs(n4.equipo) === 'fuego,agua',       eqs(n4.equipo));
  check('un equipo vacio cae a las primeras 3 que tengas (nunca 0)',
        len(n5.equipo) === 3, `son ${n5.equipo && n5.equipo.length}`);
  check('un equipo corrupto tambien cae a 3', len(n6.equipo) === 3,
        `son ${n6.equipo && n6.equipo.length}`);
  check('sobrevive dos pasadas de normalizeSave', eqs(n7.equipo) === 'rayo,planta,sombra', eqs(n7.equipo));
}

console.log('=== 3. el combate arranca con el equipo elegido ===');
{
  const G = saveConTodo();
  G.save.equipo = ['rayo','planta','sombra'];
  G.iniciar('nivel', 1);
  const eq = G.state.equipo;
  check('el combate trae las 3 elegidas, no las 8 de la mochila',
        eqs(eq) === 'rayo,planta,sombra', `trajo ${eq.length}: ${eqs(eq)}`);
  check('la babosa seleccionada es del equipo', eq.includes(G.state.selected), G.state.selected);
}

console.log('=== 4. para elegir se necesita una funcion y una pantalla ===');
{
  const G = saveConTodo();
  check('existe toggleEquipo para marcar/desmarcar',
        typeof G.toggleEquipo === 'function', `es ${typeof G.toggleEquipo}`);
  check('existe la pantalla del selector', typeof G.renderEquipo === 'function',
        `es ${typeof G.renderEquipo}`);
  G.renderMenu();
  check('el menu ofrece entrar al selector',
        /btnEquipo/.test(G.overlayHTML), 'no hay boton de equipo');
}

console.log('=== 5. las reglas de marcar/desmarcar ===');
{
  const G = saveConTodo();
  G.save.equipo = ['fuego','agua'];
  if (typeof G.toggleEquipo === 'function'){
    G.toggleEquipo('hielo');
    check('agrega cuando hay lugar', eqs(G.save.equipo) === 'fuego,agua,hielo', eqs(G.save.equipo));
  } else check('agrega cuando hay lugar', false, 'toggleEquipo no existe');
}
{
  const G = saveConTodo();
  G.save.equipo = ['fuego','agua','tierra'];
  if (typeof G.toggleEquipo === 'function'){
    G.toggleEquipo('hielo');
    check('rechaza una 4ta babosa', G.save.equipo.length === 3, `quedaron ${G.save.equipo.length}`);
  } else check('rechaza una 4ta babosa', false, 'toggleEquipo no existe');
}
{
  const G = saveConTodo();
  G.save.equipo = ['fuego'];
  if (typeof G.toggleEquipo === 'function'){
    G.toggleEquipo('fuego');
    check('no te deja sin ninguna babosa', G.save.equipo.length >= 1, `quedaron ${G.save.equipo.length}`);
  } else check('no te deja sin ninguna babosa', false, 'toggleEquipo no existe');
}
{
  const G = saveCon(['fuego','agua']);
  G.save.equipo = ['fuego','agua'];
  if (typeof G.toggleEquipo === 'function'){
    G.toggleEquipo('sombra');
    check('rechaza babosas que no tenes', G.save.equipo.length === 2, `quedaron ${G.save.equipo.length}`);
  } else check('rechaza babosas que no tenes', false, 'toggleEquipo no existe');
}
{
  const G = saveConTodo();
  G.save.equipo = ['fuego','agua','tierra'];
  if (typeof G.toggleEquipo === 'function'){
    G.toggleEquipo('agua');
    check('desmarcar libera el lugar', eqs(G.save.equipo) === 'fuego,tierra', eqs(G.save.equipo));
    G.toggleEquipo('hielo');
    check('y el lugar liberado se puede ocupar',
          eqs(G.save.equipo) === 'fuego,tierra,hielo', eqs(G.save.equipo));
  } else check('desmarcar libera el lugar', false, 'toggleEquipo no existe');
}

console.log('');
if (fallos) { console.log(`FALLARON ${fallos} checks`); process.exit(1); }
console.log('OK: el equipo de combate se elige y son 3 como maximo');
