// Extrae las funciones de potencia DEL ARCHIVO REAL y valida el tradeoff.
const fs = require('fs');
const SRC = 'C:/Users/USUARIO/slug-out-/index.html';
const html = fs.readFileSync(SRC, 'utf8');

// --- extraer el bloque de potencia tal como está en el archivo ---
const bloque = html.match(/const POWER_MIN[\s\S]*?function powerMult\(p\)\{ return 0\.55 \+ 0\.45\*p; \}/);
if (!bloque) { console.error('FALLO: no se encontró el bloque de potencia en el archivo'); process.exit(1); }
const code = bloque[0];
console.log('--- bloque extraído del archivo real ---');
console.log(code);
console.log('----------------------------------------\n');

// --- extraer la fórmula de daño real de applyHit ---
const dmgLine = html.match(/const dmg = Math\.round\(16\*mult\*comboMult\*critMult\*rm\*eclipseMult[^;]*;/);
if (!dmgLine) { console.error('FALLO: no se encontró la fórmula de daño'); process.exit(1); }
console.log('fórmula de daño real:', dmgLine[0], '\n');

const sandbox = {};
new Function('exports', code + '\nexports.POWER_MIN=POWER_MIN;exports.POWER_MAX=POWER_MAX;' +
  'exports.powerFromDrag=powerFromDrag;exports.cooldownFor=cooldownFor;' +
  'exports.cargaMaxima=cargaMaxima;exports.powerMult=powerMult;')(sandbox);
const { POWER_MIN, POWER_MAX, powerFromDrag, cooldownFor, cargaMaxima, powerMult } = sandbox;

// daño base (mult=1, comboMult=1, critMult=1, rm=1, eclipseMult=1) con la fórmula real
function dmg(p){
  const max = cargaMaxima(p);
  return Math.round(16 * powerMult(p) * (max ? 1.25 : 1));
}
function dps(p){ return dmg(p) / (cooldownFor(p) / 1000); }

let fails = 0;
function check(nombre, cond, detalle){
  console.log((cond ? '  OK  ' : ' FALLO') + '  ' + nombre + (detalle ? '   [' + detalle + ']' : ''));
  if (!cond) fails++;
}

console.log('== 1. el arrastre controla la potencia (monótona y acotada) ==');
check('arrastre 10px = potencia mínima', powerFromDrag(0,-10) === POWER_MIN, 'p=' + powerFromDrag(0,-10));
check('arrastre 26px = potencia mínima', powerFromDrag(0,-26) === POWER_MIN, 'p=' + powerFromDrag(0,-26));
check('arrastre 176px = potencia máxima', powerFromDrag(0,-176) === POWER_MAX, 'p=' + powerFromDrag(0,-176));
check('arrastre 900px satura en el máximo', powerFromDrag(0,-900) === POWER_MAX);
let mono = true, prev = -1;
for (let d = 0; d <= 300; d += 5){ const p = powerFromDrag(0,-d); if (p < prev - 1e-9) mono = false; prev = p; }
check('la potencia crece de forma monótona con el arrastre', mono);
check('la potencia siempre queda en [MIN,MAX]', powerFromDrag(0,-60) >= POWER_MIN && powerFromDrag(0,-60) <= POWER_MAX);

console.log('\n== 2. existe un tradeoff real: más carga = más daño Y recarga más larga ==');
console.log('  potencia | daño | recarga | DPS');
[POWER_MIN, 0.8, 1.0, 1.3, POWER_MAX].forEach(p=>{
  console.log('   ' + p.toFixed(2) + '    |  ' + String(dmg(p)).padStart(3) +
              '  |  ' + String(cooldownFor(p)).padStart(3) + 'ms |  ' + dps(p).toFixed(1));
});
check('más potencia => más daño', dmg(POWER_MAX) > dmg(POWER_MIN),
      dmg(POWER_MIN) + ' -> ' + dmg(POWER_MAX) + '  (x' + (dmg(POWER_MAX)/dmg(POWER_MIN)).toFixed(2) + ')');
check('más potencia => recarga más larga', cooldownFor(POWER_MAX) > cooldownFor(POWER_MIN),
      cooldownFor(POWER_MIN) + 'ms -> ' + cooldownFor(POWER_MAX) + 'ms');
check('la recarga base NO cambia (sigue siendo fluida)', cooldownFor(1) <= 700, cooldownFor(1) + 'ms');

console.log('\n== 3. la carga máxima desbloquea el bonus (no es solo daño) ==');
check('carga máxima se detecta en el tope del rango', cargaMaxima(POWER_MAX) === true);
check('carga máxima NO se detecta por debajo del tope', cargaMaxima(POWER_MAX - 0.1) === false);
check('el bonus de carga máxima es significativo', dmg(POWER_MAX) / dmg(1.0) >= 1.5,
      'x' + (dmg(POWER_MAX)/dmg(1.0)).toFixed(2) + ' vs un tiro normal');
check('un tiro sin cargar es más débil que uno normal', dmg(POWER_MIN) < dmg(1.0),
      dmg(POWER_MIN) + ' < ' + dmg(1.0));

console.log('\n== 4. el combo caduca (hay presión, no se puede farmear) ==');
const tieneComboT = /state\.comboT = 5000/.test(html) && /state\.comboT -= dt/.test(html);
check('el combo tiene temporizador de caducidad de 5s', tieneComboT);

console.log('\n== 5. los controles nuevos están conectados ==');
check('pausa por teclado P', /k==='p'\|\|k==='P'\|\|k==='Escape'/.test(html));
check('disparo por Espacio', /dispararConTeclado\(\)/.test(html));
check('potencia por flechas ↑↓', /keys\['ArrowUp'\]/.test(html));
check('la previsualización usa la potencia real', /const v0 = vel\(state\.selected\)\*pw/.test(html));
check('silencio persistente', /if\(save\.mute\) return;/.test(html) && /o\.mute = s\.mute/.test(html));
check('pausa automática al cambiar de pestaña', /visibilitychange/.test(html));
check('bug de desafío mejorOla corregido (max, no suma)', /d\.max\s*\n\s*\? Math\.min\(d\.target, Math\.max/.test(html));
check('el contador de logros del menu se calcula (no hardcodeado)',
      /potencia de tiro.*LOGROS\.length/.test(html) || /'+LOGROS\.length\+' logros/.test(html));

console.log('\n' + (fails === 0 ? '=== TODOS LOS CHECKS PASARON ===' : '=== ' + fails + ' CHECK(S) FALLARON ==='));
process.exit(fails === 0 ? 0 : 1);
