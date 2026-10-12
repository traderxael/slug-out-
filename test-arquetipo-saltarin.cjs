/**
 * Arquetipos de enemigo — SLICE 3: el SALTARIN.
 *
 * Salta solo, en ciclo. Con y creciendo hacia abajo, el impulso es NEGATIVO y la
 * gravedad lo devuelve: si el signo se invierte el enemigo se hunde en el piso y
 * el arquetipo muere en silencio. Por eso el test afirma tambien que vuelve
 * EXACTO al suelo, no solo que "se movio".
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
console.log('arquetipo saltarin: salta solo y vuelve al piso\n');

G.iniciar('nivel', 6);
const st = G.state, en = st.enemy;

// Aislar: que nada mas toque al enemigo durante el ciclo.
en.atkInterval = 1e9; en.evasion = 0; en.dodgeCd = 1e9; en.vx = 0;
en.arquetipo = 'saltarin';
en.aire = false; en.saltoV = 0; en.saltoT = 200;

const sueloY = en.y;          // y de suelo: es el y con el que nacio
let minY = sueloY, aterrizoExacto = false, paso = 0;
for (let i = 0; i < 1200 && paso < 3; i++) {     // ~19s: da para dos saltos
  G.update(16);
  if (en.y < minY) minY = en.y;                  // y chico = mas arriba
  if (paso === 0 && en.aire) paso = 1;
  else if (paso === 1 && !en.aire) {
    if (Math.abs(en.y - sueloY) < 1) aterrizoExacto = true;
    paso = 2;
  }
  else if (paso === 2 && en.aire) paso = 3;
}
const altura = sueloY - minY;

check('el saltarin salta SIN intervencion del jugador', paso >= 1, `paso=${paso}`);
check('aterriza de nuevo (no se queda en el aire)', paso >= 2, `paso=${paso}`);
check('vuelve a saltar (cicla)', paso >= 3, `paso=${paso}`);
check('vuelve EXACTO al suelo (el signo de la gravedad esta bien)',
      aterrizoExacto, `y final ${en.y.toFixed(1)} vs suelo ${sueloY.toFixed(1)}`);
check('el salto es significativo, no un tembleque (>30px)',
      altura > 30, `${altura.toFixed(1)}px de altura`);

console.log('\n' + (fallos === 0
  ? '=== OK: el saltarin salta y aterriza ==='
  : `=== ${fallos} FALLA(S): el saltarin no salta ===`));
process.exit(fallos ? 1 : 0);
