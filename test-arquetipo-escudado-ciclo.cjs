/**
 * Arquetipos de enemigo — SLICE 2: el escudo SUBE SOLO y TELEGRAFIADO.
 *
 * El slice 1 probo que el escudo absorbe. Este prueba el ciclo que lo hace
 * jugable: el escudo se levanta sin que el jugador haga nada, pero AVISA antes
 * (escWindup), dura ESC_DUR y vuelve a bajar. Sin el aviso seria suerte; con el
 * aviso es un ritmo que se lee y se espera.
 *
 * Corre contra el index.html real via harness.cjs.
 */
const { buildGame } = require('./harness.cjs');

let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};

const G = buildGame();
console.log('arquetipo escudado: el escudo sube solo y telegrafiado\n');

G.iniciar('nivel', 6);
const st = G.state, en = st.enemy;

// Aislar: que nada mas toque al enemigo durante el ciclo.
en.atkInterval = 1e9; en.evasion = 0; en.dodgeCd = 1e9; en.vx = 0;
en.arquetipo = 'escudado';
en.escCd = 300; en.escOn = false; en.escWindup = 0; en.escT = 0;

// Maquina de estados: 0 sin escudo -> 1 escudo arriba -> 2 bajo -> 3 volvio a subir
let paso = 0, vioWindup = false, tArriba = 0;
for (let i = 0; i < 900 && paso < 3; i++) {     // ~14.4s: da para dos ciclos
  G.update(16);
  if (!en.escOn && en.escWindup > 0) vioWindup = true;
  if (paso === 0 && en.escOn) paso = 1;
  else if (paso === 1 && !en.escOn) paso = 2;
  else if (paso === 2 && en.escOn) paso = 3;
  if (paso === 1) tArriba += 16;
}

check('el escudo sube SIN intervencion del jugador', paso >= 1, `paso=${paso}`);
check('hubo aviso (escWindup) antes de que subiera — se lee, no se adivina', vioWindup);
check('el escudo baja solo (no se queda para siempre)', paso >= 2, `paso=${paso}`);
check('vuelve a subir (cicla de verdad)', paso >= 3, `paso=${paso}`);
check('el escudo dura ~900ms (ESC_DUR)', Math.abs(tArriba - 900) < 250, `${tArriba}ms`);

console.log('\n' + (fallos === 0
  ? '=== OK: el escudo cicla telegrafiado ==='
  : `=== ${fallos} FALLA(S): el escudo no cicla ===`));
process.exit(fallos ? 1 : 0);
