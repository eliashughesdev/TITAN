import {
  useCallback,
  useEffect,
  useRef,
} from 'react'

import type {
  KeyboardEvent,
  MouseEvent,
  WheelEvent,
} from 'react'
import type { RemoteFrame } from '../../api/remoteSupportSignalR'

interface RemoteDesktopViewerProps {
  frame: RemoteFrame | null;
  width?: number;
  height?: number;
  connected: boolean;
  allowMouse: boolean;
  allowKeyboard: boolean;

  onPointerMove: (
    x: number,
    y: number,
  ) => void;

  onPointerButton: (
    action:
      | "left-down"
      | "left-up"
      | "right-down"
      | "right-up",
  ) => void;

  onWheel: (
    delta: number,
  ) => void;

  onKeyboard: (
    virtualKey: number,
    keyDown: boolean,
  ) => void;
}

function getVirtualKey(
  event: KeyboardEvent<HTMLDivElement>,
): number {
  const key = event.key;

  if (
    key.length === 1
  ) {
    return key
      .toUpperCase()
      .charCodeAt(0);
  }

  const specialKeys: Record<
    string,
    number
  > = {
    Backspace: 0x08,
    Tab: 0x09,
    Enter: 0x0d,
    Shift: 0x10,
    Control: 0x11,
    Alt: 0x12,
    Pause: 0x13,
    CapsLock: 0x14,
    Escape: 0x1b,
    " ": 0x20,
    PageUp: 0x21,
    PageDown: 0x22,
    End: 0x23,
    Home: 0x24,
    ArrowLeft: 0x25,
    ArrowUp: 0x26,
    ArrowRight: 0x27,
    ArrowDown: 0x28,
    Insert: 0x2d,
    Delete: 0x2e,
    F1: 0x70,
    F2: 0x71,
    F3: 0x72,
    F4: 0x73,
    F5: 0x74,
    F6: 0x75,
    F7: 0x76,
    F8: 0x77,
    F9: 0x78,
    F10: 0x79,
    F11: 0x7a,
    F12: 0x7b,
  };

  return (
    specialKeys[key] ??
    0
  );
}

export default function RemoteDesktopViewer({
  frame,
  width,
  height,
  connected,
  allowMouse,
  allowKeyboard,
  onPointerMove,
  onPointerButton,
  onWheel,
  onKeyboard,
}: RemoteDesktopViewerProps) {
  const containerRef =
    useRef<HTMLDivElement>(
      null,
    );

  const canvasRef = useRef<HTMLCanvasElement>(null);

  const pendingPointerRef = useRef<{
    x: number;
    y: number;
  } | null>(null);

  const pointerTimerRef = useRef<number | null>(null);

  useEffect(() => () => {
    if (pointerTimerRef.current !== null) {
      window.clearTimeout(pointerTimerRef.current);
    }
  }, []);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || !frame) return;

    let disposed = false;
    let bitmap: ImageBitmap | null = null;

    const render = async () => {
      const bytes = new Uint8Array(frame.data.byteLength);
      bytes.set(frame.data);

      bitmap = await createImageBitmap(
        new Blob([bytes], { type: frame.mimeType }),
      );

      if (disposed) {
        bitmap.close();
        return;
      }

      canvas.width = frame.width;
      canvas.height = frame.height;
      canvas.getContext('2d', { alpha: false })
        ?.drawImage(bitmap, 0, 0, frame.width, frame.height);
    };

    void render().catch(error => {
      if (!disposed) {
        console.error('No fue posible decodificar el frame remoto.', error);
      }
    });

    return () => {
      disposed = true;
      bitmap?.close();
    };
  }, [frame]);

  const calculatePosition =
    useCallback(
      (
        event:
          MouseEvent<HTMLDivElement>,
      ) => {
        const rect =
          event.currentTarget
            .getBoundingClientRect();

        if (!width || !height) {
          return {
            x: (event.clientX - rect.left) / rect.width,
            y: (event.clientY - rect.top) / rect.height,
          };
        }

        const scale = Math.min(
          rect.width / width,
          rect.height / height,
        );
        const renderedWidth = width * scale;
        const renderedHeight = height * scale;
        const offsetX = (rect.width - renderedWidth) / 2;
        const offsetY = (rect.height - renderedHeight) / 2;
        const localX = event.clientX - rect.left - offsetX;
        const localY = event.clientY - rect.top - offsetY;

        if (
          localX < 0 ||
          localY < 0 ||
          localX > renderedWidth ||
          localY > renderedHeight
        ) {
          return null;
        }

        return {
          x: localX / renderedWidth,
          y: localY / renderedHeight,
        };
      },
      [height, width],
    );

  const handleMouseMove =
    useCallback(
      (
        event:
          MouseEvent<HTMLDivElement>,
      ) => {
        if (
          !connected ||
          !allowMouse
        ) {
          return;
        }

        const position =
          calculatePosition(
            event,
          );

        if (!position) return;

        pendingPointerRef.current = position;

        if (pointerTimerRef.current !== null) return;

        pointerTimerRef.current = window.setTimeout(() => {
          pointerTimerRef.current = null;
          const latest = pendingPointerRef.current;
          pendingPointerRef.current = null;

          if (latest) {
            onPointerMove(latest.x, latest.y);
          }
        }, 33);
      },
      [
        connected,
        allowMouse,
        calculatePosition,
        onPointerMove,
      ],
    );

  const handleMouseDown =
    useCallback(
      (
        event:
          MouseEvent<HTMLDivElement>,
      ) => {
        if (
          !connected ||
          !allowMouse
        ) {
          return;
        }

        event.preventDefault();

        containerRef.current?.focus();

        const position = calculatePosition(event);
        if (!position) return;

        onPointerMove(position.x, position.y);

        if (event.button === 0) {
          onPointerButton(
            "left-down",
          );
        }

        if (event.button === 2) {
          onPointerButton(
            "right-down",
          );
        }
      },
      [
        connected,
        allowMouse,
        calculatePosition,
        onPointerMove,
        onPointerButton,
      ],
    );

  const handleMouseUp =
    useCallback(
      (
        event:
          MouseEvent<HTMLDivElement>,
      ) => {
        if (
          !connected ||
          !allowMouse
        ) {
          return;
        }

        event.preventDefault();

        if (event.button === 0) {
          onPointerButton(
            "left-up",
          );
        }

        if (event.button === 2) {
          onPointerButton(
            "right-up",
          );
        }
      },
      [
        connected,
        allowMouse,
        onPointerButton,
      ],
    );

  const handleWheel =
    useCallback(
      (
        event:
          WheelEvent<HTMLDivElement>,
      ) => {
        if (
          !connected ||
          !allowMouse
        ) {
          return;
        }

        event.preventDefault();

        onWheel(
          event.deltaY < 0
            ? 120
            : -120,
        );
      },
      [
        connected,
        allowMouse,
        onWheel,
      ],
    );

  const handleKeyDown =
    useCallback(
      (
        event:
          KeyboardEvent<HTMLDivElement>,
      ) => {
        if (
          !connected ||
          !allowKeyboard
        ) {
          return;
        }

        const virtualKey =
          getVirtualKey(
            event,
          );

        if (!virtualKey) {
          return;
        }

        event.preventDefault();

        onKeyboard(
          virtualKey,
          true,
        );
      },
      [
        connected,
        allowKeyboard,
        onKeyboard,
      ],
    );

  const handleKeyUp =
    useCallback(
      (
        event:
          KeyboardEvent<HTMLDivElement>,
      ) => {
        if (
          !connected ||
          !allowKeyboard
        ) {
          return;
        }

        const virtualKey =
          getVirtualKey(
            event,
          );

        if (!virtualKey) {
          return;
        }

        event.preventDefault();

        onKeyboard(
          virtualKey,
          false,
        );
      },
      [
        connected,
        allowKeyboard,
        onKeyboard,
      ],
    );

  return (
    <div
      ref={containerRef}
      tabIndex={0}
      onMouseMove={
        handleMouseMove
      }
      onMouseDown={
        handleMouseDown
      }
      onMouseUp={
        handleMouseUp
      }
      onWheel={
        handleWheel
      }
      onKeyDown={
        handleKeyDown
      }
      onKeyUp={
        handleKeyUp
      }
      onContextMenu={(
        event,
      ) =>
        event.preventDefault()
      }
      style={{
        width: "100%",
        height: "100%",
        minHeight: 420,
        position: "relative",
        overflow: "hidden",
        outline: "none",
        background:
          "#05080d",
        borderRadius: 14,
        cursor:
          connected &&
          allowMouse
            ? "default"
            : "not-allowed",
      }}
    >
      {frame ? (
        <canvas
          ref={canvasRef}
          aria-label="Escritorio remoto"
          role="img"
          style={{
            width: "100%",
            height: "100%",
            display: "block",
            objectFit:
              "contain",
            userSelect:
              "none",
            pointerEvents:
              "none",
          }}
        />
      ) : (
        <div
          style={{
            position:
              "absolute",
            inset: 0,
            display: "grid",
            placeItems:
              "center",
            color:
              "#94a3b8",
            textAlign:
              "center",
            padding: 24,
          }}
        >
          <div>
            <div
              style={{
                fontSize:
                  18,
                fontWeight:
                  700,
                color:
                  "#e2e8f0",
              }}
            >
              {connected
                ? "Esperando imagen del equipo..."
                : "Sin transmisión activa"}
            </div>

            <div
              style={{
                marginTop:
                  8,
                fontSize:
                  13,
              }}
            >
              {connected
                ? "El canal está conectado. TitanMDM está esperando el primer frame."
                : "Inicia o selecciona una sesión remota."}
            </div>
          </div>
        </div>
      )}

      {width &&
        height && (
          <div
            style={{
              position:
                "absolute",
              right: 12,
              bottom: 10,
              padding:
                "4px 8px",
              borderRadius:
                8,
              background:
                "rgba(0,0,0,.65)",
              color:
                "#cbd5e1",
              fontSize:
                11,
              pointerEvents:
                "none",
            }}
          >
            {width} ×{" "}
            {height}
          </div>
        )}
    </div>
  );
}
