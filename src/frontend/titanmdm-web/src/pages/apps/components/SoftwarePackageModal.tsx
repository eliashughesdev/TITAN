import {
  Upload,
  X,
} from 'lucide-react'

import {
  useState,
} from 'react'

import type {
  UploadSoftwarePackageRequest,
} from '../../../api/applicationsApi'

interface Props {
  busy: boolean

  onClose: () => void

  onUpload: (
    request:
      UploadSoftwarePackageRequest,
  ) => Promise<void>
}

export function SoftwarePackageModal({
  busy,
  onClose,
  onUpload,
}: Props) {
  const [
    name,
    setName,
  ] =
    useState('')

  const [
    version,
    setVersion,
  ] =
    useState('')

  const [
    packageType,
    setPackageType,
  ] =
    useState<
      'MSI' |
      'EXE' |
      'MSIX' |
      'APPX'
    >('MSI')

  const [
    installArguments,
    setInstallArguments,
  ] =
    useState('')

  const [
    file,
    setFile,
  ] =
    useState<File | null>(
      null,
    )

  async function submit() {
    if (
      !name.trim()
      ||
      !version.trim()
      ||
      !file
    ) {
      window.alert(
        'Nombre, versión y archivo son obligatorios.',
      )

      return
    }

    const extension =
      file.name
        .split('.')
        .pop()
        ?.toUpperCase()

    if (
      extension !==
      packageType
    ) {
      window.alert(
        `El archivo seleccionado es ${extension ?? 'desconocido'} pero el tipo configurado es ${packageType}.`,
      )

      return
    }

    await onUpload({
      name:
        name.trim(),

      version:
        version.trim(),

      packageType,

      installArguments:
        installArguments.trim()
        ||
        undefined,

      file,
    })
  }

  return (
    <div className="apps-modal-backdrop">
      <div className="apps-modal">
        <header>
          <div>
            <span>
              SOFTWARE PACKAGE
            </span>

            <h3>
              Agregar paquete
            </h3>
          </div>

          <button
            type="button"
            onClick={onClose}
            disabled={busy}
          >
            <X
              size={18}
            />
          </button>
        </header>

        <div className="apps-modal-form">
          <label>
            <span>
              Nombre
            </span>

            <input
              value={name}
              placeholder="Google Chrome"
              onChange={
                event =>
                  setName(
                    event.target.value,
                  )
              }
            />
          </label>

          <label>
            <span>
              Versión
            </span>

            <input
              value={version}
              placeholder="153.0.1"
              onChange={
                event =>
                  setVersion(
                    event.target.value,
                  )
              }
            />
          </label>

          <label>
            <span>
              Tipo de paquete
            </span>

            <select
              value={packageType}
              onChange={
                event =>
                  setPackageType(
                    event.target.value as
                      | 'MSI'
                      | 'EXE'
                      | 'MSIX'
                      | 'APPX',
                  )
              }
            >
              <option value="MSI">
                MSI
              </option>

              <option value="EXE">
                EXE
              </option>

              <option value="MSIX">
                MSIX
              </option>

              <option value="APPX">
                APPX
              </option>
            </select>
          </label>

          <label>
            <span>
              Argumentos silenciosos
            </span>

            <input
              value={
                installArguments
              }
              placeholder="/quiet /norestart"
              onChange={
                event =>
                  setInstallArguments(
                    event.target.value,
                  )
              }
            />
          </label>

          <label className="apps-file-field">
            <span>
              Instalador
            </span>

            <input
              type="file"
              accept=".msi,.exe,.msix,.appx"
              onChange={
                event =>
                  setFile(
                    event.target
                      .files?.[0]
                    ??
                    null,
                  )
              }
            />

            {file && (
              <small>
                {file.name}
                {' · '}
                {(
                  file.size
                  /
                  1024
                  /
                  1024
                ).toFixed(1)}
                {' MB'}
              </small>
            )}
          </label>
        </div>

        <footer>
          <button
            type="button"
            disabled={busy}
            onClick={onClose}
          >
            Cancelar
          </button>

          <button
            type="button"
            className="primary"
            disabled={
              busy
              ||
              !file
            }
            onClick={() =>
              void submit()
            }
          >
            <Upload
              size={15}
            />

            {busy
              ? 'Subiendo...'
              : 'Subir paquete'}
          </button>
        </footer>
      </div>
    </div>
  )
}