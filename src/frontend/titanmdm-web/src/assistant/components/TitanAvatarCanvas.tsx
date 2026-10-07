import type {
  TitanAnimationState,
  TitanFacingDirection,
  TitanVelocity,
} from '../context/TitanAssistantContext'

interface Props {
  animation: TitanAnimationState
  velocity: TitanVelocity
  direction: TitanFacingDirection
  pointerX: number
  pointerY: number
  reducedMotion: boolean
  onClick: () => void
}

export function TitanAvatarCanvas({
  animation,
  direction,
  reducedMotion,
  onClick,
}: Props) {
  const active =
    !reducedMotion &&
    ['walking', 'running', 'flying', 'peeking'].includes(animation)

  return (
    <svg
      className={[
        'titan-avatar-canvas',
        'titan-avatar-canvas--metal',
        active ? 'is-moving' : '',
      ].filter(Boolean).join(' ')}
      width="130"
      height="190"
      viewBox="0 0 130 190"
      role="button"
      aria-label="Abrir Titan Assistant"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault()
          onClick()
        }
      }}
      style={{
        transform: direction === 'left'
          ? 'scaleX(-1)'
          : undefined,
      }}
    >
      <defs>
        <linearGradient id="titan-metal" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#eaf8ff" />
          <stop offset=".28" stopColor="#88b6d6" />
          <stop offset=".57" stopColor="#254b78" />
          <stop offset=".79" stopColor="#71a9d0" />
          <stop offset="1" stopColor="#142c53" />
        </linearGradient>

        <linearGradient id="titan-face" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#e6faff" />
          <stop offset=".65" stopColor="#9fcce9" />
          <stop offset="1" stopColor="#6697c0" />
        </linearGradient>

        <radialGradient id="titan-light">
          <stop offset="0" stopColor="#e8ffff" />
          <stop offset=".45" stopColor="#65e4ff" />
          <stop offset="1" stopColor="#1587d5" />
        </radialGradient>

        <filter id="titan-glow">
          <feGaussianBlur stdDeviation="2" />
        </filter>
      </defs>

      <ellipse
        cx="66"
        cy="181"
        rx="42"
        ry="5"
        fill="#123a73"
        opacity=".18"
      />

      {/* Piernas y botas */}
      <g stroke="#17375e" strokeWidth="2" strokeLinejoin="round">
        <path
          d="M46 119 L59 122 L56 162 L45 165 L40 142 Z"
          fill="url(#titan-metal)"
        />
        <path
          d="M71 122 L83 119 L91 144 L84 165 L73 162 Z"
          fill="url(#titan-metal)"
        />
        <path
          d="M44 160 L58 159 L58 176 Q55 181 34 180 L30 175 Z"
          fill="url(#titan-metal)"
        />
        <path
          d="M72 159 L86 160 L99 175 Q98 181 77 180 L72 176 Z"
          fill="url(#titan-metal)"
        />
      </g>

      {/* Brazos con articulaciones luminosas */}
      <g stroke="#193d68" strokeWidth="2" strokeLinejoin="round">
        <path
          d="M43 72 L33 78 L26 103 L33 108 L46 85 Z"
          fill="url(#titan-metal)"
        />
        <path
          d="M86 72 L96 78 L105 106 L97 110 L83 85 Z"
          fill="url(#titan-metal)"
        />
        <circle cx="33" cy="106" r="5" fill="url(#titan-light)" />
        <circle cx="98" cy="108" r="5" fill="url(#titan-light)" />
        <path
          d="M25 104 L33 108 L36 128 L29 132 L20 118 Z"
          fill="url(#titan-metal)"
        />
        <path
          d="M98 109 L106 105 L112 123 L106 131 L99 126 Z"
          fill="url(#titan-metal)"
        />
      </g>

      {/* Torso */}
      <path
        d="M47 68 Q64 61 83 68 L91 91 L84 119
           Q65 128 45 119 L38 91 Z"
        fill="url(#titan-metal)"
        stroke="#17375e"
        strokeWidth="2"
      />
      <path
        d="M48 78 Q65 73 82 78 L80 106
           Q64 112 50 106 Z"
        fill="#173e6d"
        stroke="#67c9f6"
        strokeWidth="1.5"
      />
      <path
        d="M60 83 L70 83 L72 89 L65 98 L58 89 Z"
        fill="url(#titan-light)"
      />
      <text
        x="65"
        y="105"
        textAnchor="middle"
        fill="#e8f7ff"
        fontSize="8"
        fontWeight="800"
        fontFamily="Arial, sans-serif"
      >
        TITAN
      </text>
      <path
        d="M49 114 Q65 119 81 114"
        fill="none"
        stroke="#75dfff"
        strokeWidth="2"
        opacity=".8"
      />

      {/* Auriculares y antenas */}
      <path
        d="M31 37 Q29 12 65 10 Q101 12 99 37"
        fill="none"
        stroke="#1d456f"
        strokeWidth="6"
      />
      <path
        d="M33 31 L25 26 L25 49 L33 48 Z"
        fill="url(#titan-metal)"
        stroke="#17375e"
        strokeWidth="2"
      />
      <path
        d="M97 31 L105 26 L105 49 L97 48 Z"
        fill="url(#titan-metal)"
        stroke="#17375e"
        strokeWidth="2"
      />
      <circle cx="29" cy="39" r="4" fill="url(#titan-light)" />
      <circle cx="101" cy="39" r="4" fill="url(#titan-light)" />

      {/* Cabeza y rostro */}
      <path
        d="M39 22 Q65 10 91 22 Q99 31 95 53
           Q87 70 65 72 Q43 70 35 53 Q31 31 39 22 Z"
        fill="url(#titan-metal)"
        stroke="#17375e"
        strokeWidth="2"
      />
      <path
        d="M43 28 Q65 21 87 28 Q91 34 88 51
           Q81 63 65 65 Q49 63 42 51 Q39 34 43 28 Z"
        fill="url(#titan-face)"
        stroke="#477aa6"
        strokeWidth="1.5"
      />

      <ellipse cx="53" cy="43" rx="7" ry="9" fill="#f4ffff" />
      <ellipse cx="76" cy="43" rx="7" ry="9" fill="#f4ffff" />
      <circle cx="54" cy="44" r="4" fill="#1775bf" />
      <circle cx="76" cy="44" r="4" fill="#1775bf" />
      <circle cx="55" cy="42" r="1.4" fill="white" />
      <circle cx="77" cy="42" r="1.4" fill="white" />

      <path
        d="M56 56 Q65 62 74 56"
        fill="none"
        stroke="#24527c"
        strokeWidth="2.5"
        strokeLinecap="round"
      />
      <path
        d="M43 28 Q65 18 87 28"
        fill="none"
        stroke="#e9faff"
        strokeWidth="2"
        opacity=".75"
      />

      {/* Destellos suaves */}
      <circle
        cx="65"
        cy="89"
        r="12"
        fill="#5fe3ff"
        opacity=".14"
        filter="url(#titan-glow)"
      />
    </svg>
  )
}