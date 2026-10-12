/**
 * Obstaculos, slice 2: la roca CAE.
 *
 * Una roca quieta seria un obstaculo estatico: se memoriza la columna y listo.
 * Cayendo, la columna libre cambia con el tiempo y el escenario pasa a ser un
 * problema de timing. Ademas es lo que permite que la roca llegue al piso y
 * presione al jugador.
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
console.log('obstaculos: la roca cae\n');

G.iniciar('nivel', 6);
const st = G.state;

// --- 1. la roca cae ---
st.rocas = [{ x: 180, y: 200, vy: 0, r: 16, hp: 40, maxHp: 40 }];
const y0 = st.rocas[0].y;
for (let i = 0; i < 30; i++) G.update(16);
const caida = st.rocas.length ? st.rocas[0].y - y0 : -1;
check('la roca cae con el tiempo', caida > 20, `bajo ${caida}px en 480ms`);

check('la caida acelera (no es velocidad constante)',
      (() => {
        st.rocas = [{ x: 180, y: 200, vy: 0, r: 16, hp: 40, maxHp: 40 }];
        G.update(16); const v1 = st.rocas[0].vy;
        G.update(16); const v2 = st.rocas[0].vy;
        return v2 > v1;
      })());

// --- 2. la roca se rompe al agotar su hp ---
st.rocas = [{ x: 180, y: 300, vy: 0, r: 16, hp: 5, maxHp: 40 }];
G.fire(0, -100, G.POWER_MAX);
for (let i = 0; i < 150 && st.rocas.length; i++) G.update(16);
check('la roca se rompe y desaparece al agotar su hp', st.rocas.length === 0,
      `${st.rocas.length} rocas restantes`);

// --- 3. la roca se deshace al llegar al piso ---
st.rocas = [{ x: 180, y: 400, vy: 0, r: 16, hp: 40, maxHp: 40 }];
for (let i = 0; i < 300 && st.rocas.length; i++) G.update(16);
check('la roca se deshace al llegar al piso (no queda flotando)', st.rocas.length === 0,
      `${st.rocas.length} rocas restantes`);

console.log('\n' + (fallos === 0
  ? '=== OK: la roca cae y se rompe ==='
  : `=== ${fallos} FALLA(S) ===`));
process.exit(fallos ? 1 : 0);
