# Requisitos de sprites - Titan, login y Fiorella

## Estado actual

- Titan Assistant usa avatar SVG/CSS y motores de movimiento existentes.
- El robot login es una composicion CSS; no hay hoja de sprites final.
- Fiorella dispone actualmente de cuatro recursos provisionales: idle, walk, jump y point. Los demas estados reutilizan el recurso mas cercano.
- No se afirma que estos recursos sean el arte final.

## Manifest login

`TitanLoginAnimationManifest.ts` normaliza:

- `boot`;
- `idle`;
- `greeting`;
- `attention`;
- `email-focus`;
- `password-focus`;
- `authenticating`;
- `success`;
- `failure`;
- `exit`;
- `reduced-motion`.

Cada entrada define duracion, loop, fallback y `spriteUrl`. Todos los `spriteUrl` permanecen `null` hasta recibir arte aprobado. El preload ignora recursos nulos y el robot CSS es fallback.

## Estados Titan futuros

- Idle.
- Greeting.
- Walk.
- Point.
- Thinking.
- Talk.
- Jetpack startup.
- Hover.
- Fly.
- Landing.

## Estados Fiorella

- Idle.
- Walk.
- Jump.
- Point.
- Wave.
- Celebrate.
- Surprised.
- Sleep.
- Think.
- Alert.
- Peek.
- Hide.

## Entrega grafica recomendada

| Requisito | Valor recomendado |
|---|---|
| Formato | WebP o PNG con alpha; AVIF opcional con fallback. |
| Color | sRGB. |
| Fondo | Transparente. |
| Escala | 1x y 2x o atlas 2x con metadata. |
| Pivot | Pies/centro inferior constante para locomocion. |
| Caja | Mismas dimensiones y baseline en todos los frames del personaje. |
| Naming | `{character}-{state}-{frame}@2x.webp`. |
| Metadata | JSON con fps, loop, frame rect, pivot, hitbox visual y fallback. |
| Duracion | Expresada por estado, no inferida del nombre del archivo. |

No recortar cada frame con cajas distintas: produce saltos de escala y posicion.

## Calidad de animacion

- Idle/talk: 12-18 fps suele ser suficiente.
- Walk/fly: 18-24 fps.
- Gestos cortos: inicio y fin compatibles con idle.
- Jetpack: startup, hover, fly y landing deben ser clips separados.
- Evitar flashes blancos al decodificar; todos los recursos deben precargarse antes del primer cambio.
- Mantener el ultimo frame valido hasta que el siguiente recurso este decodificado.
- No animar transformaciones grandes y cambio de sprite en el mismo frame.

## Accesibilidad

- `prefers-reduced-motion` debe usar pose estatica y transiciones de opacidad cortas o nulas.
- Las animaciones son decorativas; estado, error y exito deben tener texto/ARIA independiente.
- Ningun componente de animacion recibe contrasenas, tokens ni secretos.
- Los gestos no deben bloquear formularios, tablas o controles.

## Responsive y zonas seguras

- Validar 1920, 1440, 1024 y movil.
- El chat debe permanecer dentro del viewport y no cubrir acciones primarias.
- Mantener una zona segura minima de 16 px en movil y 24 px en desktop.
- La locomocion debe respetar paneles abiertos, overlays y reduced motion.

## Criterios de aceptacion del arte final

1. Todos los frames usan el mismo canvas y pivot.
2. No hay bordes opacos ni halos de premultiplicacion.
3. El manifest valida rutas y fallbacks.
4. Preload y error de imagen no dejan al personaje invisible.
5. Transiciones idle -> gesto -> idle no saltan.
6. Las pruebas visuales cubren los cuatro breakpoints.
7. Reduced motion no reproduce loops.
8. Los nombres de estados coinciden con los contratos TypeScript; no se agregan alias ad hoc en CSS.
