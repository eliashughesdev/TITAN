import type {
  ReactNode,
} from 'react'

import {
  EyeOff,
  Gauge,
  MousePointer2,
  Moon,
  RotateCcw,
  Rocket,
  Sparkles,
  Volume2,
  X,
} from 'lucide-react'

import {
  useTitanAssistant,
} from '../context/TitanAssistantContext'

interface Props {
  onClose: () => void
}

export function TitanPreferencesPanel({
  onClose,
}: Props) {
  const {
    preferences,

    setVisible,
    setDoNotDisturb,
    setAutonomousBehavior,
    setProactiveComments,
    setFollowPointer,
    setReducedMotion,
    setAllowFlight,
    setSoundEnabled,

    resetPreferences,
  } =
    useTitanAssistant()

  return (
    <div
      className="titan-preferences"
      data-titan-blocking="true"
    >
      <div className="titan-preferences__header">
        <div>
          <strong>
            Preferencias de Titan
          </strong>

          <span>
            Personaliza el comportamiento del asistente.
          </span>
        </div>

        <button
          type="button"
          onClick={onClose}
          aria-label="Cerrar preferencias"
        >
          <X size={16} />
        </button>
      </div>

      <div className="titan-preferences__options">
        <PreferenceToggle
          icon={
            <Sparkles
              size={16}
            />
          }
          title="Comportamiento contextual"
          description="Titan puede reaccionar a lo que ocurre en el módulo actual."
          checked={
            preferences.autonomousBehavior
          }
          onChange={
            setAutonomousBehavior
          }
        />

        <PreferenceToggle
          icon={
            <MousePointer2
              size={16}
            />
          }
          title="Reaccionar al puntero"
          description="Titan podrá mirar y, ocasionalmente, acercarse al cursor."
          checked={
            preferences.followPointer
          }
          onChange={
            setFollowPointer
          }
        />

        <PreferenceToggle
          icon={
            <Rocket
              size={16}
            />
          }
          title="Permitir vuelo"
          description="Usa las turbinas cuando un desplazamiento dirigido sea largo."
          checked={
            preferences.allowFlight
          }
          onChange={
            setAllowFlight
          }
        />

        <PreferenceToggle
          icon={
            <Sparkles
              size={16}
            />
          }
          title="Comentarios proactivos"
          description="Permite comentarios relacionados con el contexto actual."
          checked={
            preferences.proactiveComments
          }
          onChange={
            setProactiveComments
          }
        />

        <PreferenceToggle
          icon={
            <Moon
              size={16}
            />
          }
          title="No molestar"
          description="Suspende comentarios y comportamientos proactivos."
          checked={
            preferences.doNotDisturb
          }
          onChange={
            setDoNotDisturb
          }
        />

        <PreferenceToggle
          icon={
            <Gauge
              size={16}
            />
          }
          title="Reducir movimiento"
          description="Reduce animaciones ambientales y desplazamientos secundarios."
          checked={
            preferences.reducedMotion
          }
          onChange={
            setReducedMotion
          }
        />

        <PreferenceToggle
          icon={
            <Volume2
              size={16}
            />
          }
          title="Sonido"
          description="Reserva sonidos sutiles para interacciones futuras."
          checked={
            preferences.soundEnabled
          }
          onChange={
            setSoundEnabled
          }
        />
      </div>

      <div className="titan-preferences__footer">
        <button
          type="button"
          onClick={
            resetPreferences
          }
        >
          <RotateCcw
            size={14}
          />

          Restablecer
        </button>

        <button
          type="button"
          className="titan-preferences__hide"
          onClick={() =>
            setVisible(false)
          }
        >
          <EyeOff
            size={14}
          />

          Ocultar Titan
        </button>
      </div>
    </div>
  )
}

interface PreferenceToggleProps {
  icon: ReactNode

  title: string

  description: string

  checked: boolean

  onChange:
    (value: boolean) => void
}

function PreferenceToggle({
  icon,
  title,
  description,
  checked,
  onChange,
}: PreferenceToggleProps) {
  return (
    <label className="titan-preference">
      <span className="titan-preference__icon">
        {icon}
      </span>

      <span className="titan-preference__text">
        <strong>
          {title}
        </strong>

        <span>
          {description}
        </span>
      </span>

      <input
        type="checkbox"
        checked={checked}
        onChange={(event) =>
          onChange(
            event.target.checked,
          )
        }
      />

      <span className="titan-switch" />
    </label>
  )
}