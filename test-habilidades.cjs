/**
 * Habilidades elementales del villano.
 *
 * Cada villano usa, durante el combate, la habilidad de SU elemento: la misma
 * simetria que tienen las babosas del jugador. El escudado te absorbe el tiro,
 * el de fuego te deja ardiendo, el de sombra te drena la vida.
 *
 * El jugador NO se mueve (LX fijo), asi que cada habilidad se responde con
 * CUANDO tirar, con QUE elemento y CUANTO cargar — no esquivando.
 *
 * Corre contra el index.html real via harness.cjs.
 */
const { buildGame } = require('./harness.cjs');

let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};

// Que se espera ver de cada elemento, y como se observa.
const CASOS = [
  { tipo:'fuego',  que:'charco ardiente que TE quema',
    vio:(st) => st.zonas && st.zonas.length > 0 },
  { tipo:'agua',   que:'marea que TE ralentiza los tiros',
    vio:(st) => st.player.slowT > 0 },
  { tipo:'tierra', que:'golpe que TE aturde (sin disparar)',
    vio:(st) => st.player.stunT > 0 },
  { tipo:'aire',   que:'abanico de tiros que te perfora',
    vio:(st) => st.enemyShots.length >= 3 },
  { tipo:'hielo',  que:'TE congela (y te baja la carga)',
    vio:(st) => st.player.freezeT > 0 },
  { tipo:'rayo',   que:'descarga doble rapida',
    vio:(st) => st.enemyShots.length >= 2 },
  { tipo:'planta', que:'se regenera curandose',
    vio:(st,en) => en.hp > en.hpInicial },
  { tipo:'sombra', que:'te drena vida y se cura con ella',
    vio:(st) => st.player.hp < st.player.maxHp }
];

console.log('habilidades elementales del villano\n');

for (const c of CASOS){
  const G = buildGame();
  G.iniciar('nivel', 6);
  const st = G.state, en = st.enemy;

  // Aislar: el escupitajo normal corre por su propio reloj, y las rocas caen
  // solas y le pegan al jugador (-12). Las dos contaminarian la medicion.
  en.atkInterval = 1e9;
  st.rocaTimer = 1e9; st.rocas = [];
  en.tipo = c.tipo;
  en.habCd = 200; en.habTell = 0; en.habTimer = 0;

  // Planta y sombra se curan: hay que darles un motivo (vida bajada) para que la
  // curacion sea observable y no un falso positivo de "ya estaba lleno".
  // La referencia se toma DESPUES de bajarles la vida: si se toma antes, curarse
  // hasta el maximo nunca la supera y el test no puede pasar.
  if (c.tipo === 'planta' || c.tipo === 'sombra') en.hp = Math.round(en.maxHp * 0.6);
  en.hpInicial = en.hp;

  let vioTell = false, paso = false;
  for (let i = 0; i < 400 && !paso; i++){        // 6.4s de combate
    G.update(16);
    if (en.habTell > 0) vioTell = true;
    if (c.vio(st, en)) paso = true;
  }

  check(`${c.tipo.padEnd(7)} — ${c.que}`, paso && vioTell,
        paso ? (vioTell ? 'con aviso previo' : 'SIN aviso previo') : 'nunca se activo');
}

console.log('\n' + (fallos === 0
  ? '=== OK: los 8 elementos tienen habilidad en combate ==='
  : `=== ${fallos} FALLA(S): faltan habilidades ===`));
process.exit(fallos ? 1 : 0);
