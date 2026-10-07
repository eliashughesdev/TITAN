import axios from 'axios'

import {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from 'react'

import {
  deviceGroupsApi,
  type DeviceGroup,
  type DeviceGroupDetails,
} from '../../../api/deviceGroupsApi'

import {
  devicesApi,
} from '../../../api/devicesApi'

import type {
  DeviceListItem,
} from '../../../types/device'

import {
  COMMAND_OPTIONS,
} from '../deviceGroups.constants'

import {
  buildDynamicRule,
  parseDynamicRule,
} from '../deviceGroups.rules'

import type {
  GroupMode,
  PlatformFilter,
  StatusFilter,
} from '../deviceGroups.types'

function getApiErrorMessage(
  error: unknown,
  fallback: string,
): string {
  if (
    axios.isAxiosError(error)
  ) {
    const data =
      error.response?.data as
        | {
            message?: string
          }
        | undefined

    if (
      data?.message
    ) {
      return data.message
    }

    if (
      error.response?.status ===
      403
    ) {
      return 'No tienes permisos para realizar esta operación.'
    }

    if (
      error.response?.status ===
      404
    ) {
      return 'El recurso solicitado ya no existe.'
    }

    if (
      error.response?.status ===
      409
    ) {
      return 'La operación produjo un conflicto con el estado actual.'
    }

    if (
      error.response?.status &&
      error.response.status >=
        500
    ) {
      return 'El servidor no pudo completar la operación.'
    }
  }

  if (
    error instanceof Error &&
    error.message
  ) {
    return error.message
  }

  return fallback
}

export function useDeviceGroupsController() {
  const [
    groups,
    setGroups,
  ] =
    useState<DeviceGroup[]>([])

  const [
    devices,
    setDevices,
  ] =
    useState<DeviceListItem[]>([])

  const [
    selectedGroup,
    setSelectedGroup,
  ] =
    useState<DeviceGroupDetails | null>(
      null,
    )

  const [
    selectedDeviceIds,
    setSelectedDeviceIds,
  ] =
    useState<Set<string>>(
      new Set(),
    )

  const [
    search,
    setSearch,
  ] =
    useState('')

  const [
    platformFilter,
    setPlatformFilter,
  ] =
    useState<PlatformFilter>(
      'All',
    )

  const [
    statusFilter,
    setStatusFilter,
  ] =
    useState<StatusFilter>(
      'All',
    )

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
    mode,
    setMode,
  ] =
    useState<GroupMode>(
      'static',
    )

  const [
    dynamicPlatform,
    setDynamicPlatform,
  ] =
    useState('Windows')

  const [
    dynamicStatus,
    setDynamicStatus,
  ] =
    useState('Online')

  const [
    editName,
    setEditName,
  ] =
    useState('')

  const [
    editDescription,
    setEditDescription,
  ] =
    useState('')

  const [
    editDynamicPlatform,
    setEditDynamicPlatform,
  ] =
    useState('Windows')

  const [
    editDynamicStatus,
    setEditDynamicStatus,
  ] =
    useState('Online')

  const [
    editingGroup,
    setEditingGroup,
  ] =
    useState(false)

  const [
    commandType,
    setCommandType,
  ] =
    useState('PING')

  const [
    loading,
    setLoading,
  ] =
    useState(true)

  const [
    working,
    setWorking,
  ] =
    useState(false)

  const [
    message,
    setMessage,
  ] =
    useState<string | null>(
      null,
    )

  const [
    error,
    setError,
  ] =
    useState<string | null>(
      null,
    )

  const clearFeedback =
    useCallback(() => {
      setMessage(null)
      setError(null)
    }, [])

  useEffect(() => {
    if (
      !message &&
      !error
    ) {
      return
    }

    const timer =
      window.setTimeout(
        () => {
          setMessage(null)
          setError(null)
        },
        7000,
      )

    return () =>
      window.clearTimeout(
        timer,
      )
  }, [
    message,
    error,
  ])

  const loadBase =
    useCallback(
      async () => {
        try {
          setLoading(true)
          setError(null)

          const [
            groupData,
            deviceData,
          ] =
            await Promise.all([
              deviceGroupsApi
                .getAll(),

              devicesApi
                .getDevices({
                  page: 1,
                  pageSize: 100,
                }),
            ])

          setGroups(
            groupData,
          )

          setDevices(
            deviceData.items,
          )
        } catch (loadError) {
          setError(
            getApiErrorMessage(
              loadError,
              'No fue posible cargar la administración de flota.',
            ),
          )
        } finally {
          setLoading(false)
        }
      },
      [],
    )

  useEffect(() => {
    void loadBase()
  }, [
    loadBase,
  ])

  useEffect(() => {
    document.title =
      'Grupos y Flota | TitanMDM'
  }, [])

  const filteredDevices =
    useMemo(() => {
      const value =
        search
          .trim()
          .toLowerCase()

      return devices.filter(
        device => {
          const matchesSearch =
            !value
            ||
            device.deviceName
              .toLowerCase()
              .includes(
                value,
              )
            ||
            device.serialNumber
              .toLowerCase()
              .includes(
                value,
              )
            ||
            (
              device.assignedUser ??
              ''
            )
              .toLowerCase()
              .includes(
                value,
              )
            ||
            (
              device.department ??
              ''
            )
              .toLowerCase()
              .includes(
                value,
              )

          const matchesPlatform =
            platformFilter ===
              'All'
            ||
            device.platform ===
              platformFilter

          const matchesStatus =
            statusFilter ===
              'All'
            ||
            device.status ===
              statusFilter

          return (
            matchesSearch
            &&
            matchesPlatform
            &&
            matchesStatus
          )
        },
      )
    }, [
      devices,
      search,
      platformFilter,
      statusFilter,
    ])

  const stats =
    useMemo(() => {
      const windowsDevices =
        devices.filter(
          device =>
            device.platform ===
            'Windows',
        ).length

      const androidDevices =
        devices.filter(
          device =>
            device.platform ===
            'Android',
        ).length

      const onlineDevices =
        devices.filter(
          device =>
            device.status ===
            'Online',
        ).length

      const compliantDevices =
        devices.filter(
          device =>
            device.complianceStatus ===
            'Compliant',
        ).length

      const totalMembers =
        groups.reduce(
          (
            total,
            group,
          ) =>
            total +
            group.deviceCount,
          0,
        )

      return {
        windowsDevices,
        androidDevices,
        onlineDevices,
        compliantDevices,
        totalMembers,
      }
    }, [
      devices,
      groups,
    ])

  const selectedDevices =
    useMemo(
      () =>
        devices.filter(
          device =>
            selectedDeviceIds.has(
              device.id,
            ),
        ),
      [
        devices,
        selectedDeviceIds,
      ],
    )

  const selectedWindowsCount =
    useMemo(
      () =>
        selectedDevices.filter(
          device =>
            device.platform ===
            'Windows',
        ).length,
      [
        selectedDevices,
      ],
    )

  const currentCommand =
    useMemo(
      () =>
        COMMAND_OPTIONS.find(
          command =>
            command.value ===
            commandType,
        ),
      [
        commandType,
      ],
    )

  function toggleDevice(
    deviceId: string,
  ) {
    setSelectedDeviceIds(
      current => {
        const next =
          new Set(
            current,
          )

        if (
          next.has(
            deviceId,
          )
        ) {
          next.delete(
            deviceId,
          )
        } else {
          next.add(
            deviceId,
          )
        }

        return next
      },
    )
  }

  function toggleAllVisible() {
    const visibleIds =
      filteredDevices.map(
        device =>
          device.id,
      )

    const allSelected =
      visibleIds.length >
        0
      &&
      visibleIds.every(
        id =>
          selectedDeviceIds.has(
            id,
          ),
      )

    setSelectedDeviceIds(
      current => {
        const next =
          new Set(
            current,
          )

        visibleIds.forEach(
          id => {
            if (
              allSelected
            ) {
              next.delete(
                id,
              )
            } else {
              next.add(
                id,
              )
            }
          },
        )

        return next
      },
    )
  }

  function selectWindows() {
    setSelectedDeviceIds(
      new Set(
        filteredDevices
          .filter(
            device =>
              device.platform ===
              'Windows',
          )
          .map(
            device =>
              device.id,
          ),
      ),
    )
  }

  function selectOnline() {
    setSelectedDeviceIds(
      new Set(
        filteredDevices
          .filter(
            device =>
              device.status ===
              'Online',
          )
          .map(
            device =>
              device.id,
          ),
      ),
    )
  }

  function clearSelection() {
    setSelectedDeviceIds(
      new Set(),
    )
  }

  async function openGroup(
    groupId: string,
  ) {
    if (working) {
      return
    }

    try {
      setWorking(true)
      clearFeedback()

      const details =
        await deviceGroupsApi
          .getById(
            groupId,
          )

      setSelectedGroup(
        details,
      )

      setEditName(
        details.name,
      )

      setEditDescription(
        details.description ??
          '',
      )

      if (
        details.isDynamic
      ) {
        const parsedRule =
          parseDynamicRule(
            details.ruleJson,
          )

        setEditDynamicPlatform(
          parsedRule.platform,
        )

        setEditDynamicStatus(
          parsedRule.status,
        )
      } else {
        setEditDynamicPlatform(
          'Windows',
        )

        setEditDynamicStatus(
          'Online',
        )
      }

      setEditingGroup(
        false,
      )

      setSelectedDeviceIds(
        new Set(
          details.members.map(
            member =>
              member.deviceId,
          ),
        ),
      )
    } catch (openError) {
      setError(
        getApiErrorMessage(
          openError,
          'No fue posible abrir el grupo.',
        ),
      )
    } finally {
      setWorking(false)
    }
  }

  function closeGroup() {
    if (working) {
      return
    }

    setSelectedGroup(
      null,
    )

    setEditingGroup(
      false,
    )

    setSelectedDeviceIds(
      new Set(),
    )

    clearFeedback()
  }

  async function createGroup() {
    if (working) {
      return
    }

    if (
      !name.trim()
    ) {
      setError(
        'El grupo necesita un nombre.',
      )

      return
    }

    try {
      setWorking(true)
      clearFeedback()

      const ruleJson =
        mode ===
        'dynamic'
          ?
            buildDynamicRule(
              dynamicPlatform,
              dynamicStatus,
            )
          :
            null

      const created =
        await deviceGroupsApi
          .create({
            name:
              name.trim(),

            description:
              description
                .trim()
              ||
              null,

            isDynamic:
              mode ===
              'dynamic',

            ruleJson,

            deviceIds:
              mode ===
              'static'
                ?
                  Array.from(
                    selectedDeviceIds,
                  )
                :
                  [],
          })

      setName('')
      setDescription('')
      setSelectedDeviceIds(
        new Set(),
      )

      await loadBase()

      await openGroup(
        created.id,
      )

      setMessage(
        `Grupo "${created.name}" creado correctamente.`,
      )
    } catch (createError) {
      setError(
        getApiErrorMessage(
          createError,
          'No fue posible crear el grupo.',
        ),
      )
    } finally {
      setWorking(false)
    }
  }

  async function updateGroup() {
    if (
      working ||
      !selectedGroup
    ) {
      return
    }

    if (
      !editName.trim()
    ) {
      setError(
        'El nombre del grupo no puede estar vacío.',
      )

      return
    }

    try {
      setWorking(true)
      clearFeedback()

      const updatedRuleJson =
        selectedGroup
          .isDynamic
          ?
            buildDynamicRule(
              editDynamicPlatform,
              editDynamicStatus,
            )
          :
            null

      const updated =
        await deviceGroupsApi
          .update(
            selectedGroup.id,
            {
              name:
                editName.trim(),

              description:
                editDescription
                  .trim()
                ||
                null,

              isDynamic:
                selectedGroup
                  .isDynamic,

              ruleJson:
                updatedRuleJson,
            },
          )

      setSelectedGroup(
        updated,
      )

      setEditingGroup(
        false,
      )

      setSelectedDeviceIds(
        new Set(
          updated.members.map(
            member =>
              member.deviceId,
          ),
        ),
      )

      await loadBase()

      setMessage(
        'Grupo actualizado correctamente.',
      )
    } catch (updateError) {
      setError(
        getApiErrorMessage(
          updateError,
          'No fue posible actualizar el grupo.',
        ),
      )
    } finally {
      setWorking(false)
    }
  }

  async function synchronizeMembers() {
    if (
      working ||
      !selectedGroup
    ) {
      return
    }

    if (
      selectedGroup.isDynamic
    ) {
      setError(
        'Los miembros de un grupo dinámico se administran automáticamente.',
      )

      return
    }

    try {
      setWorking(true)
      clearFeedback()

      const original =
        new Set(
          selectedGroup
            .members
            .map(
              member =>
                member.deviceId,
            ),
        )

      const additions =
        Array.from(
          selectedDeviceIds,
        )
          .filter(
            id =>
              !original.has(
                id,
              ),
          )

      const removals =
        Array.from(
          original,
        )
          .filter(
            id =>
              !selectedDeviceIds.has(
                id,
              ),
          )

      if (
        additions.length >
        0
      ) {
        await deviceGroupsApi
          .addMembers(
            selectedGroup.id,
            additions,
          )
      }

      for (
        const deviceId
        of removals
      ) {
        await deviceGroupsApi
          .removeMember(
            selectedGroup.id,
            deviceId,
          )
      }

      await openGroup(
        selectedGroup.id,
      )

      await loadBase()

      setMessage(
        `Miembros actualizados: ${additions.length} agregado(s), ${removals.length} eliminado(s).`,
      )
    } catch (syncError) {
      setError(
        getApiErrorMessage(
          syncError,
          'No fue posible actualizar los miembros.',
        ),
      )
    } finally {
      setWorking(false)
    }
  }

  async function sendGroupCommand() {
    if (
      working ||
      !selectedGroup
    ) {
      return
    }

    const memberCount =
      selectedGroup
        .members
        .length

    if (
      memberCount ===
      0
    ) {
      setError(
        'El grupo no contiene dispositivos. No hay destinos para la operación.',
      )

      return
    }

    const nonWindows =
      selectedGroup
        .members
        .filter(
          member =>
            member.platform !==
            'Windows',
        )

    if (
      currentCommand
        ?.windowsOnly
      &&
      nonWindows.length >
        0
    ) {
      setError(
        `La operación "${currentCommand.label}" es exclusiva de Windows. El grupo contiene ${nonWindows.length} dispositivo(s) no compatibles.`,
      )

      return
    }

    const commandLabel =
      currentCommand
        ?.label
      ??
      commandType

    const confirmed =
      window.confirm(
        [
          `Operación: ${commandLabel}`,
          `Grupo: ${selectedGroup.name}`,
          `Destinos: ${memberCount}`,
          '',
          '¿Deseas continuar?',
        ].join(
          '\n',
        ),
      )

    if (
      !confirmed
    ) {
      return
    }

    try {
      setWorking(true)
      clearFeedback()

      const result =
        await deviceGroupsApi
          .executeCommand(
            selectedGroup.id,
            {
              commandType,

              payloadJson:
                '{}',

              expiresInMinutes:
                60,
            },
          )

      await openGroup(
        selectedGroup.id,
      )

      await loadBase()

      setMessage(
        `${commandLabel} fue encolado correctamente para ${result.queuedDevices} dispositivo(s).`,
      )
    } catch (commandError) {
      setError(
        getApiErrorMessage(
          commandError,
          'No fue posible ejecutar la operación masiva.',
        ),
      )
    } finally {
      setWorking(false)
    }
  }

  async function deleteGroup() {
    if (
      working ||
      !selectedGroup
    ) {
      return
    }

    const confirmed =
      window.confirm(
        [
          `Eliminar grupo "${selectedGroup.name}"`,
          '',
          'Los dispositivos NO serán eliminados.',
          'Solo se eliminará el grupo y sus membresías.',
          '',
          '¿Deseas continuar?',
        ].join(
          '\n',
        ),
      )

    if (
      !confirmed
    ) {
      return
    }

    try {
      setWorking(true)
      clearFeedback()

      await deviceGroupsApi
        .delete(
          selectedGroup.id,
        )

      setSelectedGroup(
        null,
      )

      setEditingGroup(
        false,
      )

      setSelectedDeviceIds(
        new Set(),
      )

      await loadBase()

      setMessage(
        'Grupo eliminado correctamente.',
      )
    } catch (deleteError) {
      setError(
        getApiErrorMessage(
          deleteError,
          'No fue posible eliminar el grupo.',
        ),
      )
    } finally {
      setWorking(false)
    }
  }

  return {
    groups,
    devices,
    selectedGroup,
    selectedDeviceIds,
    filteredDevices,

    search,
    platformFilter,
    statusFilter,

    name,
    description,
    mode,
    dynamicPlatform,
    dynamicStatus,

    editName,
    editDescription,
    editDynamicPlatform,
    editDynamicStatus,
    editingGroup,

    commandType,

    loading,
    working,

    message,
    error,

    selectedWindowsCount,
    stats,

    setSearch,
    setPlatformFilter,
    setStatusFilter,

    setName,
    setDescription,
    setMode,
    setDynamicPlatform,
    setDynamicStatus,

    setEditName,
    setEditDescription,
    setEditDynamicPlatform,
    setEditDynamicStatus,
    setEditingGroup,

    setCommandType,

    loadBase,
    openGroup,
    closeGroup,
    createGroup,
    updateGroup,
    synchronizeMembers,
    sendGroupCommand,
    deleteGroup,

    toggleDevice,
    toggleAllVisible,
    selectWindows,
    selectOnline,
    clearSelection,
  }
}