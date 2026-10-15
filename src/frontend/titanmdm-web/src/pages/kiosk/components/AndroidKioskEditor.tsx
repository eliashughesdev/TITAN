import { LockKeyhole, Rocket, Smartphone } from 'lucide-react'
import { useState } from 'react'

import type { AndroidKioskConfiguration } from '../../../api/kioskApi'

interface Props {
  saving: boolean
  canManage: boolean
  onCreate: (request: {
    name: string
    description?: string
    configuration: AndroidKioskConfiguration
  }) => Promise<boolean>
}

const initialRestrictions = {
  factoryResetDisabled: true,
  safeBootDisabled: true,
  screenCaptureDisabled: true,
  usbFileTransferDisabled: true,
  bluetoothDisabled: false,
  cameraDisabled: false,
}

export function AndroidKioskEditor({ saving, canManage, onCreate }: Props) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [packageName, setPackageName] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [disableStatusBar, setDisableStatusBar] = useState(true)
  const [disableKeyguard, setDisableKeyguard] = useState(true)
  const [restrictions, setRestrictions] = useState(initialRestrictions)

  async function submit() {
    if (!name.trim() || !packageName.trim()) return

    const created = await onCreate({
      name: name.trim(),
      description: description.trim() || undefined,
      configuration: {
        application: {
          packageName: packageName.trim(),
          displayName: displayName.trim() || packageName.trim(),
        },
        disableStatusBar,
        disableKeyguard,
        deviceRestrictions: restrictions,
      },
    })

    if (created) {
      setName('')
      setDescription('')
      setPackageName('')
      setDisplayName('')
    }
  }

  const setRestriction = (
    key: keyof typeof initialRestrictions,
    checked: boolean,
  ) => setRestrictions((current) => ({ ...current, [key]: checked }))

  return (
    <div className="kiosk-editor-content">
      {!canManage && (
        <div className="kiosk-permission-note">
          Modo lectura. Se requiere `android.enterprise.manage` para crear y publicar.
        </div>
      )}

      <div className="kiosk-form-grid">
        <label>
          <span>Nombre del perfil</span>
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Punto de venta Android"
          />
        </label>
        <label>
          <span>Nombre visible</span>
          <input
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
            placeholder="Titan POS"
          />
        </label>
        <label className="kiosk-form-wide">
          <span>Package name</span>
          <input
            value={packageName}
            onChange={(event) => setPackageName(event.target.value)}
            placeholder="com.empresa.pos"
            autoCapitalize="none"
          />
        </label>
        <label className="kiosk-form-wide">
          <span>Descripción</span>
          <textarea
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Dispositivos dedicados de atención..."
          />
        </label>
      </div>

      <div className="kiosk-editor-callout">
        <Smartphone size={20} />
        <div>
          <strong>Single app administrado</strong>
          <span>La aplicación se compila con installType KIOSK mediante Android Management API.</span>
        </div>
      </div>

      <fieldset className="kiosk-option-grid">
        <legend><LockKeyhole size={16} /> Experiencia y restricciones</legend>
        <KioskCheck label="Ocultar barra de estado" checked={disableStatusBar} onChange={setDisableStatusBar} />
        <KioskCheck label="Deshabilitar Keyguard" checked={disableKeyguard} onChange={setDisableKeyguard} />
        <KioskCheck label="Bloquear restablecimiento" checked={restrictions.factoryResetDisabled} onChange={(value) => setRestriction('factoryResetDisabled', value)} />
        <KioskCheck label="Bloquear modo seguro" checked={restrictions.safeBootDisabled} onChange={(value) => setRestriction('safeBootDisabled', value)} />
        <KioskCheck label="Bloquear capturas" checked={restrictions.screenCaptureDisabled} onChange={(value) => setRestriction('screenCaptureDisabled', value)} />
        <KioskCheck label="Bloquear archivos por USB" checked={restrictions.usbFileTransferDisabled} onChange={(value) => setRestriction('usbFileTransferDisabled', value)} />
        <KioskCheck label="Bloquear Bluetooth" checked={restrictions.bluetoothDisabled} onChange={(value) => setRestriction('bluetoothDisabled', value)} />
        <KioskCheck label="Bloquear cámara" checked={restrictions.cameraDisabled} onChange={(value) => setRestriction('cameraDisabled', value)} />
      </fieldset>

      <button
        type="button"
        className="titan-btn titan-btn--primary kiosk-submit"
        disabled={!canManage || saving || !name.trim() || !packageName.trim()}
        onClick={() => void submit()}
      >
        <Rocket size={17} />
        {saving ? 'Creando y publicando...' : 'Crear, activar y publicar'}
      </button>
    </div>
  )
}

function KioskCheck({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <label className="kiosk-check">
      <input
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <span>{label}</span>
    </label>
  )
}
