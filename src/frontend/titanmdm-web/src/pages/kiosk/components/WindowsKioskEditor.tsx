import {
  AppWindow,
  Plus,
  Rocket,
  Trash2,
} from 'lucide-react'

import {
  useState,
} from 'react'

import type {
  KioskMode,
  WindowsKioskApplication,
  WindowsKioskRestrictions,
} from '../../../api/kioskApi'

interface Props {
  saving: boolean

  onCreate:
    (
      request: {
        name: string

        description?:
          string

        mode:
          KioskMode

        account:
          string

        applications:
          WindowsKioskApplication[]

        restrictions:
          WindowsKioskRestrictions
      },
    ) => Promise<void>
}

const initialRestrictions:
  WindowsKioskRestrictions = {
    showTaskbar:
      false,

    blockTaskManager:
      true,

    blockSettings:
      true,

    blockCommandPrompt:
      true,

    blockRemovableStorage:
      true,
  }

export function WindowsKioskEditor({
  saving,
  onCreate,
}: Props) {
  const [
    name,
    setName,
  ] =
    useState('')

  const [
    description,
    setDescription,
  ] =
    useState('')

  const [
    account,
    setAccount,
  ] =
    useState(
      '.\\TitanKiosk',
    )

  const [
    mode,
    setMode,
  ] =
    useState<KioskMode>(
      'singleApp',
    )

  const [
    applications,
    setApplications,
  ] =
    useState<
      WindowsKioskApplication[]
    >([])

  const [
    displayName,
    setDisplayName,
  ] =
    useState('')

  const [
    appUserModelId,
    setAppUserModelId,
  ] =
    useState('')

  const [
    desktopAppPath,
    setDesktopAppPath,
  ] =
    useState('')

  const [
    restrictions,
    setRestrictions,
  ] =
    useState(
      initialRestrictions,
    )

  function addApplication() {
    const aumid =
      appUserModelId.trim()

    const desktop =
      desktopAppPath.trim()

    if (
      !aumid
      &&
      !desktop
    ) {
      window.alert(
        'Debes indicar AppUserModelId o DesktopAppPath.',
      )

      return
    }

    if (
      mode ===
        'singleApp'
      &&
      !aumid
    ) {
      window.alert(
        'Single App requiere AppUserModelId. Las aplicaciones Win32 se administran mediante Multi App.',
      )

      return
    }

    if (
      mode ===
        'singleApp'
      &&
      applications.length >
        0
    ) {
      window.alert(
        'Single App admite una sola aplicación.',
      )

      return
    }

    setApplications(
      current => [
        ...current,
        {
          displayName:
            displayName.trim()
            ||
            aumid
            ||
            desktop,

          appUserModelId:
            aumid
            ||
            undefined,

          desktopAppPath:
            desktop
            ||
            undefined,
        },
      ],
    )

    setDisplayName('')
    setAppUserModelId('')
    setDesktopAppPath('')
  }

  function setRestriction(
    key:
      keyof WindowsKioskRestrictions,
    value:
      boolean,
  ) {
    setRestrictions(
      current => ({
        ...current,
        [key]:
          value,
      }),
    )
  }

  async function submit() {
    if (
      !name.trim())
    {
      window.alert(
        'El perfil necesita un nombre.',
      )

      return
    }

    if (
      !account.trim())
    {
      window.alert(
        'Indica la cuenta Windows del Kiosk.',
      )

      return
    }

    if (
      applications.length ===
      0)
    {
      window.alert(
        'Agrega al menos una aplicación.',
      )

      return
    }

    await onCreate({
      name:
        name.trim(),

      description:
        description.trim()
        ||
        undefined,

      mode,

      account:
        account.trim(),

      applications,

      restrictions,
    })

    setName('')
    setDescription('')
    setApplications([])
  }

  return (
    <section className="kiosk-editor">
      <div className="kiosk-section-title">
        <div>
          <Plus size={18} />

          <strong>
            Nuevo Kiosk Windows
          </strong>
        </div>

        <span>
          Assigned Access administrado por TitanMDM
        </span>
      </div>

      <label>
        Nombre
        <input
          value={name}
          onChange={
            event =>
              setName(
                event.target.value,
              )
          }
          placeholder="Kiosk Producción"
        />
      </label>

      <label>
        Descripción
        <textarea
          value={description}
          onChange={
            event =>
              setDescription(
                event.target.value,
              )
          }
          placeholder="Equipos dedicados..."
        />
      </label>

      <label>
        Cuenta Windows
        <input
          value={account}
          onChange={
            event =>
              setAccount(
                event.target.value,
              )
          }
          placeholder=".\TitanKiosk"
        />
      </label>

      <div className="kiosk-mode-grid">
        <button
          type="button"
          className={
            mode ===
            'singleApp'
              ? 'kiosk-mode selected'
              : 'kiosk-mode'
          }
          onClick={() => {
            setMode(
              'singleApp',
            )

            setApplications(
              current =>
                current.slice(
                  0,
                  1,
                ),
            )
          }}
        >
          <AppWindow size={22} />

          <strong>
            Single App
          </strong>

          <span>
            Una aplicación UWP/AUMID en pantalla dedicada.
          </span>
        </button>

        <button
          type="button"
          className={
            mode ===
            'multiApp'
              ? 'kiosk-mode selected'
              : 'kiosk-mode'
          }
          onClick={() =>
            setMode(
              'multiApp',
            )
          }
        >
          <AppWindow size={22} />

          <strong>
            Multi App
          </strong>

          <span>
            Aplicaciones UWP y Win32 autorizadas.
          </span>
        </button>
      </div>

      <div className="kiosk-app-builder">
        <h3>
          Aplicaciones permitidas
        </h3>

        <div className="kiosk-windows-app-inputs">
          <input
            value={displayName}
            onChange={
              event =>
                setDisplayName(
                  event.target.value,
                )
            }
            placeholder="Nombre visible"
          />

          <input
            value={appUserModelId}
            onChange={
              event =>
                setAppUserModelId(
                  event.target.value,
                )
            }
            placeholder="AppUserModelId"
          />

          <input
            value={desktopAppPath}
            onChange={
              event =>
                setDesktopAppPath(
                  event.target.value,
                )
            }
            placeholder="C:\Program Files\App\App.exe"
          />

          <button
            type="button"
            onClick={
              addApplication
            }
          >
            <Plus size={15} />

            Agregar
          </button>
        </div>

        {applications.map(
          (
            application,
            index,
          ) => (
            <div
              className="kiosk-app-row"
              key={
                `${application.displayName}-${index}`
              }
            >
              <div>
                <strong>
                  {application.displayName}
                </strong>

                <span>
                  {application.appUserModelId
                  ??
                  application.desktopAppPath}
                </span>
              </div>

              <span>
                {application.appUserModelId
                  ? 'AUMID'
                  : 'Win32'}
              </span>

              <button
                type="button"
                onClick={() =>
                  setApplications(
                    current =>
                      current.filter(
                        (
                          _,
                          itemIndex,
                        ) =>
                          itemIndex !==
                          index,
                      ),
                  )
                }
              >
                <Trash2 size={15} />
              </button>
            </div>
          ),
        )}
      </div>

      <div className="kiosk-restrictions">
        <h3>
          Restricciones
        </h3>

        <Toggle
          label="Mostrar barra de tareas"
          checked={
            restrictions
              .showTaskbar
          }
          onChange={
            value =>
              setRestriction(
                'showTaskbar',
                value,
              )
          }
        />

        <Toggle
          label="Bloquear Task Manager"
          checked={
            restrictions
              .blockTaskManager
          }
          onChange={
            value =>
              setRestriction(
                'blockTaskManager',
                value,
              )
          }
        />

        <Toggle
          label="Bloquear Settings / Control Panel"
          checked={
            restrictions
              .blockSettings
          }
          onChange={
            value =>
              setRestriction(
                'blockSettings',
                value,
              )
          }
        />

        <Toggle
          label="Bloquear Command Prompt"
          checked={
            restrictions
              .blockCommandPrompt
          }
          onChange={
            value =>
              setRestriction(
                'blockCommandPrompt',
                value,
              )
          }
        />

        <Toggle
          label="Bloquear almacenamiento removible"
          checked={
            restrictions
              .blockRemovableStorage
          }
          onChange={
            value =>
              setRestriction(
                'blockRemovableStorage',
                value,
              )
          }
        />
      </div>

      <button
        type="button"
        className="kiosk-create"
        disabled={saving}
        onClick={() =>
          void submit()
        }
      >
        <Rocket size={17} />

        {saving
          ? 'Creando...'
          : 'Crear perfil Windows'}
      </button>
    </section>
  )
}

function Toggle({
  label,
  checked,
  onChange,
}: {
  label: string

  checked: boolean

  onChange:
    (
      value:
        boolean,
    ) => void
}) {
  return (
    <label className="kiosk-toggle">
      <span>
        {label}
      </span>

      <input
        type="checkbox"
        checked={checked}
        onChange={
          event =>
            onChange(
              event.target
                .checked,
            )
        }
      />
    </label>
  )
}