# Casa VR – Apartamentos (Unity 6.3 + Meta Quest)

Casa de dos pisos (un apartamento por piso, escalera exterior) creada a partir de
`Apartamentos_Planos_Blender.blend`, con la misma distribución, puertas y ventanas del modelo original.

* **Piso 1:** sala, cocina-comedor, dormitorio, baño, lavandería y patio (abierto al jardín).
* **Piso 2:** sala, cocina-comedor, dormitorio, baño, lavandería, balcón techado y pasillo exterior.
* **Exterior:** porche de entrada, escalera, jardín frontal y trasero con árboles, césped y arbustos.

## Requisitos

| Paquete | Versión |
|---|---|
| Unity | 6000.3.21f1 |
| XR Interaction Toolkit | 3.5.1 |
| OpenXR Plugin | 1.17.1 |
| XR Plugin Management | 4.5.0 |

## Controles

| Acción | Meta Quest | Simulador XR / escritorio |
|---|---|---|
| Caminar | **Joystick izquierdo** (continuo, hacia donde miras; con gravedad y colisiones) | WASD (Shift para correr) |
| Girar | Joystick derecho (giros de 45°) | Mouse |
| Abrir/cerrar puertas y ventanas | Apuntar con el rayo y gatillo o grip (clic). Mantener y mover el control = empujar la puerta o deslizar la ventana | Mirar al objeto y **E** o clic |
| Interruptores de luz | Apuntar + gatillo | E / clic |
| Llaves (tubos), lavatorio, fregadero, pila y ducha | Apuntar + gatillo: la manija gira y corre el agua | E / clic |
| **Menú de teletransporte** (en cualquier momento) | Botón **Menú** del control izquierdo, **Y** o **B** | **Tab** o **M** |

### Menú de teletransporte

El menú de teletransporte permite desplazarse de forma inmediata a cualquier espacio de la vivienda y puede
abrirse en cualquier momento de la experiencia.

1. **Apertura.** En Meta Quest se abre con el botón **Menú (☰)** del control izquierdo, o con los botones
   **Y** (control izquierdo) o **B** (control derecho). En el XR Interaction Simulator se utiliza la tecla
   **M**, y en el modo escritorio, las teclas **Tab** o **M**.
2. **Ubicación.** El panel se presenta frente al usuario, a aproximadamente 1.25 m y ligeramente por debajo
   de la línea de visión. Si existe una pared en esa dirección, el sistema lo reubica hacia el lado libre más
   cercano a la mirada. Su tamaño angular se mantiene constante y se dibuja por encima de paredes y muebles;
   el rayo y el control se representan sobre el menú para indicar en todo momento el destino señalado.
3. **Organización.** Los destinos se agrupan en tres categorías visibles de forma simultánea, una por columna.
   La categoría correspondiente a la ubicación actual del usuario se resalta en azul y el encabezado indica
   "Estás en: …".
   * **Piso 1:** Sala · Cocina y comedor · Dormitorio · Baño · Lavandería · Patio
   * **Piso 2:** Sala · Cocina y comedor · Dormitorio · Baño · Lavandería · Balcón techado · Pasillo exterior
   * **Exterior:** Entrada principal · Escalera · Jardín frontal · Jardín trasero
4. **Selección.** El destino se elige apuntándolo con el rayo, lo que lo resalta, y presionando el
   **gatillo** (en la computadora, mediante clic). El usuario queda con los pies sobre el piso del destino,
   orientado hacia el interior del espacio, y el menú se cierra automáticamente.
5. **Opciones adicionales.** En la parte inferior se encuentran el botón **Modo noche / Modo día**, que
   permite apreciar el efecto de la iluminación, y el botón **Cerrar**. El menú también se cierra al presionar
   nuevamente el botón con que se abrió.

El diseño del menú puede consultarse en `Blender/vistas_previas/menu_teletransporte.png`.

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
abiertas) y con la prueba PlayMode `Mesas_SePuedenRodearPorTodosLados`.

El jugador es una cápsula de **0.40 m** de diámetro (radio 0.20 + piel 0.02). Las puertas son sólidas:
ábrelas con rayo + gatillo (o **E**) para pasar. Los mapas `vistas_previas/circulacion_piso1.png` y
`circulacion_piso2.png` muestran en verde todo el piso alcanzable.

### Probar desde la computadora

* **XR Interaction Simulator** (se agrega solo en el editor): simula visor y controles con teclado y mouse
  (ver el panel de teclas en pantalla). El menú de salas se abre con **M**.
* **Sin simulador** (CasaVR > Opciones > Activar o desactivar el simulador): modo escritorio en primera persona.
* **Quest Link**: activa *Initialize XR on Startup* en *Project Settings > XR Plug-in Management* (pestaña PC).
  Viene apagado para que el simulador funcione sin errores de OpenXR. Con el visor conectado, el simulador se
  apaga solo.

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
