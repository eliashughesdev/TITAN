import {
  useEffect,
  useRef,
  useState,
  type CSSProperties,
} from 'react'

import './TitanLoginRobot.css'

export type TitanLoginRobotMode =
  | 'idle'
  | 'email'
  | 'password'
  | 'authenticating'
  | 'error'
  | 'success'
  | 'launch'

type RobotReaction =
  | 'none'
  | 'field'
  | 'pulse'

interface TitanLoginRobotProps {
  mode: TitanLoginRobotMode
  emailValue: string
  showPassword: boolean
}

const BOOT_DURATION_MS = 900
const REACTION_FIELD_MS = 520
const REACTION_PULSE_MS = 420

/** Tiempo maximo de la secuencia de encendido (0 si el usuario prefiere
 *  movimiento reducido, para no retrasar el acceso). */
function bootDuration(): number {
  if (
    typeof window === 'undefined' ||
    typeof window.matchMedia !== 'function'
  ) {
    return BOOT_DURATION_MS
  }

  return window.matchMedia(
    '(prefers-reduced-motion: reduce)',
  ).matches
    ? 0
    : BOOT_DURATION_MS
}

function getMessage(
  mode: TitanLoginRobotMode,
  showPassword: boolean,
): string {
  switch (mode) {
    case 'email':
      return 'Identificación del operador.'

    case 'password':
      return showPassword
        ? 'Visor desviado. Tu clave sigue siendo tuya.'
        : 'Protocolo de privacidad activo.'

    case 'authenticating':
      return 'Validando identidad y autorización...'

    case 'error':
      return 'Acceso no validado.'

    case 'success':
      return 'Identidad confirmada. Acceso autorizado.'

    case 'launch':
      return 'Inicializando TitanMDM...'

    default:
      return 'Sistema de acceso Titan en espera.'
  }
}

export function TitanLoginRobot({
  mode,
  emailValue,
  showPassword,
}: TitanLoginRobotProps) {
  const [bootMs] = useState(bootDuration)

  const [booting, setBooting] =
    useState(bootMs > 0)

  const [reaction, setReaction] =
    useState<RobotReaction>('none')

  const previousMode =
    useRef<TitanLoginRobotMode>(mode)

  // Secuencia de encendido del robot: solo se ejecuta al montar y
  // termina sola, sin dejar temporizadores vivos.
  useEffect(() => {
    if (bootMs <= 0) {
      return
    }

    const timer = window.setTimeout(
      () => setBooting(false),
      bootMs,
    )

    return () => window.clearTimeout(timer)
  }, [bootMs])

  // La secuencia de encendido nunca debe solaparse con una
  // autenticación real: si el operador ya envio credenciales, se
  // interrumpe de inmediato para no enmascarar el estado real.
  useEffect(() => {
    if (!booting) {
      return
    }

    if (
      mode === 'authenticating' ||
      mode === 'success' ||
      mode === 'launch'
    ) {
      setBooting(false)
    }
  }, [mode, booting])

  // Reacción breve al cambiar de campo (correo <-> contraseña) o al
  // entrar en un estado de atención. No lee ni usa el contenido de la
  // contraseña: solo observa la transición de estado.
  useEffect(() => {
    const previous = previousMode.current

    if (previous === mode) {
      return
    }

    previousMode.current = mode

    if (bootMs <= 0) {
      return
    }

    const fieldSwap =
      (previous === 'email' &&
        mode === 'password') ||
      (previous === 'password' &&
        mode === 'email')

    setReaction(fieldSwap ? 'field' : 'pulse')

    const timer = window.setTimeout(
      () => setReaction('none'),
      fieldSwap
        ? REACTION_FIELD_MS
        : REACTION_PULSE_MS,
    )

    return () => window.clearTimeout(timer)
  }, [mode, bootMs])

  const eyeShift =
    Math.min(
      emailValue.length * 0.45,
      8,
    )

  const reactClass =
    reaction === 'field'
      ? ' titan-mech--react-field'
      : reaction === 'pulse'
        ? ' titan-mech--react-pulse'
        : ''

  const bootClass =
    booting
      ? ' titan-mech--booting'
      : ''

  const style = {
    ['--eye-shift' as string]:
      `${eyeShift}px`,
  } as CSSProperties

  return (
    <div
      className={
        `titan-mech ` +
        `titan-mech--${mode}` +
        bootClass +
        reactClass
      }
      style={style}
      aria-hidden="true"
    >
      <div className="titan-mech__message">
        <span className="titan-mech__message-dot" />

        {booting
          ? 'Sistema Titan encendiendo...'
          : getMessage(
              mode,
              showPassword,
            )}
      </div>

      <div className="titan-mech__energy-field" />

      <div className="titan-mech__flight-trail">
        <i />
        <i />
        <i />
      </div>

      <div className="titan-mech__unit">
        <div className="titan-mech__backpack">
          <span className="titan-mech__backpack-wing titan-mech__backpack-wing--left" />
          <span className="titan-mech__backpack-wing titan-mech__backpack-wing--right" />

          <span className="titan-mech__engine titan-mech__engine--left" />
          <span className="titan-mech__engine titan-mech__engine--right" />
        </div>

        <div className="titan-mech__head">
          <span className="titan-mech__antenna" />
          <span className="titan-mech__crest" />

          <span className="titan-mech__helmet-fin titan-mech__helmet-fin--left" />
          <span className="titan-mech__helmet-fin titan-mech__helmet-fin--right" />

          <div className="titan-mech__helmet">
            <div className="titan-mech__visor">
              <span className="titan-mech__eye titan-mech__eye--left" />
              <span className="titan-mech__eye titan-mech__eye--right" />

              <span className="titan-mech__pupil titan-mech__pupil--left" />
              <span className="titan-mech__pupil titan-mech__pupil--right" />

              <span className="titan-mech__scanner" />
              <span className="titan-mech__privacy" />

              <span className="titan-mech__status-symbol">
                T
              </span>
            </div>

            <div className="titan-mech__jaw">
              <span />
              <span />
              <span />
              <span />
            </div>
          </div>
        </div>

        <div className="titan-mech__upper-body">
          <div className="titan-mech__shoulder titan-mech__shoulder--left">
            <span />
          </div>

          <div className="titan-mech__shoulder titan-mech__shoulder--right">
            <span />
          </div>

          <div className="titan-mech__chest">
            <span className="titan-mech__chest-plate titan-mech__chest-plate--left" />
            <span className="titan-mech__chest-plate titan-mech__chest-plate--right" />

            <div className="titan-mech__core-ring">
              <div className="titan-mech__core">
                T
              </div>
            </div>

            <div className="titan-mech__vents">
              <span />
              <span />
              <span />
            </div>
          </div>
        </div>

        <div className="titan-mech__arm titan-mech__arm--left">
          <div className="titan-mech__upper-arm" />
          <div className="titan-mech__elbow" />
          <div className="titan-mech__forearm" />

          <div className="titan-mech__hand titan-mech__hand--left">
            <span className="finger finger--1" />
            <span className="finger finger--2" />
            <span className="finger finger--3" />
            <span className="finger finger--4" />
          </div>
        </div>

        <div className="titan-mech__arm titan-mech__arm--right">
          <div className="titan-mech__upper-arm" />
          <div className="titan-mech__elbow" />
          <div className="titan-mech__forearm" />

          <div className="titan-mech__hand titan-mech__hand--right">
            <span className="finger finger--1" />
            <span className="finger finger--2" />
            <span className="finger finger--3" />
            <span className="finger finger--4" />

            <span className="titan-mech__approval-thumb" />
          </div>
        </div>

        <div className="titan-mech__lower-body">
          <div className="titan-mech__waist" />

          <div className="titan-mech__leg titan-mech__leg--left">
            <span className="titan-mech__thigh" />
            <span className="titan-mech__shin" />
          </div>

          <div className="titan-mech__leg titan-mech__leg--right">
            <span className="titan-mech__thigh" />
            <span className="titan-mech__shin" />
          </div>
        </div>

        <div className="titan-mech__thrusters">
          <span className="titan-mech__thruster titan-mech__thruster--left">
            <i />
          </span>

          <span className="titan-mech__thruster titan-mech__thruster--right">
            <i />
          </span>
        </div>
      </div>
    </div>
  )
}