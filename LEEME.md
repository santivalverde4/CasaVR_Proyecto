# Casa VR – Apartamentos (Unity 6.3 + Meta Quest)

Casa de dos pisos (un apartamento por piso, escalera exterior) creada a partir de
`Apartamentos_Planos_Blender.blend`, con la misma distribución, puertas y ventanas del modelo original.

* **Piso 1:** sala, cocina-comedor, dormitorio, baño, lavandería y patio (abierto al jardín).
* **Piso 2:** sala, cocina-comedor, dormitorio, baño, lavandería, balcón techado y pasillo exterior.
* **Exterior:** porche de entrada, escalera, jardín frontal y trasero con árboles, césped y arbustos.

## Requisitos (los mismos de tu proyecto actual)

| Paquete | Versión |
|---|---|
| Unity | 6000.3.21f1 |
| XR Interaction Toolkit | 3.5.1 |
| OpenXR Plugin | 1.17.1 |
| XR Plugin Management | 4.5.0 |
| Input System | 1.14.x (manejo de entrada: *Input System Package* o *Both*) |
| URP | 17.3 (recomendado; también funciona con Built-in) |

## Opción A (recomendada): importar en tu proyecto `ProyectoRealidadVirtual`

1. `Assets > Import Package > Custom Package…` y elige **CasaVR.unitypackage**. Importa todo.
2. Espera a que compile. Aparece el menú **CasaVR** en la barra superior.
3. **CasaVR > 1. Configurar proyecto (Meta Quest + OpenXR)**
   – Android ARM64, IL2CPP, API mínima 32, Vulkan, GameActivity, orientación Landscape Left.
   – OpenXR para Android con *Meta Quest Support* y perfiles *Oculus Touch / Touch Plus / Touch Pro*
     (Quest 2, 3, 3S y Pro); OpenXR también para PC (Quest Link).
   – URP para VR autónoma (MSAA 4x, sin HDR, sombras ligeras).
   – Importa la muestra **XR Interaction Simulator** de XRI para probar desde la computadora.
   – Ofrece cambiar la plataforma a Android.
4. **CasaVR > 2. Construir escena de la casa** → crea `Assets/CasaVR/Scenes/CasaVR.unity`
   (se puede repetir cuando quieras; regenera la escena).
5. **CasaVR > 3. Validar escena y proyecto** → lista de comprobación automática.
6. **CasaVR > 4. Compilar APK para Meta Quest** → `Builds/CasaVR.apk`.

## Opción B: abrir como proyecto nuevo

Abre la carpeta `CasaVR_Proyecto` desde Unity Hub (*Add > Add project from disk*) con Unity 6000.3.21f1.
Si aparece el aviso del Input System pidiendo activar el nuevo sistema de entrada, acepta (reinicia el editor).
Luego sigue los pasos 3 a 6.

## Controles

| Acción | Meta Quest | Simulador XR / escritorio |
|---|---|---|
| Caminar | **Joystick izquierdo** (continuo, hacia donde miras; con gravedad y colisiones) | WASD (Shift para correr) |
| Girar | Joystick derecho (giros de 45°) | Mouse |
| Abrir/cerrar puertas y ventanas | Apuntar con el rayo y gatillo o grip (clic). Mantener y mover el control = empujar la puerta o deslizar la ventana | Mirar al objeto y **E** o clic |
| Interruptores de luz | Apuntar + gatillo | E / clic |
| Llaves (tubos), lavatorio, fregadero, pila y ducha | Apuntar + gatillo: la manija gira y corre el agua | E / clic |
| **Menú de teletransporte** (en cualquier momento) | Botón **Menú** del control izquierdo, **Y** o **B** | **Tab** o **M** |

### Menú de teletransporte (se abre en cualquier momento)

1. Pulsa el botón de menú: en Quest **☰ (Menú) del control izquierdo**, o **Y** (control izquierdo) o **B**
   (control derecho). En el simulador XR, **M**. En modo escritorio, **Tab** o **M**.
2. El panel aparece delante de ti a ~1.25 m y un poco debajo de los ojos. Si hay una pared enfrente, se
   coloca hacia el lado libre más cercano a tu mirada; siempre se ve del mismo tamaño y por encima de paredes
   y muebles. El rayo y el control se dibujan encima del menú, así siempre ves a qué destino apuntas.
3. Las tres categorías están a la vista a la vez, en columnas; la del piso donde estás aparece resaltada en
   azul (y arriba dice "Estás en: …"):
   * **Piso 1:** Sala · Cocina y comedor · Dormitorio · Baño · Lavandería · Patio
   * **Piso 2:** Sala · Cocina y comedor · Dormitorio · Baño · Lavandería · Balcón techado · Pasillo exterior
   * **Exterior:** Entrada principal · Escalera · Jardín frontal · Jardín trasero
4. Apunta con el rayo a un destino (se ilumina) y presiona el **gatillo** (en la computadora, clic). Te lleva
   ahí con los pies en el piso, mirando hacia el interior, y el menú se cierra solo.
5. Abajo: **Modo noche / Modo día** (para notar las luces) y **Cerrar** (o el mismo botón de menú).

`Blender/vistas_previas/menu_teletransporte.png` muestra el diseño del menú.

## Altura del jugador (tracking origin)

* El `XR Origin (CasaVR)` tiene escala 1 y sus pies están en el piso. La casa está a tamaño real
  (puertas 2.10 m, techos 2.88 m, encimeras 0.90 m).
* Hay una sola fuente de altura: el **Camera Offset**. La cámara queda en 0 dentro de él.
  * **Meta Quest** (modo *Floor*): el visor ya entrega la altura real de tu cabeza, así que el offset queda
    en **0**. Antes se sumaban 1.6 m extra y la vista quedaba a ~3.2 m.
  * **XR Interaction Simulator** o visor en modo *Device*: el dispositivo no entrega la altura, así que el
    offset es **1.70 m** (`CasaVRPlayer > Standing Eye Height`).
  * **Modo escritorio**: vista a 1.70 m.
* La cápsula del cuerpo sigue tu altura (vista + 10 cm) con un máximo de **1.85 m**, menor que el vano libre de
  las puertas (2.07 m): aunque seas alto, cabes por todas las puertas y por la escalera.
* Si conectas el Quest por Link en el editor, el simulador se apaga solo al detectar el visor (si estuviera
  encendido, quitaría el visor real y la cámara no seguiría tu cabeza).

## Espacio para caminar (circulación)

Los muebles de sala, comedor, cocina, dormitorio, lavandería, patio y balcón se reacomodaron para que se
pueda llegar caminando a **todos** los cuartos de los dos pisos y **rodear la mesa del comedor y la mesa de
centro por los cuatro lados**. Se comprueba con un mapa de ocupación (mismas colisiones que Unity, puertas
abiertas) y con la prueba PlayMode `Mesas_SePuedenRodearPorTodosLados`:

| Paso | Ancho libre mínimo |
|---|---|
| Alrededor de la mesa del comedor (4 sillas) | 0.67 m |
| Alrededor de la mesa de centro (entre sofá y mueble de TV) | 0.70 m |
| Comedor → cocina → patio / balcón | 0.67 m |
| Pasillo → dormitorio / baño | 0.70 m |
| Puerta de la lavandería (con la hoja abierta) | 0.65 m |
| Entrada principal | 0.85 m |

El jugador es una cápsula de **0.40 m** de diámetro (radio 0.20 + piel 0.02). Las puertas son sólidas:
ábrelas con rayo + gatillo (o **E**) para pasar. Los mapas `vistas_previas/circulacion_piso1.png` y
`circulacion_piso2.png` muestran en verde todo el piso alcanzable.

### Si quieres más espacio

* **Mover un mueble en Unity:** en la jerarquía, `Casa > Muebles piso 1` (o `piso 2`) → selecciona, por
  ejemplo, `Mueble_P1_Comedor` o `Mueble_P1_Silla_1..4` y muévelo con la herramienta Move (W). Su
  `BoxCollider` se mueve con él. Guarda la escena. (Si vuelves a usar *Construir escena*, se regenera desde
  el modelo y se pierden esos cambios.)
* **Cápsula del jugador más delgada:** `XR Origin (CasaVR)` → `Character Controller` → *Radius*
  (0.2 por defecto; no bajes de 0.15). Para que quede fijo al reconstruir, cambia `cc.radius` en
  `Scripts/Editor/CasaVRSceneBuilder.cs` (método `BuildPlayer`).
* **Cambiar la distribución en el modelo:** posiciones en `Blender/scripts/furniture.py` (bloque
  `# Sala / comedor / cocina`), luego `build.py`, `export.py` y *Construir escena*; `walkcheck.py` vuelve a
  generar el mapa de circulación y avisa si algún cuarto queda sin acceso.

### Probar desde la computadora

* **XR Interaction Simulator** (se agrega solo en el editor): simula visor y controles con teclado y mouse
  (ver el panel de teclas en pantalla). El menú de salas se abre con **M**.
* **Sin simulador** (CasaVR > Opciones > Activar o desactivar el simulador): modo escritorio en primera persona.
* **Quest Link**: activa *Initialize XR on Startup* en *Project Settings > XR Plug-in Management* (pestaña PC).
  Viene apagado para que el simulador funcione sin errores de OpenXR. Con el visor conectado, el simulador se
  apaga solo.

## Física (lo que corrige los errores de gravedad y colisiones)

* El jugador es un `CharacterController` propio (`CasaVRPlayer`) sobre el `XR Origin`: gravedad real (-9.81 m/s²),
  no puede volar, y el contacto con el piso se calcula con el `Move` que incluye la gravedad (no con el
  movimiento horizontal), que es el fallo típico que hace que el jugador "flote" o atraviese el piso.
* Si caminas físicamente con el visor, la cápsula sigue a la cabeza **con colisiones**: no puedes meter la
  cabeza a través de una pared (la vista se empuja hacia atrás).
* Paredes, losas, techo, columnas, marcos y celosía: `MeshCollider`. Muebles: `BoxCollider` estáticos.
* Escaleras: rampas invisibles (`COL_Rampa_*`) que pasan por el centro de cada huella + `stepOffset` de 32 cm;
  barandas con colisión para no caer entre los tramos.
* Puertas: hoja con `BoxCollider` + `Rigidbody` cinemático; se detienen si van a golpear al jugador.
  Abiertas, dejan el paso libre.
* Límite del terreno con colisión invisible alrededor del jardín.

## Pruebas automáticas

`Window > General > Test Runner > PlayMode > Run All`. Necesita el paquete *Test Framework* (viene en los
proyectos creados con las plantillas de Unity; si no, instálalo desde *Package Manager > Unity Registry*). Verifican: gravedad, paredes, subida de escalera,
teletransporte por pisos, puertas sobre la bisagra, ventanas, interruptores, agua de grifos y duchas, y que
se pueda caminar alrededor de las mesas de los dos pisos. También:

* `Altura_VistaHumanaYPiesEnElSuelo`: vista entre 1.60 y 1.80 m, pies en el piso, XR Origin sin escalar.
* `Teletransporte_TodosLosDestinosQuedanSobreElSuelo`: los 17 destinos, apoyado y sin flotar.
* `Joystick_IzquierdoCaminaConGravedadYColisiones`: un control XR virtual mueve el joystick izquierdo
  (el mismo binding del Quest) y el jugador avanza sin despegarse del piso.
* `Puertas_SeCabePorTodasConLaAlturaMaxima`: con la cápsula de 1.85 m pasa por las 10 puertas, ida y vuelta.
* `Menu_TresCategoriasConDestinosYSinOpcionesDeGiro`: tres categorías, un botón por destino, Modo día/noche y
  Cerrar, sin opciones de giro; elegir un destino teletransporta y cierra el menú.

## Estructura

```
Assets/CasaVR/
  Models/CasaVR_Casa.fbx      arquitectura + muebles + marcadores (1 unidad = 1 m)
  Textures/  Textures/UI (íconos y fondos del menú)  Audio/  Input/CasaVR_Controles.inputactions
  Materials/                  (se generan al construir la escena)
  Scenes/CasaVR.unity         (se genera al construir la escena)
  Scripts/Runtime             jugador, puertas, ventanas, luces, agua, menú, modo escritorio
  Scripts/Editor              configuración XR/Android, constructor de escena, validación y APK
  Scripts/Tests               pruebas PlayMode
Blender/                      .blend procesado y scripts de Python que generan el FBX
```

## Rendimiento en Quest

~53 000 triángulos en total, materiales URP Lit con texturas de 512–1024 px (ASTC), una luz direccional con
sombra y luces puntuales sin sombras por habitación, batching estático. Para mejor calidad visual existe
**CasaVR > Opciones > Hornear iluminación** (opcional; los interruptores siguen funcionando).

## Notas

* Para modificar el modelo: abre `Blender/CasaVR_proc.blend`, o regenéralo desde el `.blend` original con
  `python3 Blender/scripts/build.py Blender/Apartamentos_Planos_Blender.blend <carpeta_salida> all` y luego
  `python3 Blender/scripts/export.py <carpeta_salida>/CasaVR_proc.blend Assets/CasaVR/Models/CasaVR_Casa.fbx`
  (requiere Blender 5.x como módulo `bpy` de Python). Después, **CasaVR > 2. Construir escena** otra vez.
* `Blender/vistas_previas/` tiene las plantas con muebles/puertas y renders de cada espacio.
* Habitación 3 (lavandería) es la más pequeña, como se pidió. La ventana alta pequeña del modelo queda en ella.
