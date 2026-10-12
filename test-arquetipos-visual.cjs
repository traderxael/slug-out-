/**
 * Arquetipos — VERIFICACION DE LO DIBUJADO.
 *
 * Un arquetipo que funciona pero no se ve es mecanica muerta: el jugador no
 * tiene nada que leer. Este test comprueba que cada uno pinta su marca.
 *
 * NO cuenta pixeles por color: el escudo cian (127,212,255) y el proyectil de
 * hielo (#6fd6e6) son casi el mismo tono, asi que un conteo daria un falso
 * positivo permanente. En cambio usa el modo de grabacion del harness, que anota
 * el estilo vigente en cada stroke()/fill(): se verifica QUE se dibuja.
 *
 * Corre contra el index.html real via harness.cjs.
 */
const { buildGame } = require('./harness.cjs');

let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};

// Busca en el log de dibujo algun estilo que contenga el patron dado.
const pintoCon = (log, patron) =>
  log.some(e => String(e.stroke || '').includes(patron) ||
                String(e.fill   || '').includes(patron));

const G = buildGame({ recordDraw: true });
const log = G.__drawLog;
console.log('arquetipos: lo que se DIBUJA de cada uno\n');

// ---------- Escudado: anillo cian cuando el escudo esta arriba ----------
G.iniciar('nivel', 6);
let en = G.state.enemy;
en.arquetipo = 'escudado'; en.escOn = true; en.escT = 900; en.escWindup = 0;
log.length = 0;
G.draw();
check('escudo arriba -> pinta el anillo cian (127,212,255)',
      pintoCon(log, '127,212,255'),
      `${log.filter(e => String(e.stroke||'').includes('127,212,255')).length} trazos cian`);

// ---------- Escudado: el tell del escudo tambien se ve ----------
log.length = 0;
en.escOn = false; en.escWindup = 400;      // a mitad del aviso
G.draw();
check('aviso del escudo -> pinta el anillo que se cierra',
      pintoCon(log, '127,212,255'), 'el tell usa el mismo cian');

// ---------- Saltarin: la agachada antes de saltar se ve ----------
G.iniciar('nivel', 6);
en = G.state.enemy;
en.arquetipo = 'saltarin'; en.saltoWindup = 130; en.aire = false;
log.length = 0;
G.draw();
check('agachada antes de saltar -> pinta la elipse gris (185,198,214)',
      pintoCon(log, '185,198,214'),
      `${log.filter(e => String(e.stroke||'').includes('185,198,214')).length} trazos`);

// ---------- Frenetico: el aura roja se ve ----------
G.iniciar('nivel', 6);
en = G.state.enemy;
en.arquetipo = 'frenetico'; en.furia = true;
log.length = 0;
G.draw();
check('enfurecido -> pinta el aura roja (255,95,109)',
      pintoCon(log, '255,95,109'),
      `${log.filter(e => String(e.stroke||'').includes('255,95,109')).length} trazos`);

// ---------- Control: un enemigo normal NO pinta marcas de arquetipo ----------
G.iniciar('nivel', 6);
en = G.state.enemy;
en.arquetipo = 'normal'; en.escOn = false; en.escWindup = 0;
en.saltoWindup = 0; en.furia = false;
log.length = 0;
G.draw();
const marcas = log.filter(e =>
  String(e.stroke || '').includes('127,212,255') ||
  String(e.stroke || '').includes('255,95,109')   ||
  String(e.stroke || '').includes('185,198,214')).length;
check('un enemigo normal NO pinta ninguna marca de arquetipo',
      marcas === 0, `${marcas} trazos de arquetipo`);

console.log('\n' + (fallos === 0
  ? '=== OK: los tres arquetipos se ven ==='
  : `=== ${fallos} FALLA(S): algun arquetipo no se dibuja ===`));
process.exit(fallos ? 1 : 0);
