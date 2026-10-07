interface Props {
  connected: boolean;
  allowMouse: boolean;
  allowKeyboard: boolean;
  allowClipboard: boolean;
  allowFileTransfer: boolean;
  onFullscreen: () => void;
  onTerminate: () => void;
}

export default function RemoteSessionToolbar({
  connected,
  allowMouse,
  allowKeyboard,
  allowClipboard,
  allowFileTransfer,
  onFullscreen,
  onTerminate,
}: Props) {
  const buttonStyle = {
    border:
      "1px solid #334155",
    borderRadius: 9,
    background:
      "#111827",
    color:
      "#e2e8f0",
    padding:
      "8px 12px",
    cursor:
      "pointer",
    fontSize:
      12,
  } as const;

  return (
    <div
      style={{
        display: "flex",
        alignItems:
          "center",
        gap: 8,
        flexWrap:
          "wrap",
      }}
    >
      <span
        title="Mouse"
        style={{
          opacity:
            allowMouse
              ? 1
              : 0.35,
        }}
      >
        🖱 Mouse
      </span>

      <span
        title="Teclado"
        style={{
          opacity:
            allowKeyboard
              ? 1
              : 0.35,
        }}
      >
        ⌨ Teclado
      </span>

      <span
        title="Portapapeles"
        style={{
          opacity:
            allowClipboard
              ? 1
              : 0.35,
        }}
      >
        📋 Clipboard
      </span>

      <span
        title="Transferencia de archivos"
        style={{
          opacity:
            allowFileTransfer
              ? 1
              : 0.35,
        }}
      >
        📁 Archivos
      </span>

      <div
        style={{
          flex: 1,
        }}
      />

      <button
        type="button"
        style={
          buttonStyle
        }
        disabled={
          !connected
        }
        onClick={
          onFullscreen
        }
      >
        Pantalla completa
      </button>

      <button
        type="button"
        onClick={
          onTerminate
        }
        style={{
          ...buttonStyle,
          border:
            "1px solid #7f1d1d",
          background:
            "#450a0a",
          color:
            "#fecaca",
        }}
      >
        Finalizar sesión
      </button>
    </div>
  );
}