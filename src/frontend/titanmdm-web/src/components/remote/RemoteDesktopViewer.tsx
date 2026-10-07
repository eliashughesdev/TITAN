import {
  useCallback,
  useRef,
} from 'react'

import type {
  KeyboardEvent,
  MouseEvent,
  WheelEvent,
} from 'react'

interface RemoteDesktopViewerProps {
  frameUrl: string | null;
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
  frameUrl,
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

  const calculatePosition =
    useCallback(
      (
        event:
          MouseEvent<HTMLDivElement>,
      ) => {
        const rect =
          event.currentTarget
            .getBoundingClientRect();

        return {
          x:
            (event.clientX -
              rect.left) /
            rect.width,

          y:
            (event.clientY -
              rect.top) /
            rect.height,
        };
      },
      [],
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

        onPointerMove(
          position.x,
          position.y,
        );
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
      {frameUrl ? (
        <img
          src={frameUrl}
          alt="Escritorio remoto"
          draggable={false}
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