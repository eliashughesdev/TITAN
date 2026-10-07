import type {
  CSSProperties,
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

interface TitanLoginRobotProps {
  mode: TitanLoginRobotMode
  emailValue: string
  showPassword: boolean
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
  const eyeShift =
    Math.min(
      emailValue.length * 0.45,
      8,
    )

  const style = {
    ['--eye-shift' as string]:
      `${eyeShift}px`,
  } as CSSProperties

  return (
    <div
      className={
        `titan-mech ` +
        `titan-mech--${mode}`
      }
      style={style}
      aria-hidden="true"
    >
      <div className="titan-mech__message">
        <span className="titan-mech__message-dot" />

        {getMessage(
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