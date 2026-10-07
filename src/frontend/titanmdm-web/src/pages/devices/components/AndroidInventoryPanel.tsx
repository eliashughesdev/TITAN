import {
  CheckCircle2,
  Clock3,
  ShieldAlert,
  Smartphone,
  TriangleAlert,
} from 'lucide-react'

import type {
  AndroidDeviceInventorySummary,
  AndroidDeviceSyncResult,
} from '../../../types/androidEnterprise'

import {
  formatDeviceDateTime,
} from '../utils/devicesPage.utils'

interface Props {
  summary:
    AndroidDeviceInventorySummary | null

  lastSyncResult:
    AndroidDeviceSyncResult | null

  error:
    string | null
}

export function AndroidInventoryPanel({
  summary,
  lastSyncResult,
  error,
}: Props) {
  return (
    <section className="android-inventory-panel">
      <div className="android-inventory-panel__header">
        <div className="android-inventory-panel__identity">
          <div className="android-inventory-panel__logo">
            <Smartphone
              size={22}
            />
          </div>

          <div>
            <span>
              ANDROID ENTERPRISE
            </span>

            <h2>
              Flota administrada
            </h2>

            <p>
              Inventario sincronizado directamente con Android Management API.
            </p>
          </div>
        </div>

        <div className="android-last-sync">
          <Clock3
            size={15}
          />

          <div>
            <span>
              Última sincronización
            </span>

            <strong>
              {formatDeviceDateTime(
                summary
                  ?.lastSynchronizationUtc
                ??
                null,
              )}
            </strong>
          </div>
        </div>
      </div>

      <div className="android-inventory-metrics">
        <div>
          <span>
            Inventario Android
          </span>

          <strong>
            {summary
              ?.total
            ??
            0}
          </strong>
        </div>

        <div>
          <span>
            Administrados
          </span>

          <strong>
            {summary
              ?.managed
            ??
            0}
          </strong>
        </div>

        <div>
          <span>
            Totalmente administrados
          </span>

          <strong>
            {summary
              ?.fullyManaged
            ??
            0}
          </strong>
        </div>

        <div>
          <span>
            Kiosk / dedicados
          </span>

          <strong>
            {summary
              ?.dedicated
            ??
            0}
          </strong>
        </div>

        <div>
          <span>
            Perfil de trabajo
          </span>

          <strong>
            {summary
              ?.workProfile
            ??
            0}
          </strong>
        </div>

        <div>
          <span>
            Ausentes en Google
          </span>

          <strong>
            {summary
              ?.missingInGoogle
            ??
            0}
          </strong>
        </div>
      </div>

      {lastSyncResult && (
        <div
          className={
            lastSyncResult.failed >
            0
              ? 'android-sync-result android-sync-result--warning'
              : 'android-sync-result android-sync-result--success'
          }
        >
          {lastSyncResult.failed >
          0 ? (
            <TriangleAlert
              size={19}
            />
          ) : (
            <CheckCircle2
              size={19}
            />
          )}

          <div>
            <strong>
              Sincronización finalizada
            </strong>

            <span>
              Google reportó{' '}
              {
                lastSyncResult
                  .receivedFromGoogle
              }{' '}
              dispositivo
              {lastSyncResult
                .receivedFromGoogle ===
              1
                ? ''
                : 's'}
              .
              {' '}
              Nuevos:{' '}
              {
                lastSyncResult
                  .created
              }.
              {' '}
              Actualizados:{' '}
              {
                lastSyncResult
                  .updated
              }.
              {' '}
              Ausentes:{' '}
              {
                lastSyncResult
                  .markedMissing
              }.
              {' '}
              Errores:{' '}
              {
                lastSyncResult
                  .failed
              }.
            </span>

            {lastSyncResult
              .errors
              .length >
              0 && (
              <ul>
                {lastSyncResult
                  .errors
                  .map(
                    (
                      message,
                      index,
                    ) => (
                      <li
                        key={
                          `${index}-${message}`
                        }
                      >
                        {message}
                      </li>
                    ),
                  )}
              </ul>
            )}
          </div>
        </div>
      )}

      {error && (
        <div className="android-sync-result android-sync-result--error">
          <ShieldAlert
            size={19}
          />

          <div>
            <strong>
              Android Enterprise
            </strong>

            <span>
              {error}
            </span>
          </div>
        </div>
      )}
    </section>
  )
}