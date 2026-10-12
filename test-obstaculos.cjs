/**
 * Obstaculos: rocas que bloquean el tiro.
 *
 * SLICE 1 (el nucleo): una roca en la columna de tiro consume el proyectil y
 * recibe el daño, en vez de dejar pasar el tiro hacia el rival. Esto es lo que
 * convierte el escenario en un problema de timing: la columna libre hay que
 * elegirla, y disparar contra una roca es gastar el tiro.
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
console.log('obstaculos: la roca bloquea el tiro\n');

G.iniciar('nivel', 6);
const st = G.state;
const rocaHp0 = 40, rivalHp0 = st.enemy.hp;

// Roca quieta en la columna de tiro, debajo del rival.
st.rocas = [{ x: 180, y: 300, vy: 0, r: 16, hp: rocaHp0, maxHp: rocaHp0 }];

const proy0 = st.projectiles.length;
const disparo = G.fire(0, -100, G.POWER_MAX);
check('el disparo sale', disparo === true && st.projectiles.length === proy0 + 1);

// Avanzar hasta que ALGO reciba daño: la roca o el rival. El que reciba primero
// dice si la roca bloqueo o si el tiro paso de largo.
let primero = null;
for (let i = 0; i < 150 && primero === null; i++) {
  G.update(16);
  if (st.rocas.length && st.rocas[0].hp < rocaHp0) primero = 'roca';
  else if (st.enemy.hp < rivalHp0) primero = 'rival';
}

check('la roca recibe el daño ANTES que el rival (la roca bloquea el tiro)',
      primero === 'roca', `primero en recibir daño: ${primero}`);

check('el proyectil se destruye al chocar con la roca',
      st.projectiles.length === proy0,
      `${st.projectiles.length - proy0} proyectiles restantes`);

console.log('\n' + (fallos === 0
  ? '=== OK: la roca bloquea el tiro ==='
  : `=== ${fallos} FALLA(S): la roca no bloquea el tiro ===`));
process.exit(fallos ? 1 : 0);
