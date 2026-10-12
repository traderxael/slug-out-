/**
 * Arquetipos — ASIGNACION: la progresion con que se presentan.
 *
 * Los arquetipos se introducen por nivel, no todos de golpe: el jugador tiene que
 * aprender a leer cada uno por separado. Si la asignacion se rompe, el nivel 1
 * podria recibir un frenetico y el jugador no tendria forma de saber que paso.
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
console.log('arquetipos: con que nivel aparece cada uno\n');

const de = (L, idx) => G.enemigoNivel(L, idx).arquetipo;

// ---------- Niveles 1-3: los no-jefes son normales ----------
// Ojo: el JEFE de cada nivel es frenetico en todos los niveles (es su fase 2 al
// 50% de vida), asi que "todo normal" no aplica al ultimo indice.
const normales = [1, 2, 3].every(L => {
  const n = G.NIVELES[L-1].n;
  for (let i = 0; i < n - 1; i++) if (de(L, i) !== 'normal') return false;
  return true;
});
check('niveles 1-3: los enemigos normales son todos normales (sin arquetipos nuevos)',
      normales, `nivel 1: ${[0,1,2,3].map(i=>de(1,i)).join(',')}`);

// ---------- Nivel 4+: aparece el escudado ----------
check('nivel 4 ya trae algun escudado',
      [0,1,2,3,4].some(i => de(4, i) === 'escudado'),
      `idx con escudado: ${[0,1,2,3,4].filter(i => de(4,i)==='escudado').join(',')}`);
check('nivel 3 todavia no trae escudado',
      ![0,1,2,3,4].some(i => de(3, i) === 'escudado'));

// ---------- Nivel 7+: aparece el saltarin ----------
check('nivel 7 ya trae algun saltarin',
      [0,1,2,3,4,5,6].some(i => de(7, i) === 'saltarin'),
      `idx con saltarin: ${[0,1,2,3,4,5,6].filter(i => de(7,i)==='saltarin').join(',')}`);
check('nivel 6 todavia no trae saltarin',
      ![0,1,2,3,4].some(i => de(6, i) === 'saltarin'));

// ---------- El ultimo de cada nivel (jefe) es siempre frenetico ----------
const jefes = [1,2,3,4,5,6,7,8].map(L => de(L, G.NIVELES[L-1].n-1));
check('el jefe de cada nivel es frenetico (se enfurece al 50%)',
      jefes.every(a => a === 'frenetico'), `[${jefes.join(', ')}]`);

// ---------- Modo infinito: mismo criterio por oleada ----------
// Cada 5a oleada es jefe, y el jefe manda: la oleada 10 es frenetico, no saltarin.
check('infinito: oleada 5 (jefe) es frenetico', G.enemigoInfinito(5).arquetipo === 'frenetico');
check('infinito: oleada 8 trae escudado', G.enemigoInfinito(8).arquetipo === 'escudado');
check('infinito: oleada 13 trae saltarin (la 10 es jefe)',
      G.enemigoInfinito(13).arquetipo === 'saltarin',
      `oleada 10 = ${G.enemigoInfinito(10).arquetipo}`);
check('infinito: oleada 1 es normal', G.enemigoInfinito(1).arquetipo === 'normal');

console.log('\n' + (fallos === 0
  ? '=== OK: la progresion de arquetipos es la disenada ==='
  : `=== ${fallos} FALLA(S): la progresion no es la disenada ===`));
process.exit(fallos ? 1 : 0);
