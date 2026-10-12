/**
 * Habilidades del villano — CONSECUENCIAS sobre el jugador.
 *
 * test-habilidades.cjs prueba que cada elemento LANZA su habilidad. Este prueba
 * que la habilidad SE SIENTE: que aturdirte te impida disparar, que el hielo te
 * baje la carga (y no sea igual que el aturdimiento), y que la marea te alargue
 * la recarga.
 *
 * Corre contra el index.html real via harness.cjs.
 */
const { buildGame } = require('./harness.cjs');

let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};

console.log('habilidades del villano: lo que le hacen al jugador\n');

// ---------- Aturdido (tierra): no podes disparar ----------
{
  const G = buildGame();
  G.iniciar('nivel', 6);
  const st = G.state;
  st.enemy.atkInterval = 1e9; st.rocaTimer = 1e9; st.rocas = [];
  st.cooldown = 0; st.player.stunT = 0;

  check('sin estados: el tiro sale', G.fire(0, -100, 1) === true);

  st.cooldown = 0; st.player.stunT = 500;
  check('aturdido: el tiro NO sale', G.fire(0, -100, 1) === false);

  // Al pasar el aturdimiento vuelve a salir (no queda pegado).
  for (let i = 0; i < 40; i++) G.update(16);
  st.cooldown = 0;
  check('al pasar el aturdimiento vuelve a salir', G.fire(0, -100, 1) === true);
}

// ---------- Congelado (hielo): no dispara Y te baja la carga ----------
{
  const G = buildGame();
  G.iniciar('nivel', 6);
  const st = G.state;
  st.enemy.atkInterval = 1e9; st.rocaTimer = 1e9; st.rocas = [];

  st.cooldown = 0;
  G.fire(0, -100, G.POWER_MAX);                 // carga al maximo
  check('la carga quedo al maximo',
        Math.abs(st.aimPower - G.POWER_MAX) < 0.01, `aimPower=${st.aimPower}`);

  st.enemy.tipo = 'hielo';
  st.enemy.habTell = 1;                          // se lanza en el proximo update
  G.update(16);
  check('congelado te BAJA la carga (no es igual que aturdirte)',
        Math.abs(st.aimPower - G.POWER_MIN) < 0.01, `aimPower=${st.aimPower}`);
  st.cooldown = 0;
  check('congelado: el tiro NO sale', G.fire(0, -100, 1) === false);
}

// ---------- Marea (agua): te alarga la recarga ----------
{
  const G = buildGame();
  G.iniciar('nivel', 6);
  const st = G.state;
  st.enemy.atkInterval = 1e9; st.rocaTimer = 1e9; st.rocas = [];

  const medirRecarga = (lento) => {
    st.player.slowT = lento ? 2000 : 0;
    st.cooldown = 0;
    G.fire(0, -100, 1);
    return st.cooldownMax;
  };
  const normal = medirRecarga(false);
  const lenta  = medirRecarga(true);
  check('la marea alarga la recarga 1.6x',
        lenta > normal * 1.55, `normal ${normal}ms vs con marea ${lenta}ms`);
}

// ---------- Charco (fuego): quema mientras sigas ahi ----------
{
  const G = buildGame();
  G.iniciar('nivel', 6);
  const st = G.state;
  st.enemy.atkInterval = 1e9; st.rocaTimer = 1e9; st.rocas = [];

  st.enemy.tipo = 'fuego';
  st.enemy.habTell = 1;
  G.update(16);
  check('el charco aparece bajo tus pies', st.zonas.length === 1);

  const hp0 = st.player.hp;
  for (let i = 0; i < 60; i++) G.update(16);    // ~1s parado encima
  check('estar en el charco TE quema', st.player.hp < hp0,
        `hp ${hp0} -> ${st.player.hp}`);

  // Al terminarse el charco deja de doler.
  const zonasAntes = st.zonas.length;
  for (let i = 0; i < 300; i++) G.update(16);   // ~4.8s: se agota
  check('el charco se termina (no es permanente)', st.zonas.length < zonasAntes,
        `${zonasAntes} -> ${st.zonas.length} charcos`);
}

console.log('\n' + (fallos === 0
  ? '=== OK: las habilidades se sienten en el juego ==='
  : `=== ${fallos} FALLA(S): alguna habilidad no afecta al jugador ===`));
process.exit(fallos ? 1 : 0);
