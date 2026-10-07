interface Props {
  status: string;
  transportStatus: string;
  fps: number;
  latencyMs?: number | null;
}

export default function RemoteSessionStatus({
  status,
  transportStatus,
  fps,
  latencyMs,
}: Props) {
  const connected =
    status === "Connected";

  return (
    <div
      style={{
        display: "flex",
        gap: 16,
        flexWrap: "wrap",
        alignItems: "center",
        fontSize: 12,
        color: "#94a3b8",
      }}
    >
      <span>
        <strong
          style={{
            color: connected
              ? "#4ade80"
              : "#fbbf24",
          }}
        >
          ●
        </strong>{" "}
        {status}
      </span>

      <span>
        Canal:{" "}
        <strong
          style={{
            color: "#e2e8f0",
          }}
        >
          {transportStatus}
        </strong>
      </span>

      <span>
        FPS:{" "}
        <strong
          style={{
            color: "#e2e8f0",
          }}
        >
          {fps}
        </strong>
      </span>

      {latencyMs != null && (
        <span>
          Latencia:{" "}
          <strong
            style={{
              color:
                "#e2e8f0",
            }}
          >
            {latencyMs} ms
          </strong>
        </span>
      )}
    </div>
  );
}