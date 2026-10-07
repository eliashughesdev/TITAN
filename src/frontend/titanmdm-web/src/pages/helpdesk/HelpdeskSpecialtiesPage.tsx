import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  Link,
  useSearchParams,
} from 'react-router-dom'

import axios
  from 'axios'

import {
  CalendarClock,
  CheckCircle2,
  Clock3,
  FolderKanban,
  MapPin,
  Plus,
  RefreshCw,
  Save,
  Tags,
  Trash2,
  UserPlus,
  Users,
} from 'lucide-react'

import apiClient
  from '../../api/apiClient'

import './HelpdeskSpecialtiesPage.css'

type PlanningTab =
  | 'groups'
  | 'categories'
  | 'schedules'

type Slot = {
  day: number
  start: string
  end: string
}

type Technician = {
  userId: string

  isAvailable: boolean

  acceptsAutomaticAssignments:
    boolean

  maxOpenTickets: number

  priority: number

  timeZoneId: string

  slots: Slot[]

  configured?: boolean

  onDuty?: boolean
}

type Coverage = {
  id?: string

  siteId: string

  siteLocationId:
    string | null

  category:
    string | null

  priority: number

  isActive?: boolean
}

type Group = {
  id: string

  name: string

  description:
    string | null

  isActive: boolean

  tasks: string[]

  coverages:
    Coverage[]

  technicians:
    Technician[]
}

type Site = {
  id: string
  code: string
  name: string
  city: string | null
  province: string | null
  isActive: boolean
}

type SiteLocation = {
  id: string
  siteId: string
  name: string
  description: string | null
  isActive: boolean
}

type Staff = {
  id: string
  name: string
  email: string

  eligible: boolean

  siteId:
    string | null

  siteName:
    string | null

  siteLocationId:
    string | null

  siteLocationName:
    string | null
}

type Catalog = {
  groups: Group[]
  sites: Site[]
  locations: SiteLocation[]
  users: Staff[]
}

const DAYS = [
  'Domingo',
  'Lunes',
  'Martes',
  'Miércoles',
  'Jueves',
  'Viernes',
  'Sábado',
]

const DEFAULT_TIME_ZONE =
  'America/Santo_Domingo'

const emptyCatalog:
  Catalog = {
    groups: [],
    sites: [],
    locations: [],
    users: [],
  }

function resolveTab(
  value:
    string | null,
): PlanningTab {
  if (
    value ===
      'categories'
  ) {
    return 'categories'
  }

  if (
    value ===
      'schedules'
  ) {
    return 'schedules'
  }

  return 'groups'
}

function defaultSchedule():
  Slot[] {
  return [
    1,
    2,
    3,
    4,
    5,
  ].map(
    day => ({
      day,

      start:
        '08:00',

      end:
        '17:00',
    }),
  )
}

function cloneGroup(
  group: Group,
): Group {
  return {
    ...group,

    tasks: [
      ...group.tasks,
    ],

    coverages:
      group.coverages.map(
        item => ({
          ...item,
        }),
      ),

    technicians:
      group.technicians.map(
        technician => ({
          ...technician,

          slots:
            technician.slots.map(
              slot => ({
                ...slot,
              }),
            ),
        }),
      ),
  }
}

function errorMessage(
  exception: unknown,
) {
  if (
    axios.isAxiosError<{
      message?: string
      detail?: string
    }>(
      exception,
    )
  ) {
    return (
      exception.response
        ?.data
        ?.message
      ??
      exception.response
        ?.data
        ?.detail
      ??
      `No se pudo completar la operación (${exception.response?.status ?? 'sin conexión'}).`
    )
  }

  return exception instanceof Error
    ? exception.message
    : 'No se pudo completar la operación.'
}

export function HelpdeskSpecialtiesPage() {
  const [
    searchParams,
    setSearchParams,
  ] =
    useSearchParams()

  const requestedTab =
    resolveTab(
      searchParams.get(
        'tab',
      ),
    )

  const [
    tab,
    setTab,
  ] =
    useState<PlanningTab>(
      requestedTab,
    )

  const [
    catalog,
    setCatalog,
  ] =
    useState<Catalog>(
      emptyCatalog,
    )

  const [
    selected,
    setSelected,
  ] =
    useState<Group | null>(
      null,
    )

  const [
    dirty,
    setDirty,
  ] =
    useState(
      false,
    )

  const [
    loading,
    setLoading,
  ] =
    useState(
      true,
    )

  const [
    saving,
    setSaving,
  ] =
    useState(
      false,
    )

  const [
    success,
    setSuccess,
  ] =
    useState(
      '',
    )

  const [
    error,
    setError,
  ] =
    useState(
      '',
    )

  const [
    task,
    setTask,
  ] =
    useState(
      '',
    )

  const [
    staffId,
    setStaffId,
  ] =
    useState(
      '',
    )

  const [
    search,
    setSearch,
  ] =
    useState(
      '',
    )

  const [
    name,
    setName,
  ] =
    useState(
      '',
    )

  const [
    description,
    setDescription,
  ] =
    useState(
      '',
    )

  const [
    showCreate,
    setShowCreate,
  ] =
    useState(
      false,
    )

  const [
    coverageSiteId,
    setCoverageSiteId,
  ] =
    useState(
      '',
    )

  const [
    coverageLocationId,
    setCoverageLocationId,
  ] =
    useState(
      '',
    )

  const [
    coverageCategory,
    setCoverageCategory,
  ] =
    useState(
      '',
    )

  const [
    coveragePriority,
    setCoveragePriority,
  ] =
    useState(
      100,
    )

  const busy =
    loading
    ||
    saving

  // ============================================================
  // TAB <-> URL
  // ============================================================

  useEffect(
    () => {
      if (
        requestedTab !==
        tab
      ) {
        setTab(
          requestedTab,
        )
      }
    },
    [
      requestedTab,
      tab,
    ],
  )

  function changeTab(
    next:
      PlanningTab,
  ) {
    setTab(
      next,
    )

    setError(
      '',
    )

    setSuccess(
      '',
    )

    const params =
      new URLSearchParams(
        searchParams,
      )

    if (
      next ===
      'groups'
    ) {
      params.delete(
        'tab',
      )
    }
    else {
      params.set(
        'tab',
        next,
      )
    }

    params.set(
      'workspace',
      'helpdesk',
    )

    setSearchParams(
      params,
    )
  }

  // ============================================================
  // LOAD
  // ============================================================

  const load =
    useCallback(
      async (
        selectedId?: string,
      ) => {
        setLoading(
          true,
        )

        try {
          const {
            data,
          } =
            await apiClient
              .get<Catalog>(
                '/helpdesk/group-planning',
              )

          const normalized:
            Catalog = {
              ...data,

              groups:
                data.groups
                  .filter(
                    group =>
                      group.isActive,
                  ),
            }

          setCatalog(
            normalized,
          )

          const group =
            normalized.groups.find(
              item =>
                item.id ===
                  selectedId,
            )
            ??
            normalized.groups[0]
            ??
            null

          setSelected(
            group
              ? cloneGroup(
                  group,
                )
              : null,
          )

          setDirty(
            false,
          )
        }
        finally {
          setLoading(
            false,
          )
        }
      },
      [],
    )

  useEffect(
    () => {
      void load()
        .catch(
          exception =>
            setError(
              errorMessage(
                exception,
              ),
            ),
        )
    },
    [
      load,
    ],
  )

  // ============================================================
  // DERIVED DATA
  // ============================================================

  const locationsForCoverage =
    useMemo(
      () =>
        catalog.locations
          .filter(
            location =>
              location.siteId ===
                coverageSiteId
              &&
              location.isActive,
          ),
      [
        catalog.locations,
        coverageSiteId,
      ],
    )

  const eligibleStaff =
    useMemo(
      () =>
        catalog.users
          .filter(
            user =>
              user.eligible,
          )
          .filter(
            user =>
              !selected
                ?.technicians
                .some(
                  technician =>
                    technician.userId ===
                      user.id,
                ),
          ),
      [
        catalog.users,
        selected,
      ],
    )

  const filteredGroups =
    useMemo(
      () => {
        const term =
          search
            .trim()
            .toLowerCase()

        if (!term) {
          return catalog.groups
        }

        return catalog.groups
          .filter(
            group =>
              group.name
                .toLowerCase()
                .includes(
                  term,
                )
              ||
              (
                group.description
                ??
                ''
              )
                .toLowerCase()
                .includes(
                  term,
                )
              ||
              group.tasks
                .some(
                  item =>
                    item
                      .toLowerCase()
                      .includes(
                        term,
                      ),
                ),
          )
      },
      [
        catalog.groups,
        search,
      ],
    )

  const totalCategories =
    useMemo(
      () =>
        new Set(
          catalog.groups
            .flatMap(
              group =>
                group.tasks,
            )
            .map(
              value =>
                value
                  .toLowerCase(),
            ),
        )
          .size,
      [
        catalog.groups,
      ],
    )

  const totalTechnicians =
    useMemo(
      () =>
        new Set(
          catalog.groups
            .flatMap(
              group =>
                group.technicians,
            )
            .map(
              technician =>
                technician.userId,
            ),
        )
          .size,
      [
        catalog.groups,
      ],
    )

  const totalAutoAssignment =
    useMemo(
      () =>
        catalog.groups
          .flatMap(
            group =>
              group.technicians,
          )
          .filter(
            technician =>
              technician
                .acceptsAutomaticAssignments,
          )
          .length,
      [
        catalog.groups,
      ],
    )

  // ============================================================
  // UPDATE HELPERS
  // ============================================================

  function update(
    patch:
      Partial<Group>,
  ) {
    setSelected(
      current =>
        current
          ? {
              ...current,
              ...patch,
            }
          : null,
    )

    setDirty(
      true,
    )

    setSuccess(
      '',
    )

    setError(
      '',
    )
  }

  function updateTech(
    userId: string,

    patch:
      Partial<Technician>,
  ) {
    if (!selected) {
      return
    }

    update({
      technicians:
        selected.technicians.map(
          technician =>
            technician.userId ===
              userId
              ? {
                  ...technician,
                  ...patch,
                }
              : technician,
        ),
    })
  }

  // ============================================================
  // GROUP SELECTION
  // ============================================================

  function choose(
    group: Group,
  ) {
    if (
      dirty
      &&
      !window.confirm(
        '¿Descartar los cambios sin guardar?',
      )
    ) {
      return
    }

    setSelected(
      cloneGroup(
        group,
      ),
    )

    setDirty(
      false,
    )

    setError(
      '',
    )

    setSuccess(
      '',
    )

    setTask(
      '',
    )

    setStaffId(
      '',
    )

    setCoverageSiteId(
      '',
    )

    setCoverageLocationId(
      '',
    )

    setCoverageCategory(
      '',
    )
  }

  // ============================================================
  // CATEGORIES / TASKS
  // ============================================================

  function addTask() {
    if (!selected) {
      return
    }

    const additions =
      task
        .split(
          /[,;\n]/,
        )
        .map(
          value =>
            value
              .trim()
              .toLowerCase(),
        )
        .filter(
          Boolean,
        )

    const tasks =
      [
        ...new Set(
          [
            ...selected.tasks,
            ...additions,
          ],
        ),
      ]

    if (
      tasks.length >
        50
    ) {
      setError(
        'Cada grupo admite hasta 50 categorías.',
      )

      return
    }

    if (
      tasks.some(
        value =>
          value.length >
            100
          ||
          value.includes(
            '|',
          ),
      )
    ) {
      setError(
        'Cada categoría admite hasta 100 caracteres y no puede contener "|".',
      )

      return
    }

    update({
      tasks,
    })

    setTask(
      '',
    )
  }

  function removeTask(
    value:
      string,
  ) {
    if (!selected) {
      return
    }

    const inCoverage =
      selected.coverages.some(
        coverage =>
          coverage.category ===
            value,
      )

    if (
      inCoverage
      &&
      !window.confirm(
        'Esta categoría está utilizada por una cobertura. ¿Quieres eliminarla igualmente del grupo?',
      )
    ) {
      return
    }

    update({
      tasks:
        selected.tasks.filter(
          item =>
            item !==
              value,
        ),
    })
  }

  // ============================================================
  // COVERAGE
  // ============================================================

  function addCoverage() {
    if (
      !selected
      ||
      !coverageSiteId
    ) {
      setError(
        'Selecciona una localidad.',
      )

      return
    }

    const category =
      coverageCategory
        .trim()
        .toLowerCase()
      ||
      null

    if (
      category
      &&
      !selected.tasks
        .includes(
          category,
        )
    ) {
      setError(
        'La categoría seleccionada debe existir dentro del grupo.',
      )

      return
    }

    const duplicate =
      selected.coverages
        .some(
          coverage =>
            coverage.siteId ===
              coverageSiteId
            &&
            (
              coverage.siteLocationId
              ??
              null
            ) ===
              (
                coverageLocationId
                  ? coverageLocationId
                  : null
              )
            &&
            (
              coverage.category
              ??
              null
            ) ===
              category,
        )

    if (duplicate) {
      setError(
        'Esa cobertura ya está agregada al grupo.',
      )

      return
    }

    update({
      coverages: [
        ...selected.coverages,

        {
          siteId:
            coverageSiteId,

          siteLocationId:
            coverageLocationId
              ? coverageLocationId
              : null,

          category,

          priority:
            coveragePriority,
        },
      ],
    })

    setCoverageLocationId(
      '',
    )

    setCoverageCategory(
      '',
    )

    setCoveragePriority(
      100,
    )
  }

  function removeCoverage(
    index:
      number,
  ) {
    if (!selected) {
      return
    }

    update({
      coverages:
        selected.coverages
          .filter(
            (
              _,
              position,
            ) =>
              position !==
              index,
          ),
    })
  }

  // ============================================================
  // TECHNICIANS
  // ============================================================

  function addTech() {
    if (
      !selected
      ||
      !staffId
    ) {
      return
    }

    if (
      selected.technicians
        .some(
          technician =>
            technician.userId ===
              staffId,
        )
    ) {
      setError(
        'Ese técnico ya pertenece al grupo.',
      )

      return
    }

    const maxPriority =
      Math.max(
        0,
        ...selected.technicians
          .map(
            technician =>
              technician.priority,
          ),
      )

    update({
      technicians: [
        ...selected.technicians,

        {
          userId:
            staffId,

          isAvailable:
            true,

          acceptsAutomaticAssignments:
            true,

          maxOpenTickets:
            20,

          priority:
            Math.min(
              100,
              maxPriority +
                1,
            ),

          timeZoneId:
            DEFAULT_TIME_ZONE,

          slots:
            defaultSchedule(),
        },
      ],
    })

    setStaffId(
      '',
    )
  }

  function removeTech(
    userId:
      string,
  ) {
    if (!selected) {
      return
    }

    const user =
      catalog.users
        .find(
          item =>
            item.id ===
              userId,
        )

    if (
      !window.confirm(
        `¿Quitar a ${user?.name ?? 'este técnico'} del grupo?`,
      )
    ) {
      return
    }

    update({
      technicians:
        selected.technicians
          .filter(
            technician =>
              technician.userId !==
                userId,
          ),
    })
  }

  // ============================================================
  // SCHEDULES
  // ============================================================

  function toggleDay(
    technician:
      Technician,

    day:
      number,
  ) {
    const existing =
      technician.slots
        .find(
          slot =>
            slot.day ===
              day,
        )

    if (existing) {
      updateTech(
        technician.userId,
        {
          slots:
            technician.slots
              .filter(
                slot =>
                  slot.day !==
                    day,
              ),
        },
      )

      return
    }

    updateTech(
      technician.userId,
      {
        slots: [
          ...technician.slots,

          {
            day,

            start:
              '08:00',

            end:
              '17:00',
          },
        ]
          .sort(
            (
              a,
              b,
            ) =>
              a.day -
              b.day,
          ),
      },
    )
  }

  function updateSlot(
    technician:
      Technician,

    day:
      number,

    patch:
      Partial<Slot>,
  ) {
    updateTech(
      technician.userId,
      {
        slots:
          technician.slots
            .map(
              slot =>
                slot.day ===
                  day
                  ? {
                      ...slot,
                      ...patch,
                    }
                  : slot,
            ),
      },
    )
  }

  // ============================================================
  // SAVE
  // ============================================================

  async function save() {
    if (
      !selected
      ||
      saving
    ) {
      return
    }

    if (
      !selected.tasks.length
    ) {
      setError(
        'Agrega al menos una categoría al grupo.',
      )

      return
    }

    if (
      !selected.coverages.length
    ) {
      setError(
        'Agrega al menos una localidad de cobertura.',
      )

      return
    }

    if (
      selected.technicians.some(
        technician =>
          technician
            .acceptsAutomaticAssignments
          &&
          !technician.slots.length,
      )
    ) {
      setError(
        'Cada técnico con asignación automática necesita al menos un día de horario.',
      )

      return
    }

    const invalidSlot =
      selected.technicians
        .flatMap(
          technician =>
            technician.slots,
        )
        .some(
          slot =>
            !slot.start
            ||
            !slot.end
            ||
            slot.start >=
              slot.end,
        )

    if (invalidSlot) {
      setError(
        'Revisa los horarios: la hora de inicio debe ser anterior a la hora final.',
      )

      return
    }

    setSaving(
      true,
    )

    setError(
      '',
    )

    setSuccess(
      '',
    )

    try {
      await apiClient.put(
        `/helpdesk/group-planning/groups/${selected.id}`,
        {
          tasks:
            selected.tasks,

          coverages:
            selected.coverages.map(
              coverage => ({
                siteId:
                  coverage.siteId,

                siteLocationId:
                  coverage.siteLocationId,

                category:
                  coverage.category,

                priority:
                  coverage.priority,
              }),
            ),

          technicians:
            selected.technicians.map(
              technician => ({
                userId:
                  technician.userId,

                isAvailable:
                  technician.isAvailable,

                acceptsAutomaticAssignments:
                  technician
                    .acceptsAutomaticAssignments,

                maxOpenTickets:
                  technician
                    .maxOpenTickets,

                priority:
                  technician.priority,

                timeZoneId:
                  technician.timeZoneId,

                slots:
                  technician.slots,
              }),
            ),
        },
      )

      setDirty(
        false,
      )

      setSuccess(
        'Grupo, categorías, cobertura, técnicos y turnos guardados correctamente.',
      )

      await load(
        selected.id,
      )
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  // ============================================================
  // CREATE GROUP
  // ============================================================

  async function create() {
    if (
      !name.trim()
      ||
      saving
    ) {
      setError(
        'Escribe el nombre del grupo.',
      )

      return
    }

    setSaving(
      true,
    )

    setError(
      '',
    )

    setSuccess(
      '',
    )

    try {
      const {
        data,
      } =
        await apiClient
          .post<{
            id: string
          }>(
            '/helpdesk/group-planning/groups',
            {
              name:
                name.trim(),

              description:
                description
                  .trim()
                ||
                null,
            },
          )

      setShowCreate(
        false,
      )

      setName(
        '',
      )

      setDescription(
        '',
      )

      await load(
        data.id,
      )

      setSuccess(
        'Grupo creado. Agrega categorías, cobertura, técnicos y turnos.',
      )
    }
    catch (
      exception
    ) {
      setError(
        errorMessage(
          exception,
        ),
      )
    }
    finally {
      setSaving(
        false,
      )
    }
  }

  // ============================================================
  // DISPLAY HELPERS
  // ============================================================

  function siteLabel(
    siteId:
      string,
  ) {
    return (
      catalog.sites
        .find(
          site =>
            site.id ===
              siteId,
        )
        ?.name
      ??
      'Localidad no disponible'
    )
  }

  function locationLabel(
    locationId:
      string | null,
  ) {
    if (!locationId) {
      return 'Toda la localidad'
    }

    return (
      catalog.locations
        .find(
          location =>
            location.id ===
              locationId,
        )
        ?.name
      ??
      'Sublocalidad no disponible'
    )
  }

  function userLabel(
    userId:
      string,
  ) {
    return (
      catalog.users
        .find(
          user =>
            user.id ===
              userId,
        )
        ?.name
      ??
      'Usuario no disponible'
    )
  }

  function userEmail(
    userId:
      string,
  ) {
    return (
      catalog.users
        .find(
          user =>
            user.id ===
              userId,
        )
        ?.email
      ??
      ''
    )
  }

  // ============================================================
  // PAGE TITLE
  // ============================================================

  const pageTitle =
    tab ===
      'categories'
      ? 'Categorías y especialidades'
      : tab ===
          'schedules'
        ? 'Turnos y capacidad'
        : 'Grupos de trabajo'

  const pageDescription =
    tab ===
      'categories'
      ? 'Define qué tipos de solicitudes atiende cada grupo TIC.'
      : tab ===
          'schedules'
        ? 'Configura técnicos, disponibilidad, capacidad y horarios de asignación.'
        : 'Administra los grupos TIC, su cobertura y estructura operativa.'

  return (
    <main
      className="hdgp"
    >
      {/* ========================================================
          HEADER
         ======================================================== */}

      <header
        className="hdgp-header"
      >
        <div>
          <span
            className="hdgp-eyebrow"
          >
            MESA DE AYUDA · ADMINISTRACIÓN
          </span>

          <h1>
            {pageTitle}
          </h1>

          <p>
            {pageDescription}
          </p>
        </div>

        <div
          className="hdgp-actions"
        >
          <Link
            to="/helpdesk/operations?workspace=helpdesk"
          >
            <MapPin
              size={15}
            />

            Localidades
          </Link>

          <button
            type="button"
            disabled={
              busy
            }
            onClick={
              () =>
                void load(
                  selected?.id,
                )
            }
          >
            <RefreshCw
              size={15}
            />

            Actualizar
          </button>

          <button
            type="button"
            className="hdgp-primary"
            disabled={
              busy
            }
            onClick={
              () =>
                setShowCreate(
                  value =>
                    !value,
                )
            }
          >
            <Plus
              size={15}
            />

            Crear grupo
          </button>
        </div>
      </header>

      {/* ========================================================
          STATUS
         ======================================================== */}

      {error && (
        <div
          className="hdgp-alert hdgp-error"
          role="alert"
        >
          {error}
        </div>
      )}

      {success && (
        <div
          className="hdgp-alert hdgp-success"
          role="status"
        >
          <CheckCircle2
            size={16}
          />

          {success}
        </div>
      )}

      {/* ========================================================
          METRICS
         ======================================================== */}

      <section
        className="hdgp-metrics"
      >
        <article>
          <FolderKanban
            size={18}
          />

          <span>
            Grupos activos
          </span>

          <strong>
            {
              catalog.groups.length
            }
          </strong>
        </article>

        <article>
          <Tags
            size={18}
          />

          <span>
            Categorías
          </span>

          <strong>
            {
              totalCategories
            }
          </strong>
        </article>

        <article>
          <Users
            size={18}
          />

          <span>
            Técnicos
          </span>

          <strong>
            {
              totalTechnicians
            }
          </strong>
        </article>

        <article>
          <CalendarClock
            size={18}
          />

          <span>
            Autoasignación
          </span>

          <strong>
            {
              totalAutoAssignment
            }
          </strong>
        </article>
      </section>

      {/* ========================================================
          TABS
         ======================================================== */}

      <nav
        className="hdgp-tabs"
        aria-label="Configuración de grupos"
      >
        <button
          type="button"
          aria-pressed={
            tab ===
            'groups'
          }
          onClick={
            () =>
              changeTab(
                'groups',
              )
          }
        >
          <FolderKanban
            size={16}
          />

          Grupos
        </button>

        <button
          type="button"
          aria-pressed={
            tab ===
            'categories'
          }
          onClick={
            () =>
              changeTab(
                'categories',
              )
          }
        >
          <Tags
            size={16}
          />

          Categorías
        </button>

        <button
          type="button"
          aria-pressed={
            tab ===
            'schedules'
          }
          onClick={
            () =>
              changeTab(
                'schedules',
              )
          }
        >
          <CalendarClock
            size={16}
          />

          Turnos y capacidad
        </button>
      </nav>

      {/* ========================================================
          CREATE GROUP
         ======================================================== */}

      {showCreate && (
        <section
          className="hdgp-panel"
        >
          <h2>
            Nuevo grupo
          </h2>

          <fieldset
            disabled={
              busy
            }
            className="hdgp-create"
          >
            <label>
              Nombre

              <input
                maxLength={120}
                value={
                  name
                }
                onChange={
                  event =>
                    setName(
                      event.target
                        .value,
                    )
                }
                placeholder="Ej.: Soporte TIC"
              />
            </label>

            <label>
              Descripción

              <input
                maxLength={500}
                value={
                  description
                }
                onChange={
                  event =>
                    setDescription(
                      event.target
                        .value,
                    )
                }
                placeholder="Responsabilidad principal del grupo"
              />
            </label>

            <button
              type="button"
              className="hdgp-primary"
              onClick={
                () =>
                  void create()
              }
            >
              Crear
            </button>
          </fieldset>
        </section>
      )}

      {/* ========================================================
          WORKSPACE
         ======================================================== */}

      <div
        className="hdgp-layout"
      >
        {/* GROUP SIDEBAR */}

        <aside
          className="hdgp-panel hdgp-sidebar"
        >
          <h2>
            Grupos de trabajo
          </h2>

          <input
            aria-label="Buscar grupo"
            placeholder="Buscar grupo…"
            value={
              search
            }
            onChange={
              event =>
                setSearch(
                  event.target
                    .value,
                )
            }
          />

          {!filteredGroups
              .length ? (
            <p>
              No hay grupos.
            </p>
          ) : (
            filteredGroups.map(
              group => (
                <button
                  key={
                    group.id
                  }
                  type="button"
                  disabled={
                    busy
                  }
                  className={
                    `hdgp-group ${
                      selected?.id ===
                      group.id
                        ? 'selected'
                        : ''
                    }`
                  }
                  onClick={
                    () =>
                      choose(
                        group,
                      )
                  }
                >
                  <strong>
                    {
                      group.name
                    }
                  </strong>

                  <span>
                    {
                      group.tasks.length
                    }
                    {' '}
                    categorías ·
                    {' '}
                    {
                      group.technicians.length
                    }
                    {' '}
                    técnicos
                  </span>
                </button>
              ),
            )
          )}
        </aside>

        {/* EDITOR */}

        <section
          className="hdgp-editor"
        >
          {loading && (
            <div
              className="hdgp-panel"
            >
              Cargando configuración…
            </div>
          )}

          {!loading &&
            !selected && (
            <div
              className="hdgp-panel"
            >
              Crea o selecciona un grupo.
            </div>
          )}

          {selected && (
            <fieldset
              disabled={
                busy
              }
            >
              {/* ================================================
                  GROUPS TAB
                 ================================================ */}

              {tab ===
                'groups' && (
                <>
                  <section
                    className="hdgp-panel"
                  >
                    <div
                      className="hdgp-section-heading"
                    >
                      <div>
                        <h2>
                          {
                            selected.name
                          }
                        </h2>

                        <p>
                          {
                            selected.description
                            ??
                            'Sin descripción.'
                          }
                        </p>
                      </div>

                      <span>
                        {
                          selected.isActive
                            ? 'Activo'
                            : 'Inactivo'
                        }
                      </span>
                    </div>
                  </section>

                  <section
                    className="hdgp-panel"
                  >
                    <h2>
                      Cobertura territorial
                    </h2>

                    <p>
                      Define dónde puede recibir
                      tickets este grupo.
                    </p>

                    <div
                      className="hdgp-grid"
                    >
                      <label>
                        Localidad

                        <select
                          value={
                            coverageSiteId
                          }
                          onChange={
                            event => {
                              setCoverageSiteId(
                                event.target
                                  .value,
                              )

                              setCoverageLocationId(
                                '',
                              )
                            }
                          }
                        >
                          <option value="">
                            Selecciona
                          </option>

                          {catalog.sites
                            .filter(
                              site =>
                                site.isActive,
                            )
                            .map(
                              site => (
                                <option
                                  key={
                                    site.id
                                  }
                                  value={
                                    site.id
                                  }
                                >
                                  {
                                    site.name
                                  }
                                </option>
                              ),
                            )}
                        </select>
                      </label>

                      <label>
                        Sublocalidad

                        <select
                          value={
                            coverageLocationId
                          }
                          disabled={
                            !coverageSiteId
                          }
                          onChange={
                            event =>
                              setCoverageLocationId(
                                event.target
                                  .value,
                              )
                          }
                        >
                          <option value="">
                            Toda la localidad
                          </option>

                          {locationsForCoverage.map(
                            location => (
                              <option
                                key={
                                  location.id
                                }
                                value={
                                  location.id
                                }
                              >
                                {
                                  location.name
                                }
                              </option>
                            ),
                          )}
                        </select>
                      </label>

                      <label>
                        Categoría

                        <select
                          value={
                            coverageCategory
                          }
                          onChange={
                            event =>
                              setCoverageCategory(
                                event.target
                                  .value,
                              )
                          }
                        >
                          <option value="">
                            Todas
                          </option>

                          {selected.tasks.map(
                            item => (
                              <option
                                key={
                                  item
                                }
                                value={
                                  item
                                }
                              >
                                {
                                  item
                                }
                              </option>
                            ),
                          )}
                        </select>
                      </label>

                      <label>
                        Prioridad routing

                        <input
                          type="number"
                          min={1}
                          max={1000}
                          value={
                            coveragePriority
                          }
                          onChange={
                            event =>
                              setCoveragePriority(
                                Number(
                                  event.target
                                    .value,
                                ),
                              )
                          }
                        />
                      </label>
                    </div>

                    <button
                      type="button"
                      className="hdgp-secondary"
                      onClick={
                        addCoverage
                      }
                    >
                      <Plus
                        size={15}
                      />

                      Agregar cobertura
                    </button>

                    <div
                      className="hdgp-table"
                    >
                      <table>
                        <thead>
                          <tr>
                            <th>
                              Localidad
                            </th>

                            <th>
                              Sublocalidad
                            </th>

                            <th>
                              Categoría
                            </th>

                            <th>
                              Prioridad
                            </th>

                            <th />
                          </tr>
                        </thead>

                        <tbody>
                          {!selected.coverages
                              .length ? (
                            <tr>
                              <td
                                colSpan={5}
                              >
                                Sin cobertura configurada.
                              </td>
                            </tr>
                          ) : (
                            selected.coverages.map(
                              (
                                coverage,
                                index,
                              ) => (
                                <tr
                                  key={
                                    `${coverage.siteId}-${coverage.siteLocationId}-${coverage.category}-${index}`
                                  }
                                >
                                  <td>
                                    {siteLabel(
                                      coverage.siteId,
                                    )}
                                  </td>

                                  <td>
                                    {locationLabel(
                                      coverage.siteLocationId,
                                    )}
                                  </td>

                                  <td>
                                    {
                                      coverage.category
                                      ??
                                      'Todas'
                                    }
                                  </td>

                                  <td>
                                    {
                                      coverage.priority
                                    }
                                  </td>

                                  <td>
                                    <button
                                      type="button"
                                      className="hdgp-icon-danger"
                                      onClick={
                                        () =>
                                          removeCoverage(
                                            index,
                                          )
                                      }
                                    >
                                      <Trash2
                                        size={15}
                                      />
                                    </button>
                                  </td>
                                </tr>
                              ),
                            )
                          )}
                        </tbody>
                      </table>
                    </div>
                  </section>
                </>
              )}

              {/* ================================================
                  CATEGORIES TAB
                 ================================================ */}

              {tab ===
                'categories' && (
                <section
                  className="hdgp-panel"
                >
                  <h2>
                    Categorías de
                    {
                      ' '
                    }
                    {
                      selected.name
                    }
                  </h2>

                  <p>
                    Estas categorías determinan
                    qué tipo de solicitudes puede
                    recibir este grupo y también
                    alimentan Plantillas y Routing.
                  </p>

                  <div
                    className="hdgp-task-add"
                  >
                    <textarea
                      rows={3}
                      value={
                        task
                      }
                      onChange={
                        event =>
                          setTask(
                            event.target
                              .value,
                          )
                      }
                      placeholder={
                        'Ej.: redes, accesos, hardware, software'
                      }
                    />

                    <button
                      type="button"
                      className="hdgp-primary"
                      onClick={
                        addTask
                      }
                    >
                      <Plus
                        size={15}
                      />

                      Agregar
                    </button>
                  </div>

                  <div
                    className="hdgp-task-list"
                  >
                    {!selected.tasks
                        .length ? (
                      <p>
                        No hay categorías configuradas.
                      </p>
                    ) : (
                      selected.tasks.map(
                        item => (
                          <article
                            key={
                              item
                            }
                          >
                            <Tags
                              size={16}
                            />

                            <strong>
                              {
                                item
                              }
                            </strong>

                            <button
                              type="button"
                              onClick={
                                () =>
                                  removeTask(
                                    item,
                                  )
                              }
                            >
                              <Trash2
                                size={14}
                              />
                            </button>
                          </article>
                        ),
                      )
                    )}
                  </div>
                </section>
              )}

              {/* ================================================
                  SCHEDULES TAB
                 ================================================ */}

              {tab ===
                'schedules' && (
                <>
                  <section
                    className="hdgp-panel"
                  >
                    <h2>
                      Técnicos del grupo
                    </h2>

                    <p>
                      Agrega técnicos habilitados
                      para recibir tickets.
                    </p>

                    <div
                      className="hdgp-tech-add"
                    >
                      <select
                        value={
                          staffId
                        }
                        onChange={
                          event =>
                            setStaffId(
                              event.target
                                .value,
                            )
                        }
                      >
                        <option value="">
                          Selecciona técnico
                        </option>

                        {eligibleStaff.map(
                          user => (
                            <option
                              key={
                                user.id
                              }
                              value={
                                user.id
                              }
                            >
                              {
                                user.name
                              }
                              {
                                user.email
                                  ? ` · ${user.email}`
                                  : ''
                              }
                            </option>
                          ),
                        )}
                      </select>

                      <button
                        type="button"
                        className="hdgp-primary"
                        onClick={
                          addTech
                        }
                      >
                        <UserPlus
                          size={15}
                        />

                        Agregar técnico
                      </button>
                    </div>
                  </section>

                  {!selected.technicians
                      .length ? (
                    <section
                      className="hdgp-panel"
                    >
                      No hay técnicos
                      configurados en este grupo.
                    </section>
                  ) : (
                    selected.technicians.map(
                      technician => (
                        <section
                          key={
                            technician.userId
                          }
                          className="hdgp-panel hdgp-technician"
                        >
                          <header
                            className="hdgp-tech-header"
                          >
                            <div>
                              <strong>
                                {userLabel(
                                  technician.userId,
                                )}
                              </strong>

                              <span>
                                {userEmail(
                                  technician.userId,
                                )}
                              </span>
                            </div>

                            <button
                              type="button"
                              className="hdgp-icon-danger"
                              onClick={
                                () =>
                                  removeTech(
                                    technician.userId,
                                  )
                              }
                            >
                              <Trash2
                                size={15}
                              />
                            </button>
                          </header>

                          <div
                            className="hdgp-grid"
                          >
                            <label>
                              Disponible

                              <select
                                value={
                                  technician.isAvailable
                                    ? 'yes'
                                    : 'no'
                                }
                                onChange={
                                  event =>
                                    updateTech(
                                      technician.userId,
                                      {
                                        isAvailable:
                                          event.target
                                            .value ===
                                          'yes',
                                      },
                                    )
                                }
                              >
                                <option value="yes">
                                  Sí
                                </option>

                                <option value="no">
                                  No
                                </option>
                              </select>
                            </label>

                            <label>
                              Autoasignación

                              <select
                                value={
                                  technician
                                    .acceptsAutomaticAssignments
                                    ? 'yes'
                                    : 'no'
                                }
                                onChange={
                                  event =>
                                    updateTech(
                                      technician.userId,
                                      {
                                        acceptsAutomaticAssignments:
                                          event.target
                                            .value ===
                                          'yes',
                                      },
                                    )
                                }
                              >
                                <option value="yes">
                                  Activada
                                </option>

                                <option value="no">
                                  Desactivada
                                </option>
                              </select>
                            </label>

                            <label>
                              Capacidad máxima

                              <input
                                type="number"
                                min={1}
                                max={500}
                                value={
                                  technician
                                    .maxOpenTickets
                                }
                                onChange={
                                  event =>
                                    updateTech(
                                      technician.userId,
                                      {
                                        maxOpenTickets:
                                          Number(
                                            event.target
                                              .value,
                                          ),
                                      },
                                    )
                                }
                              />
                            </label>

                            <label>
                              Prioridad

                              <input
                                type="number"
                                min={0}
                                max={1000}
                                value={
                                  technician.priority
                                }
                                onChange={
                                  event =>
                                    updateTech(
                                      technician.userId,
                                      {
                                        priority:
                                          Number(
                                            event.target
                                              .value,
                                          ),
                                      },
                                    )
                                }
                              />
                            </label>

                            <label>
                              Zona horaria

                              <input
                                value={
                                  technician.timeZoneId
                                }
                                onChange={
                                  event =>
                                    updateTech(
                                      technician.userId,
                                      {
                                        timeZoneId:
                                          event.target
                                            .value,
                                      },
                                    )
                                }
                              />
                            </label>
                          </div>

                          <div
                            className="hdgp-schedule"
                          >
                            <h3>
                              <Clock3
                                size={16}
                              />

                              Horario semanal
                            </h3>

                            {DAYS.map(
                              (
                                dayName,
                                day,
                              ) => {
                                const slot =
                                  technician.slots
                                    .find(
                                      item =>
                                        item.day ===
                                          day,
                                    )

                                return (
                                  <div
                                    key={
                                      day
                                    }
                                    className="hdgp-slot"
                                  >
                                    <label>
                                      <input
                                        type="checkbox"
                                        checked={
                                          Boolean(
                                            slot,
                                          )
                                        }
                                        onChange={
                                          () =>
                                            toggleDay(
                                              technician,
                                              day,
                                            )
                                        }
                                      />

                                      {
                                        dayName
                                      }
                                    </label>

                                    <input
                                      type="time"
                                      disabled={
                                        !slot
                                      }
                                      value={
                                        slot?.start
                                        ??
                                        '08:00'
                                      }
                                      onChange={
                                        event =>
                                          updateSlot(
                                            technician,
                                            day,
                                            {
                                              start:
                                                event.target
                                                  .value,
                                            },
                                          )
                                      }
                                    />

                                    <span>
                                      a
                                    </span>

                                    <input
                                      type="time"
                                      disabled={
                                        !slot
                                      }
                                      value={
                                        slot?.end
                                        ??
                                        '17:00'
                                      }
                                      onChange={
                                        event =>
                                          updateSlot(
                                            technician,
                                            day,
                                            {
                                              end:
                                                event.target
                                                  .value,
                                            },
                                          )
                                      }
                                    />
                                  </div>
                                )
                              },
                            )}
                          </div>
                        </section>
                      ),
                    )
                  )}
                </>
              )}

              {/* ================================================
                  SAVE BAR
                 ================================================ */}

              <div
                className="hdgp-savebar"
              >
                <div>
                  <Save
                    size={17}
                  />

                  <span>
                    {
                      dirty
                        ? 'Hay cambios pendientes.'
                        : 'Configuración guardada.'
                    }
                  </span>
                </div>

                <button
                  type="button"
                  className="hdgp-primary"
                  disabled={
                    saving
                    ||
                    !dirty
                  }
                  onClick={
                    () =>
                      void save()
                  }
                >
                  <Save
                    size={15}
                  />

                  {
                    saving
                      ? 'Guardando…'
                      : 'Guardar configuración'
                  }
                </button>
              </div>
            </fieldset>
          )}
        </section>
      </div>
    </main>
  )
}