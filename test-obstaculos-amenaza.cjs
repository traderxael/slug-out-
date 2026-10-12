/**
 * Obstaculos, slice 4: la roca AMENAZA al jugador.
 *
 * Hasta aca la roca solo estorbaba el tiro. Para que el escenario presione de
 * verdad, una roca que cae por la columna del jugador lo golpea al cruzar su
 * linea — igual que los escupitajos del rival. Ahora hay que mirar dos cosas a
 * la vez: por donde disparar y que esta cayendo.
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
console.log('obstaculos: la roca amenaza al jugador\n');

G.iniciar('nivel', 6);
const st = G.state;
// Aislar la variable bajo prueba: el rival ataca dentro de la ventana de
// medicion y su escupitajo contaminaria el HP. Con un intervalo absurdo nunca
// ataca, asi que el unico dano posible viene de la roca.
st.enemy.atkInterval = 1e9;

// --- 1. una roca que cae por la columna del jugador lo golpea ---
const hp0 = st.player.hp;
st.rocas = [{ x: 180, y: 300, vy: 0, r: 16, hp: 40, maxHp: 40, tellT: 0 }];
let golpeo = false;
for (let i = 0; i < 120 && !golpeo; i++) { G.update(16); golpeo = st.player.hp < hp0; }
check('una roca que cae sobre el jugador lo golpea', golpeo,
      `hp ${hp0} -> ${st.player.hp}`);

// --- 2. una roca que cae por otra columna no lo golpea ---
st.rocas = [{ x: 60, y: 300, vy: 0, r: 16, hp: 40, maxHp: 40, tellT: 0 }];
const hp1 = st.player.hp;
for (let i = 0; i < 120; i++) G.update(16);
check('una roca que cae por otra columna no lo golpea', st.player.hp === hp1,
      `hp ${hp1} -> ${st.player.hp}`);

console.log('\n' + (fallos === 0
  ? '=== OK: la roca amenaza al jugador ==='
  : `=== ${fallos} FALLA(S) ===`));
process.exit(fallos ? 1 : 0);
