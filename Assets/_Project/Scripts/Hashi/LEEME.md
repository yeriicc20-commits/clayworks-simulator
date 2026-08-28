# Hashi-Watashi (máquina de puente)

Juego completo y aparte del simulador de tienda: una recreativa japonesa en la
que el premio **no se coge**, se **tira**. La caja descansa sobre dos barras
paralelas y hay que ir girándola con las dos pinzas hasta que pierde el
equilibrio y cae por el hueco.

## Dentro del local

Para que sea una máquina más de la tienda, comprable y colocable como la de
garra:

1. **ClayWorks → Hashi-Watashi → Crear prefab para el local**
   Deja `Assets/_Project/Prefabs/Machines/MaquinaPuente.prefab`, con
   `PlaceableObject`, `MachinePricing` y el disparador de «E: jugar». Se monta en
   una escena de trabajo aparte, así que no ensucia la que tengas abierta.
2. **ClayWorks → Hashi-Watashi → Ponerla a la venta en el local**
   Añade la ficha al catálogo del ordenador en `Local_01` (550 €), copiando la
   caja de reparto de las máquinas que ya se venden, y guarda la escena. Si el
   prefab no existe todavía, lo monta antes.

El icono de la tienda se lo saca solo `IconosTienda` a partir del prefab.

Dentro del local **no hay fichas ni panel de créditos**: se paga con el dinero
del negocio, con la tecla `Usar` (E de fábrica), y se juega con las teclas de
garra que el jugador tenga puestas en los ajustes (IJKL + espacio de fábrica),
las mismas que la otra máquina. La `P` abre el panel de precios. Al ganar,
`HashiPrizePayout` paga el valor del premio, suma XP y repone la caja.

### Lo que todavía no hace

**Los clientes NPC no la juegan.** `NPCClawPlayer` y `NPCManosMaquina` están
escritos contra el tipo `ClawController` de la máquina de garra
(`ClawController.AllMachines`, `PickMachine`, `PlayAtMachine`…), así que ni la
ven. Para que la usen habría que sacar una interfaz común de máquina y pasar por
ella esos dos scripts, que son unas 1.000 líneas y las que hacen funcionar la
máquina que ya va. Es un trabajo aparte y con riesgo de tocar lo que funciona.

De momento, la de puente la juega el jugador; el negocio lo siguen sosteniendo
las de garra.

## La escena de pruebas

En Unity, menú **ClayWorks → Hashi-Watashi → Montar escena**.

Crea las capas que faltan, los materiales, el prefab del premio, las 6 cajas,
las 4 dificultades, monta la máquina entera y guarda
`Assets/Scenes/Hashi_Watashi.unity`. Pregunta antes si hay algo sin guardar.

Es idempotente: se puede volver a pulsar tantas veces como haga falta.

- **Rehacer solo los assets** — regenera materiales, cajas y dificultades sin
  tocar ninguna escena.
- **Pruebas → …** — poner otra caja, devolverla a su sitio, cambiar de
  dificultad en caliente, ver el centro de masas, medir el hueco.

## Controles (solo en la escena de pruebas)

Dentro del local manda `AjustesControles`, no esta tabla.

| Tecla | Qué hace |
|---|---|
| `ENTER` | Meter crédito y empezar el turno |
| `WASD` / flechas | Mover la garra |
| `ESPACIO` | Soltar (a partir de ahí la máquina termina sola) |
| `1` `2` `3` | Cámara frontal / en ángulo / cenital |
| `C` | Crédito extra (para probar) |
| `R` | Reiniciar la partida entera |
| `F1` | Modo depuración |

Mando compatible: stick izquierdo para mover, botón sur o gatillo derecho para
soltar.

## Cómo se gana

Solo hay una manera, y no se puede forzar por script:

1. Las pinzas empujan la caja y le meten par.
2. La caja gira sobre las barras y se va poniendo de canto.
3. Pasado cierto ángulo, su centro de masas se sale del apoyo y cae sola.
4. Pasa entera por el hueco.
5. `DropZone` comprueba que está **completa** por debajo de las barras, sobre la
   bandeja y parada. Entonces, y solo entonces, cuenta.

Al fallar, **la caja se queda exactamente como esté**. El crédito siguiente parte
de ahí: es la mitad del juego.

## Los números que importan

Están todos en `Assets/_Project/Scripts/Editor/HashiWatashiBuilder.cs`, arriba
del todo, y se reparten solos al montar. Los dos que mandan:

- **`BARRA_SEPARACION` (0,17 m)** — con el radio de 8 mm deja un hueco libre de
  0,154 m. Una caja solo puede caer si su **altura** es menor que ese hueco, y
  solo se apoya si su **anchura** es mayor que la separación. `HashiAssets`
  comprueba las 6 cajas contra las 4 dificultades al generar y avisa si alguna
  combinación es injugable.
- **`GARRA_MINIMA` (0,52 m)** — hasta dónde baja la garra. Está calculada para
  que los dedos abiertos bajen **por fuera** de la caja más ancha, con 7 mm de
  margen. Bajarla más pone el dedo encima de la tapa, y como el cuerpo de la
  garra es cinemático, aplastaría la caja contra las barras en vez de empujarla.

Para afinar el tacto, en orden de cuánto se nota:

1. `gripForce` (par del motor de cada pinza, N·m) — 0,45 empuja del orden del
   peso de la caja. A 2 la vuelca de un golpe.
2. `closeSpeed` — rápido da un toque seco, lento empuja.
3. `friction` de la caja — mucho hace que pivote, poco que resbale sin girar.
4. `centerOfMassOffset` — lo que decide si vuelca fácil.

## Estructura

```
Scripts/Hashi/
  Machine/  MachineController, BarRig, DropZone, HashiLayers
  Claw/     ClawController, ClawFingerController, ClawFingerContact
  Prize/    PrizeController, PrizeDefinition, PrizeSpawner
  Game/     GameManager, CreditsManager, DifficultySettings,
            CameraController, InputReader
  UI/       UIManager, WinEffects
  Audio/    AudioManager
Scripts/Editor/
  HashiWatashiBuilder, HashiAssets, HashiUIBuilder, HashiMateriales,
  HashiPiezas, HashiCableado, HashiDebugMenu
```

Todo va en `namespace Hashi`. **Es obligatorio**: el proyecto ya tiene
`ClawController`, `GameManager` y `CameraController` en el espacio global, del
simulador de tienda y de TextMesh Pro. Sin el namespace no compila.

## Dos decisiones que sorprenden al abrir la escena

**Las pinzas no cuelgan del cuerpo de la garra.** Son hermanas suyas en la
jerarquía, unidas solo por la bisagra. Un Rigidbody dentro de un transform que
otro mueve a mano cada fotograma no se entera de que se ha movido: PhysX lo
teletransporta y las bisagras dan tirones.

**Los carriles son adorno.** El cuerpo de la garra es un Rigidbody cinemático que
se mueve con `MovePosition`; `RailX`, `RailZ` y `VerticalAxis` lo siguen. Así el
motor de física conoce la velocidad de la garra y se la transmite a las pinzas,
que es de donde sale que empujen la caja al desplazarse.

## Sonido

`AudioManager` tiene los ocho eventos preparados (mover, bajar, cerrar, pinza
contra premio, premio contra barra, premio cae, victoria, botón) con los clips
sin asignar. El que falte simplemente no suena.
