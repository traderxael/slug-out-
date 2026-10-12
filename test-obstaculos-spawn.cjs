/**
 * Obstaculos, slice 3: las rocas APARECEN AVISADAS.
 *
 * Una roca que cae de la nada es injusta: no hay decision, solo suerte. Con un
 * tell previo (la roca se marca y tiembla unos instantes antes de soltarse) el
 * jugador LEE el escenario y decide: disparar por otra columna, apurar el tiro,
 * o romper la roca antes de que caiga. Es el mismo principio que la esquiva
 * telegrafiada, aplicado al escenario.
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
console.log('obstaculos: las rocas aparecen avisadas\n');

G.iniciar('nivel', 6);
const st = G.state;

// --- 1. aparecen rocas durante el juego, sin intervencion del jugador ---
let primera = -1;
for (let i = 0; i < 900 && primera < 0; i++) {
  const antes = st.rocas.length;
  G.update(16);
  if (st.rocas.length > antes) primera = i;
}
check('aparecen rocas durante el juego', primera >= 0,
      primera >= 0 ? `primera a los ${(primera + 1) * 16}ms` : 'ninguna en 14.4s');

// --- 2. la roca aparece telegrafiada: marcada, pero todavia no cae ---
let telegrafiada = false, tell = 0;
for (let i = 0; i < 900 && !telegrafiada; i++) {
  const antes = st.rocas.length;
  G.update(16);
  if (st.rocas.length > antes) {
    const ro = st.rocas[st.rocas.length - 1];
    telegrafiada = (ro.tellT > 0 && ro.vy === 0);
    tell = ro.tellT;
  }
}
check('la roca aparece telegrafiada: marcada y quieta antes de caer', telegrafiada,
      `tellT=${tell}`);

// --- 3. pasado el tell, la roca empieza a caer ---
let ro = null;
for (let i = 0; i < 900 && !ro; i++) {
  const antes = st.rocas.length;
  G.update(16);
  if (st.rocas.length > antes) ro = st.rocas[st.rocas.length - 1];
}
let cayo = false, tHastaCaer = -1;
if (ro) {
  for (let i = 0; i < 300 && st.rocas.includes(ro) && !cayo; i++) {
    G.update(16);
    if (ro.vy > 0) { cayo = true; tHastaCaer = i * 16; }
  }
}
check('pasado el tell, la roca empieza a caer', cayo,
      tHastaCaer >= 0 ? `cayo ${tHastaCaer}ms despues de aparecer` : 'nunca cayo');

console.log('\n' + (fallos === 0
  ? '=== OK: las rocas aparecen avisadas ==='
  : `=== ${fallos} FALLA(S) ===`));
process.exit(fallos ? 1 : 0);
