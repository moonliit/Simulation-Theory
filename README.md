# Synth Survivor - Motor de Raymarching SDF en Unity (URP)

Un motor de renderizado volumétrico basado en *Raymarching* y Campos de Distancia Signada (*Signed Distance Fields*, SDF), construido de forma nativa para Unity utilizando el *Universal Render Pipeline (URP)*.

## Descripción del Proyecto

Este repositorio aloja la implementación práctica de un sistema de renderizado híbrido. Combina la geometría poligonal tradicional de Unity (rasterización estándar) con mallas matemáticas evaluadas píxel a píxel en tiempo real (Raymarching).

El objetivo principal de este entorno es permitir la representación paramétrica, manipulación y animación de objetos volumétricos utilizando operaciones booleanas complejas de Geometría Constructiva de Sólidos (CSG), logrando fusiones orgánicas (smooth blending) imposibles de obtener con polígonos convencionales.

## Reproducibilidad

Se recomienda utilizar **Unity 6000.3.14f1**, ya que esta fue la versión empleada y testeada a lo largo de todo el desarrollo del proyecto.

### Pasos para abrir y ejecutar el proyecto:
1. **Instalación:** Asegúrate de tener instalada la versión sugerida `6000.3.14f1` del motor. Se recomienda realizar la instalación a través de **Unity Hub**.
2. **Apertura:** En Unity Hub, ve a la pestaña *Projects*, haz clic en *Add* (o *Open* > *Add project from disk*), localiza la carpeta descomprimida de este proyecto y selecciónala.
3. **Ejecución en el Editor:** Una vez que Unity importe los assets y abra el proyecto, navega en el panel *Project* hacia la carpeta de escenas y abre la escena principal del juego. Presiona el botón de **Play** en la parte superior central del editor para iniciar la simulación en tiempo real.

### Proceso de Compilación (Build):
1. Ve al menú superior y selecciona **File > Build Settings**.
2. Verifica que la escena principal del juego esté agregada y marcada con un *tick* en la lista *Scenes in Build*.
3. Selecciona tu plataforma objetivo (por defecto *Windows, Mac, Linux*).
4. Haz clic en **Build**. El editor te pedirá que selecciones una carpeta de destino; crea una carpeta vacía, selecciónala y espera a que el proceso de compilación genere el ejecutable final.

## Características Principales

*   **Integración con URP y Z-Buffer:** Diseñado para coexistir de manera transparente con los shaders estándar. Lee de forma nativa la textura de profundidad (`Depth Texture`) de la cámara, logrando que los volúmenes virtuales se ocluyan de manera realista detrás de geometrías estáticas (suelos, muros, props).
*   **Herramienta de Compilación (Tree Baker):** Sistema de desarrollo integrado en el Editor que permite construir figuras agrupando primitivas (GameObjects) y compila este árbol jerárquico directamente a código HLSL optimizado.
*   **Estructura de Primitivas:** Soporte completo de escalado asimétrico y transformaciones locales mediante matrices inversas para cajas, cápsulas, esferas y toros, con soporte para adición y sustracción espacial.

## Estructura del Repositorio

*   **`Assets/Scripts/Core/`**: Contiene la lógica del motor en la CPU. Aquí se maneja la jerarquía de `SdfPrimitiveSubscriber`, el despachador de recursos a la GPU (`SdfOctreeManager` y `SdfSceneManager`) y la herramienta de parseo y construcción de scripts genéricos (`SdfCsgTreeBaker`).
*   **`Assets/Shaders/`**: Aloja el corazón matemático del proyecto. Incluye el shader principal de recorrido de rayos (`SDFVolumeRaymarcher.shader`) y los archivos HLSL estáticos precompilados por el Baker para geometrías de jefes y personajes complejos.

## Documentación y Metodología

La documentación principal del proyecto se puede encontrar en el informe técnico en formato PDF, adjunto en el archivo ZIP que contiene el resto del proyecto.