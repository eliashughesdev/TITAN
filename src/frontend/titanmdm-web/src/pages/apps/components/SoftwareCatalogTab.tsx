import {
  Boxes,
  Download,
  HardDrive,
  Upload,
} from 'lucide-react'

import type {
  SoftwarePackage,
} from '../../../api/applicationsApi'

interface Props {
  packages:
    SoftwarePackage[]

  busy: boolean

  onUpload:
    () => void

  onDeploy:
    (
      item:
        SoftwarePackage,
    ) => void
}

function formatBytes(
  value: number,
) {
  if (
    value <= 0
  ) {
    return '0 B'
  }

  const units = [
    'B',
    'KB',
    'MB',
    'GB',
  ]

  const index =
    Math.min(
      Math.floor(
        Math.log(value)
        /
        Math.log(1024),
      ),
      units.length - 1,
    )

  return `${
    (
      value
      /
      Math.pow(
        1024,
        index,
      )
    ).toFixed(
      index >= 2
        ? 1
        : 0,
    )
  } ${units[index]}`
}

export function SoftwareCatalogTab({
  packages,
  busy,
  onUpload,
  onDeploy,
}: Props) {
  return (
    <>
      <div className="apps-catalog-toolbar">
        <div>
          <Boxes
            size={18}
          />

          <div>
            <strong>
              Catálogo central
            </strong>

            <span>
              Paquetes Windows administrados
              por TitanMDM.
            </span>
          </div>
        </div>

        <button
          type="button"
          className="primary"
          disabled={busy}
          onClick={onUpload}
        >
          <Upload
            size={15}
          />

          Agregar paquete
        </button>
      </div>

      {packages.length ===
      0 ? (
        <div className="apps-empty-state">
          <HardDrive
            size={30}
          />

          <strong>
            Catálogo vacío
          </strong>

          <span>
            Sube tu primer paquete MSI,
            EXE, MSIX o APPX.
          </span>
        </div>
      ) : (
        <div className="apps-package-grid">
          {packages.map(
            item => (
              <article
                className="apps-package-card"
                key={item.id}
              >
                <div className="apps-package-card-head">
                  <div className="apps-package-icon">
                    <Boxes
                      size={20}
                    />
                  </div>

                  <span
                    className={
                      item.isActive
                        ? 'apps-package-status active'
                        : 'apps-package-status disabled'
                    }
                  >
                    {item.isActive
                      ? 'Activo'
                      : 'Deshabilitado'}
                  </span>
                </div>

                <h3>
                  {item.name}
                </h3>

                <div className="apps-package-meta">
                  <span>
                    Versión
                    <strong>
                      {item.version}
                    </strong>
                  </span>

                  <span>
                    Tipo
                    <strong>
                      {
                        item
                          .packageType
                      }
                    </strong>
                  </span>

                  <span>
                    Tamaño
                    <strong>
                      {formatBytes(
                        item.sizeBytes,
                      )}
                    </strong>
                  </span>
                </div>

                <div className="apps-package-file">
                  <span>
                    Archivo
                  </span>

                  <strong>
                    {
                      item
                        .originalFileName
                    }
                  </strong>
                </div>

                <div className="apps-package-hash">
                  <span>
                    SHA-256
                  </span>

                  <code>
                    {item.sha256}
                  </code>
                </div>

                <button
                  type="button"
                  className="apps-deploy-button"
                  disabled={
                    busy
                    ||
                    !item.isActive
                  }
                  onClick={() =>
                    onDeploy(item)
                  }
                >
                  <Download
                    size={15}
                  />

                  Desplegar
                </button>
              </article>
            ),
          )}
        </div>
      )}
    </>
  )
}