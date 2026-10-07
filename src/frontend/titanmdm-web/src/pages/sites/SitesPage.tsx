import {
  Building2,
  MapPin,
  Plus,
  RefreshCw,
  ShieldCheck,
  X,
} from 'lucide-react'

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  sitesApi,
  type SaveSiteRequest,
  type Site,
  type SiteLocation,
  type SiteOperationalSummary,
} from '../../api/sitesApi'

import {
  useAuth,
} from '../../auth/AuthContext'

import './SitesPage.css'

// ============================================================
// INITIAL FORM
// ============================================================

const emptySite:
  SaveSiteRequest = {
    code: '',
    name: '',
    description: '',
    address: '',
    city: '',
    province: '',
    country:
      'República Dominicana',
    timeZoneId:
      'America/Santo_Domingo',
  }

// ============================================================
// ERROR
// ============================================================

function getErrorMessage(
  error: unknown,
): string {
  if (
    typeof error === 'object'
    &&
    error !== null
    &&
    'response' in error
  ) {
    const response =
      (
        error as {
          response?: {
            status?: number
            data?: {
              message?: string
            }
          }
        }
      ).response

    if (
      response?.data?.message
    ) {
      return response
        .data
        .message
    }

    if (
      response?.status ===
      403
    ) {
      return 'No tienes permisos para realizar esta operación.'
    }
  }

  if (
    error instanceof Error
  ) {
    return error.message
  }

  return 'Ocurrió un error inesperado.'
}

// ============================================================
// COMPONENT
// ============================================================

export function SitesPage() {
  const {
    hasPermission,
  } =
    useAuth()

  const canManage =
    hasPermission(
      'sites.manage',
    )

  // ==========================================================
  // STATE
  // ==========================================================

  const [
    sites,
    setSites,
  ] =
    useState<Site[]>([])

  const [
    selectedSite,
    setSelectedSite,
  ] =
    useState<Site | null>(
      null,
    )

  const [
    locations,
    setLocations,
  ] =
    useState<
      SiteLocation[]
    >([])

  const [
    summary,
    setSummary,
  ] =
    useState<
      SiteOperationalSummary |
      null
    >(null)

  const [
    error,
    setError,
  ] =
    useState<
      string | null
    >(null)

  const [
    isLoading,
    setIsLoading,
  ] =
    useState(
      true,
    )

  const [
    drawerLoading,
    setDrawerLoading,
  ] =
    useState(
      false,
    )

  const [
    showCreate,
    setShowCreate,
  ] =
    useState(
      false,
    )

  const [
    form,
    setForm,
  ] =
    useState<
      SaveSiteRequest
    >(
      emptySite,
    )

  // ==========================================================
  // LOAD SITES
  // ==========================================================

  const loadSites =
    useCallback(
      async () => {
        try {
          setIsLoading(
            true,
          )

          setError(
            null,
          )

          const result =
            await sitesApi
              .getSites(
                true,
              )

          setSites(
            result,
          )
        } catch (
          loadError
        ) {
          setError(
            getErrorMessage(
              loadError,
            ),
          )
        } finally {
          setIsLoading(
            false,
          )
        }
      },
      [],
    )

  useEffect(
    () => {
      void loadSites()

      document.title =
        'Localidades | TitanMDM'
    },
    [
      loadSites,
    ],
  )

  // ==========================================================
  // KPIs
  // ==========================================================

  const activeSites =
    useMemo(
      () =>
        sites.filter(
          site =>
            site.isActive,
        ).length,
      [
        sites,
      ],
    )

  // ==========================================================
  // OPEN SITE
  // ==========================================================

  const openSite =
    async (
      site: Site,
    ) => {
      /*
       * MUY IMPORTANTE:
       *
       * limpiamos inmediatamente todo el estado asociado
       * al Site anteriormente abierto.
       *
       * Así jamás mostramos locations del Site A
       * mientras estamos viendo Site B.
       */

      setSelectedSite(
        site,
      )

      setLocations(
        [],
      )

      setSummary(
        null,
      )

      setError(
        null,
      )

      setDrawerLoading(
        true,
      )

      try {
        // ====================================================
        // LOCATIONS
        // ====================================================

        const siteLocations =
          await sitesApi
            .getLocations(
              site.id,
            )

        /*
         * Esta respuesta debe contener ÚNICAMENTE
         * locations cuyo SiteId == site.id.
         */

        setLocations(
          siteLocations,
        )

        // ====================================================
        // SUMMARY
        // ====================================================

        if (
          site.isActive
        ) {
          try {
            const siteSummary =
              await sitesApi
                .getSummary(
                  site.id,
                )

            setSummary(
              siteSummary,
            )
          } catch (
            summaryError
          ) {
            /*
             * Summary no debe bloquear el drawer.
             *
             * Sobre todo durante pruebas de RBAC/scopes.
             */
            console.warn(
              'No fue posible cargar el resumen de la localidad.',
              summaryError,
            )

            setSummary(
              null,
            )
          }
        }
      } catch (
        locationsError
      ) {
        /*
         * Si falla la carga de locations:
         * SIEMPRE conservamos el arreglo vacío.
         */

        setLocations(
          [],
        )

        setSummary(
          null,
        )

        setError(
          getErrorMessage(
            locationsError,
          ),
        )
      } finally {
        setDrawerLoading(
          false,
        )
      }
    }

  // ==========================================================
  // CLOSE SITE
  // ==========================================================

  const closeSite =
    () => {
      setSelectedSite(
        null,
      )

      setLocations(
        [],
      )

      setSummary(
        null,
      )

      setError(
        null,
      )

      setDrawerLoading(
        false,
      )
    }

  // ==========================================================
  // CREATE SITE
  // ==========================================================

  const createSite =
    async () => {
      if (
        !form.code
          .trim()
        ||
        !form.name
          .trim()
      ) {
        return
      }

      try {
        setError(
          null,
        )

        await sitesApi
          .createSite({
            ...form,

            code:
              form.code
                .trim(),

            name:
              form.name
                .trim(),
          })

        setForm({
          ...emptySite,
        })

        setShowCreate(
          false,
        )

        await loadSites()
      } catch (
        createError
      ) {
        setError(
          getErrorMessage(
            createError,
          ),
        )
      }
    }

  // ==========================================================
  // SITE STATUS
  // ==========================================================

  const toggleSite =
    async (
      site: Site,
    ) => {
      try {
        setError(
          null,
        )

        if (
          site.isActive
        ) {
          await sitesApi
            .deactivateSite(
              site.id,
            )
        } else {
          await sitesApi
            .activateSite(
              site.id,
            )
        }

        closeSite()

        await loadSites()
      } catch (
        toggleError
      ) {
        setError(
          getErrorMessage(
            toggleError,
          ),
        )
      }
    }

  // ==========================================================
  // CREATE LOCATION
  // ==========================================================

  const createLocation =
    async () => {
      if (
        !selectedSite
      ) {
        return
      }

      /*
       * Guardamos el ID antes del await.
       *
       * Así garantizamos que toda la operación
       * pertenece a la Site actualmente seleccionada.
       */

      const siteId =
        selectedSite.id

      const name =
        window.prompt(
          `Nueva ubicación dentro de ${selectedSite.name}`,
        )

      if (
        !name?.trim()
      ) {
        return
      }

      try {
        setError(
          null,
        )

        await sitesApi
          .createLocation(
            siteId,
            {
              name:
                name.trim(),

              description:
                null,
            },
          )

        /*
         * Recargamos EXCLUSIVAMENTE:
         *
         * /sites/{siteId}/locations
         */

        const refreshedLocations =
          await sitesApi
            .getLocations(
              siteId,
            )

        /*
         * Protección extra ante una navegación rápida:
         * solamente aplicamos la respuesta si el drawer
         * todavía corresponde a esa misma Site.
         */

        setLocations(
          current => {
            void current

            return refreshedLocations
          },
        )
      } catch (
        locationError
      ) {
        setError(
          getErrorMessage(
            locationError,
          ),
        )
      }
    }

  // ==========================================================
  // LOCATION STATUS
  // ==========================================================

  const toggleLocation =
    async (
      location:
        SiteLocation,
    ) => {
      if (
        !selectedSite
      ) {
        return
      }

      const siteId =
        selectedSite.id

      /*
       * Protección:
       * la location recibida tiene que pertenecer
       * al Site actualmente abierto.
       */

      if (
        location.siteId !==
        siteId
      ) {
        setError(
          'La ubicación no pertenece a la localidad seleccionada.',
        )

        return
      }

      try {
        setError(
          null,
        )

        if (
          location.isActive
        ) {
          await sitesApi
            .deactivateLocation(
              siteId,
              location.id,
            )
        } else {
          await sitesApi
            .activateLocation(
              siteId,
              location.id,
            )
        }

        const refreshedLocations =
          await sitesApi
            .getLocations(
              siteId,
            )

        setLocations(
          refreshedLocations,
        )
      } catch (
        locationError
      ) {
        setError(
          getErrorMessage(
            locationError,
          ),
        )
      }
    }

  // ==========================================================
  // UI
  // ==========================================================

  return (
    <main className="sites-page">
      {/* ======================================================
          HEADER
      ====================================================== */}

      <header className="sites-hero">
        <div>
          <span className="sites-eyebrow">
            MULTI-SITE
          </span>

          <h1>
            Localidades
          </h1>

          <p>
            Administración central de plantas,
            naves, edificios y ubicaciones
            operativas de TitanMDM.
          </p>
        </div>

        <div className="sites-actions">
          <button
            type="button"
            className="sites-button"
            onClick={() =>
              void loadSites()
            }
          >
            <RefreshCw
              size={16}
            />

            Actualizar
          </button>

          {canManage && (
            <button
              type="button"
              className="sites-button sites-button--primary"
              onClick={() =>
                setShowCreate(
                  true,
                )
              }
            >
              <Plus
                size={16}
              />

              Nueva localidad
            </button>
          )}
        </div>
      </header>

      {/* ======================================================
          KPIs
      ====================================================== */}

      <section className="sites-kpis">
        <article>
          <Building2 />

          <div>
            <span>
              Localidades
            </span>

            <strong>
              {sites.length}
            </strong>
          </div>
        </article>

        <article>
          <ShieldCheck />

          <div>
            <span>
              Activas
            </span>

            <strong>
              {activeSites}
            </strong>
          </div>
        </article>

        <article>
          <MapPin />

          <div>
            <span>
              Inactivas
            </span>

            <strong>
              {sites.length -
                activeSites}
            </strong>
          </div>
        </article>
      </section>

      {/* ======================================================
          ERROR
      ====================================================== */}

      {error && (
        <div className="sites-error">
          {error}
        </div>
      )}

      {/* ======================================================
          SITE GRID
      ====================================================== */}

      <section className="sites-grid">
        {isLoading ? (
          <div className="sites-empty">
            Cargando localidades...
          </div>
        ) : sites.length ===
          0 ? (
          <div className="sites-empty">
            No existen localidades.
          </div>
        ) : (
          sites.map(
            site => (
              <button
                type="button"
                key={
                  site.id
                }
                className="site-card"
                onClick={() =>
                  void openSite(
                    site,
                  )
                }
              >
                <div className="site-card__icon">
                  <Building2
                    size={22}
                  />
                </div>

                <div className="site-card__body">
                  <div className="site-card__title">
                    <strong>
                      {site.name}
                    </strong>

                    <span
                      className={
                        site.isActive
                          ? 'site-state site-state--active'
                          : 'site-state site-state--inactive'
                      }
                    >
                      {site.isActive
                        ? 'Activa'
                        : 'Inactiva'}
                    </span>
                  </div>

                  <span className="site-code">
                    {site.code}
                  </span>

                  <p>
                    {[
                      site.city,
                      site.province,
                      site.country,
                    ]
                      .filter(
                        Boolean,
                      )
                      .join(
                        ', ',
                      )
                    ||
                    'Ubicación no especificada'}
                  </p>
                </div>
              </button>
            ),
          )
        )}
      </section>

      {/* ======================================================
          SITE DRAWER
      ====================================================== */}

      {selectedSite && (
        <div className="sites-drawer-backdrop">
          <aside className="sites-drawer">
            <header>
              <div>
                <span>
                  {selectedSite.code}
                </span>

                <h2>
                  {selectedSite.name}
                </h2>
              </div>

              <button
                type="button"
                onClick={
                  closeSite
                }
                aria-label="Cerrar"
              >
                <X
                  size={19}
                />
              </button>
            </header>

            <div className="sites-drawer__body">
              {/* ==============================================
                  SITE DATA
              ============================================== */}

              <section className="sites-details">
                <div>
                  <span>
                    Ciudad
                  </span>

                  <strong>
                    {selectedSite.city ??
                      'N/D'}
                  </strong>
                </div>

                <div>
                  <span>
                    Provincia
                  </span>

                  <strong>
                    {selectedSite.province ??
                      'N/D'}
                  </strong>
                </div>

                <div>
                  <span>
                    Zona horaria
                  </span>

                  <strong>
                    {selectedSite.timeZoneId ??
                      'N/D'}
                  </strong>
                </div>

                <div>
                  <span>
                    Estado
                  </span>

                  <strong>
                    {selectedSite.isActive
                      ? 'Activa'
                      : 'Inactiva'}
                  </strong>
                </div>
              </section>

              {/* ==============================================
                  LOADING
              ============================================== */}

              {drawerLoading && (
                <p className="sites-muted">
                  Cargando información de la localidad...
                </p>
              )}

              {/* ==============================================
                  SUMMARY
              ============================================== */}

              {summary && (
                <section className="site-summary">
                  <h3>
                    Operación
                  </h3>

                  <div>
                    <article>
                      <span>
                        Equipos
                      </span>

                      <strong>
                        {summary.devices.total}
                      </strong>
                    </article>

                    <article>
                      <span>
                        Online
                      </span>

                      <strong>
                        {summary.devices.online}
                      </strong>
                    </article>

                    <article>
                      <span>
                        Tickets
                      </span>

                      <strong>
                        {summary.helpdesk.total}
                      </strong>
                    </article>

                    <article>
                      <span>
                        SLA vencido
                      </span>

                      <strong>
                        {summary.helpdesk.slaBreached}
                      </strong>
                    </article>

                    <article>
                      <span>
                        Seguridad
                      </span>

                      <strong>
                        {summary.security.averageComplianceScore}%
                      </strong>
                    </article>
                  </div>
                </section>
              )}

              {/* ==============================================
                  LOCATIONS
              ============================================== */}

              <section className="site-locations">
                <header>
                  <div>
                    <h3>
                      Ubicaciones
                    </h3>

                    <p className="sites-muted">
                      Áreas internas exclusivas de{' '}
                      <strong>
                        {selectedSite.name}
                      </strong>
                    </p>
                  </div>

                  {canManage && (
                    <button
                      type="button"
                      onClick={() =>
                        void createLocation()
                      }
                    >
                      <Plus
                        size={15}
                      />

                      Añadir
                    </button>
                  )}
                </header>

                {!drawerLoading &&
                locations.length ===
                  0 ? (
                  <p className="sites-muted">
                    Esta localidad todavía no tiene
                    sububicaciones configuradas.
                  </p>
                ) : (
                  locations.map(
                    location => (
                      <div
                        className="site-location-row"
                        key={
                          location.id
                        }
                      >
                        <div>
                          <strong>
                            {location.name}
                          </strong>

                          <span>
                            {location.description ??
                              'Sin descripción'}
                          </span>
                        </div>

                        <span
                          className={
                            location.isActive
                              ? 'site-state site-state--active'
                              : 'site-state site-state--inactive'
                          }
                        >
                          {location.isActive
                            ? 'Activa'
                            : 'Inactiva'}
                        </span>

                        {canManage && (
                          <button
                            type="button"
                            onClick={() =>
                              void toggleLocation(
                                location,
                              )
                            }
                          >
                            {location.isActive
                              ? 'Desactivar'
                              : 'Activar'}
                          </button>
                        )}
                      </div>
                    ),
                  )
                )}
              </section>

              {/* ==============================================
                  SITE STATUS
              ============================================== */}

              {canManage && (
                <div className="site-danger-zone">
                  <button
                    type="button"
                    onClick={() =>
                      void toggleSite(
                        selectedSite,
                      )
                    }
                  >
                    {selectedSite.isActive
                      ? 'Desactivar localidad'
                      : 'Activar localidad'}
                  </button>
                </div>
              )}
            </div>
          </aside>
        </div>
      )}

      {/* ======================================================
          CREATE SITE MODAL
      ====================================================== */}

      {showCreate && (
        <div className="sites-modal-backdrop">
          <div className="sites-modal">
            <header>
              <div>
                <span>
                  MULTI-SITE
                </span>

                <h2>
                  Nueva localidad
                </h2>
              </div>

              <button
                type="button"
                onClick={() =>
                  setShowCreate(
                    false,
                  )
                }
                aria-label="Cerrar"
              >
                <X
                  size={18}
                />
              </button>
            </header>

            <div className="sites-form">
              <label>
                Código

                <input
                  value={
                    form.code
                  }
                  onChange={
                    event =>
                      setForm(
                        current => ({
                          ...current,

                          code:
                            event
                              .target
                              .value,
                        }),
                      )
                  }
                />
              </label>

              <label>
                Nombre

                <input
                  value={
                    form.name
                  }
                  onChange={
                    event =>
                      setForm(
                        current => ({
                          ...current,

                          name:
                            event
                              .target
                              .value,
                        }),
                      )
                  }
                />
              </label>

              <label className="sites-field-full">
                Dirección

                <input
                  value={
                    form.address ??
                    ''
                  }
                  onChange={
                    event =>
                      setForm(
                        current => ({
                          ...current,

                          address:
                            event
                              .target
                              .value,
                        }),
                      )
                  }
                />
              </label>

              <label>
                Ciudad

                <input
                  value={
                    form.city ??
                    ''
                  }
                  onChange={
                    event =>
                      setForm(
                        current => ({
                          ...current,

                          city:
                            event
                              .target
                              .value,
                        }),
                      )
                  }
                />
              </label>

              <label>
                Provincia

                <input
                  value={
                    form.province ??
                    ''
                  }
                  onChange={
                    event =>
                      setForm(
                        current => ({
                          ...current,

                          province:
                            event
                              .target
                              .value,
                        }),
                      )
                  }
                />
              </label>

              <label>
                País

                <input
                  value={
                    form.country ??
                    ''
                  }
                  onChange={
                    event =>
                      setForm(
                        current => ({
                          ...current,

                          country:
                            event
                              .target
                              .value,
                        }),
                      )
                  }
                />
              </label>

              <label>
                Zona horaria

                <input
                  value={
                    form.timeZoneId ??
                    ''
                  }
                  onChange={
                    event =>
                      setForm(
                        current => ({
                          ...current,

                          timeZoneId:
                            event
                              .target
                              .value,
                        }),
                      )
                  }
                />
              </label>

              <label className="sites-field-full">
                Descripción

                <textarea
                  value={
                    form.description ??
                    ''
                  }
                  onChange={
                    event =>
                      setForm(
                        current => ({
                          ...current,

                          description:
                            event
                              .target
                              .value,
                        }),
                      )
                  }
                />
              </label>
            </div>

            <footer>
              <button
                type="button"
                className="sites-button"
                onClick={() =>
                  setShowCreate(
                    false,
                  )
                }
              >
                Cancelar
              </button>

              <button
                type="button"
                className="sites-button sites-button--primary"
                disabled={
                  !form.code
                    .trim()
                  ||
                  !form.name
                    .trim()
                }
                onClick={() =>
                  void createSite()
                }
              >
                Crear localidad
              </button>
            </footer>
          </div>
        </div>
      )}
    </main>
  )
}