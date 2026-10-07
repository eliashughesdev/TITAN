import {
  Edit3,
  Save,
  X,
} from 'lucide-react'

import type {
  DeviceGroupDetails,
} from '../../../api/deviceGroupsApi'

interface SelectedGroupPanelProps {
  group: DeviceGroupDetails

  editing: boolean

  editName: string

  editDescription: string

  editDynamicPlatform: string

  editDynamicStatus: string

  working: boolean

  onEditingChange:
    (value: boolean) => void

  onEditNameChange:
    (value: string) => void

  onEditDescriptionChange:
    (value: string) => void

  onEditDynamicPlatformChange:
    (value: string) => void

  onEditDynamicStatusChange:
    (value: string) => void

  onSave: () => void

  onClose: () => void
}

export function SelectedGroupPanel({
  group,
  editing,
  editName,
  editDescription,
  editDynamicPlatform,
  editDynamicStatus,
  working,
  onEditingChange,
  onEditNameChange,
  onEditDescriptionChange,
  onEditDynamicPlatformChange,
  onEditDynamicStatusChange,
  onSave,
  onClose,
}: SelectedGroupPanelProps) {
  return (
    <section className="fleet-selected-group">
      {!editing ? (
        <div>
          <span>
            GRUPO ACTIVO
          </span>

          <h2>
            {group.name}
          </h2>

          <p>
            {group.description
              ?? 'Sin descripción'}
          </p>

          <small>
            {group.isDynamic
              ? 'Grupo dinámico'
              : 'Grupo estático'}

            {' · '}

            {group.members.length}{' '}
            miembro(s)
          </small>

          {group.isDynamic && (
            <div className="fleet-rule-summary">
              <strong>
                Regla automática
              </strong>

              <span>
                Plataforma:{' '}
                {editDynamicPlatform}
              </span>

              <span>
                Estado:{' '}
                {editDynamicStatus}
              </span>
            </div>
          )}
        </div>
      ) : (
        <div className="fleet-edit-group">
          <div className="fleet-form-grid">
            <label>
              Nombre

              <input
                value={editName}
                onChange={event =>
                  onEditNameChange(
                    event.target.value,
                  )
                }
              />
            </label>

            <label>
              Descripción

              <input
                value={
                  editDescription
                }
                onChange={event =>
                  onEditDescriptionChange(
                    event.target.value,
                  )
                }
              />
            </label>
          </div>

          {group.isDynamic && (
            <div className="fleet-dynamic-rule">
              <strong>
                Regla dinámica
              </strong>

              <label>
                Plataforma

                <select
                  value={
                    editDynamicPlatform
                  }
                  onChange={event =>
                    onEditDynamicPlatformChange(
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
                  value={
                    editDynamicStatus
                  }
                  onChange={event =>
                    onEditDynamicStatusChange(
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
        </div>
      )}

      <div>
        {!editing ? (
          <button
            type="button"
            className="fleet-secondary"
            onClick={() =>
              onEditingChange(
                true,
              )
            }
          >
            <Edit3 size={16} />

            Editar
          </button>
        ) : (
          <button
            type="button"
            className="fleet-primary"
            disabled={working}
            onClick={onSave}
          >
            <Save size={16} />

            Guardar
          </button>
        )}

        <button
          type="button"
          className="fleet-close"
          onClick={onClose}
          aria-label="Cerrar grupo"
        >
          <X size={17} />
        </button>
      </div>
    </section>
  )
}