import { Plus } from 'lucide-react'

import type {
  GroupMode,
} from '../deviceGroups.types'

interface GroupBuilderProps {
  name: string
  description: string
  mode: GroupMode
  dynamicPlatform: string
  dynamicStatus: string
  working: boolean
  onNameChange: (value: string) => void
  onDescriptionChange: (value: string) => void
  onModeChange: (value: GroupMode) => void
  onDynamicPlatformChange: (value: string) => void
  onDynamicStatusChange: (value: string) => void
  onCreate: () => void
}

export function GroupBuilder({
  name,
  description,
  mode,
  dynamicPlatform,
  dynamicStatus,
  working,
  onNameChange,
  onDescriptionChange,
  onModeChange,
  onDynamicPlatformChange,
  onDynamicStatusChange,
  onCreate,
}: GroupBuilderProps) {
  return (
    <section className="fleet-builder">
      <div className="fleet-panel-title">
        <div>
          <Plus size={18} />
          <strong>Crear grupo</strong>
        </div>
      </div>

      <div className="fleet-form-grid">
        <label>
          Nombre

          <input
            value={name}
            onChange={event =>
              onNameChange(
                event.target.value,
              )
            }
            placeholder="Windows - Operaciones"
          />
        </label>

        <label>
          Tipo

          <select
            value={mode}
            onChange={event =>
              onModeChange(
                event.target.value as GroupMode,
              )
            }
          >
            <option value="static">
              Estático
            </option>

            <option value="dynamic">
              Dinámico
            </option>
          </select>
        </label>
      </div>

      <label>
        Descripción

        <input
          value={description}
          onChange={event =>
            onDescriptionChange(
              event.target.value,
            )
          }
          placeholder="Flota Windows administrada por TitanMDM"
        />
      </label>

      {mode === 'dynamic' && (
        <div className="fleet-dynamic-rule">
          <strong>Regla dinámica</strong>

          <label>
            Plataforma

            <select
              value={dynamicPlatform}
              onChange={event =>
                onDynamicPlatformChange(
                  event.target.value,
                )
              }
            >
              <option value="Windows">
                Windows
              </option>

              <option value="Android">
                Android
              </option>
            </select>
          </label>

          <label>
            Estado

            <select
              value={dynamicStatus}
              onChange={event =>
                onDynamicStatusChange(
                  event.target.value,
                )
              }
            >
              <option value="Online">
                Online
              </option>

              <option value="Offline">
                Offline
              </option>

              <option value="Quarantined">
                Quarantined
              </option>
            </select>
          </label>
        </div>
      )}

      <button
        type="button"
        className="fleet-primary"
        onClick={onCreate}
        disabled={working}
      >
        <Plus size={16} />
        Crear grupo
      </button>
    </section>
  )
}
