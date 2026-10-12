/**
 * Verifica el tradeoff de la esquiva telegrafiada por CALCULO, no por lectura.
 *
 * Extrae las constantes REALES de index.html y comprueba la cadena de tiempos
 * que sostiene el diseno: el rival detecta la amenaza a 160px, se agacha
 * `dodgeWindupMax` ms y recien despues se mueve `dodgeT` ms. Un tiro que llegue
 * dentro de esa ventana INTERRUMPE la esquiva.
 *
 * Lo que se afirma: la potencia decide si el tiro llega a tiempo. Mas carga =
 * mas fiable contra la evasion. Si la curva se invirtiera, el tradeoff seria
 * al reves y la mecanica no significaria nada.
 */
const fs = require('fs');
const html = fs.readFileSync(__dirname + '/index.html', 'utf8');

const num = (re, label) => {
  const m = html.match(re);
  if (!m) throw new Error('no encontre ' + label);
  return parseFloat(m[1]);
};

const V0             = num(/const V0 = ([\d.]+)/, 'V0');
const GRAV_FUEGO     = num(/const GRAV = \{fuego:([\d.]+)/, 'GRAV.fuego');
const POWER_MIN      = num(/POWER_MIN = ([\d.]+)/, 'POWER_MIN');
const POWER_MAX      = num(/POWER_MAX = ([\d.]+)/, 'POWER_MAX');
const WINDUP         = num(/dodgeWindupMax:(\d+)/, 'dodgeWindupMax');
const DODGE_T        = num(/en\.dodgeT = (\d+)/, 'dodgeT');
const DETECT_DY      = num(/if\(dy>0 && dy<(\d+)/, 'umbral de deteccion');
const MARGEN_COLISION= num(/en\.r\*1\.4\+5/, 'margen de colision') ? 0 : 0; // documental

// El elemento por defecto es equipo[0] = ELKEYS[0] = 'fuego' (VEL solo sobreescribe 'rayo')
const vel = 0.56 * 1;   // V0 * potencia 1

console.log('constantes extraidas del archivo real:');
console.log(`  V0=${V0}  GRAV.fuego=${GRAV_FUEGO}  POWER ${POWER_MIN}..${POWER_MAX}`);
console.log(`  deteccion a dy<${DETECT_DY}px  viento=${WINDUP}ms  esquiva=${DODGE_T}ms`);
console.log(`  ventana de interrupcion total = ${WINDUP + DODGE_T}ms\n`);

/**
 * Tiempo que tarda el proyectil en recorrer `dy` px hacia arriba, CON gravedad.
 * y(t) = y0 - v*t + 0.5*g*t^2  =>  0.5*g*t^2 - v*t + dy = 0  (raiz menor)
 */
function tiempoDeVuelo(potencia, dy) {
  const v = V0 * potencia, g = GRAV_FUEGO;
  const disc = v * v - 2 * g * dy;
  if (disc <= 0) return Infinity;          // no llega
  return (v - Math.sqrt(disc)) / g;
}

const VENTANA = WINDUP + DODGE_T;
let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};

console.log('tiempo de vuelo desde que el rival detecta la amenaza:');
const casos = [
  ['MAXIMA (1.60)', POWER_MAX],
  ['media   (1.00)', 1.00],
  ['MINIMA (0.55)', POWER_MIN],
];
const t = {};
for (const [nombre, p] of casos) {
  t[nombre] = tiempoDeVuelo(p, DETECT_DY);
  const fase = t[nombre] <= WINDUP ? 'durante el tell (congelado)'
            : t[nombre] <= VENTANA ? 'durante el movimiento de esquiva'
            : 'DESPUES: la esquiva ya completo';
  console.log(`  ${nombre}: ${t[nombre].toFixed(0).padStart(4)}ms  -> ${fase}`);
}

console.log('\nel tradeoff se sostiene:');
check('a potencia MAXIMA el tiro llega durante el tell (el rival esta quieto)',
      t['MAXIMA (1.60)'] < WINDUP,
      `${t['MAXIMA (1.60)'].toFixed(0)}ms < ${WINDUP}ms`);
check('a potencia media llega dentro de la ventana de interrupcion',
      t['media   (1.00)'] < VENTANA,
      `${t['media   (1.00)'].toFixed(0)}ms < ${VENTANA}ms`);
check('a potencia MINIMA llega tarde: la esquiva ya se completo',
      t['MINIMA (0.55)'] > VENTANA,
      `${t['MINIMA (0.55)'].toFixed(0)}ms > ${VENTANA}ms`);
check('mas carga llega SIEMPRE antes (monotono)',
      t['MAXIMA (1.60)'] < t['media   (1.00)'] && t['media   (1.00)'] < t['MINIMA (0.55)'],
      'max < media < minima');

// El tell tiene que ser VISIBLE: si durara menos que un frame (~16ms) no se
// podria leer, y si durara mas que el vuelo del tiro mas rapido nunca se
// podria castigar.
check('el tell dura lo suficiente para leerse (>= 3 frames)',
      WINDUP >= 48, `${WINDUP}ms`);
check('el tell es castigable: el tiro mas rapido llega antes de que cierre',
      t['MAXIMA (1.60)'] < WINDUP + DODGE_T);

console.log('\n' + (fallos === 0
  ? '=== OK: el tradeoff carga-vs-evasion es real y calculable ==='
  : `=== ${fallos} FALLA(S): el balance de la esquiva no sostiene el tradeoff ===`));
process.exit(fallos ? 1 : 0);
