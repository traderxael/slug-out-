/**
 * Arquetipos de enemigo — SLICE 1: el ESCUDADO.
 *
 * El enemigo levanta un escudo (telegrafiado) que ABSORBE el tiro: el proyectil
 * se destruye y el enemigo no recibe daño. Sin esto el escudo seria un numero mas
 * en una barra de vida; con esto es un ritmo que hay que leer y esperar.
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
console.log('arquetipo escudado: el escudo absorbe el tiro\n');

G.iniciar('nivel', 6);
const st = G.state, en = st.enemy;

// Aislar: el rival camina, esquiva y ataca por su propio reloj.
en.atkInterval = 1e9; en.evasion = 0; en.dodgeCd = 1e9;
en.vx = 0; en.x = 180; en.y = 109;
const hp0 = en.hp;

// --- Caso 1: escudo ARRIBA -> el tiro no debe danar ---
// Ojo: escOn no es pegajoso. El ciclo del escudo lo baja cuando escT llega a 0,
// asi que hay que darle duracion o se cae en el primer update.
en.arquetipo = 'escudado';
en.escOn = true; en.escT = 900;

const proy0 = st.projectiles.length;
const disparo = G.fire(0, -100, G.POWER_MAX);
check('el disparo sale con el escudo puesto',
      disparo === true && st.projectiles.length === proy0 + 1);

for (let i = 0; i < 150 && st.projectiles.length > proy0; i++) G.update(16);

check('el proyectil se destruye al tocar el escudo (no rebota ni sigue)',
      st.projectiles.length === proy0,
      `${st.projectiles.length - proy0} proyectiles restantes`);
check('el enemigo NO recibe daño con el escudo puesto', en.hp === hp0,
      `hp ${hp0} -> ${en.hp}`);

// --- Caso 2 (control): escudo ABAJO -> el tiro DEBE danar ---
// Ojo: el primer tiro se resolvio en ~182ms pero el cooldown a potencia maxima
// es 300+320*1.6 = 812ms. Sin dejar pasar el cooldown, el segundo disparo ni
// sale y el control daria un falso negativo.
for (let i = 0; i < 70; i++) G.update(16);   // ~1120ms: vence el cooldown
en.escOn = false;
const disparo2 = G.fire(0, -100, G.POWER_MAX);
check('el segundo disparo sale (el cooldown ya vencio)', disparo2 === true);
for (let i = 0; i < 150 && en.hp === hp0; i++) G.update(16);
check('con el escudo abajo el tiro si dana (el caso 1 no es un falso positivo)',
      en.hp < hp0, `hp ${hp0} -> ${en.hp}`);

console.log('\n' + (fallos === 0
  ? '=== OK: el escudo absorbe el tiro ==='
  : `=== ${fallos} FALLA(S): el escudo no absorbe el tiro ===`));
process.exit(fallos ? 1 : 0);
