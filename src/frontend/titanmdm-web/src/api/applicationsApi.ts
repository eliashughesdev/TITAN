import apiClient from './apiClient'

export interface ApplicationSummary {
  packageName: string
  applicationName: string

  versionName:
    string | null

  versionCode: number

  isSystemApp: boolean

  deviceCount: number

  enabledCount: number

  lastSeenAtUtc: string
}

export interface DeviceApplication {
  id: string

  deviceId: string

  deviceName: string

  packageName: string

  applicationName: string

  versionName:
    string | null

  versionCode: number

  isSystemApp: boolean

  isEnabled: boolean

  isPresent: boolean

  installerPackageName:
    string | null

  firstInstallTimeUtc:
    string | null

  lastUpdateTimeUtc:
    string | null

  firstSeenAtUtc: string

  lastSeenAtUtc: string
}

export interface ApplicationsQuery {
  search?: string

  systemApp?: boolean
}

export interface SoftwarePackage {
  id: string

  name: string

  version: string

  packageType: string

  originalFileName: string

  sha256: string

  sizeBytes: number

  installArguments:
    string | null

  isActive: boolean

  createdAtUtc: string
}

export interface SoftwareDeployment {
  id: string

  packageId: string

  packageName: string

  packageVersion: string

  targetType: string

  targetId: string

  targetName: string

  status: string

  queuedDevices: number

  createdAtUtc: string
}

export interface UploadSoftwarePackageRequest {
  name: string

  version: string

  packageType:
    | 'MSI'
    | 'EXE'
    | 'MSIX'
    | 'APPX'

  installArguments?: string

  file: File
}

export interface DeploySoftwarePackageRequest {
  targetType:
    | 'Device'
    | 'Group'

  targetId: string
}

export const applicationsApi = {
  async getAll(
    query:
      ApplicationsQuery = {},
  ): Promise<ApplicationSummary[]> {
    const response =
      await apiClient
        .get<ApplicationSummary[]>(
          '/applications',
          {
            params: {
              search:
                query.search
                ||
                undefined,

              systemApp:
                query.systemApp,
            },
          },
        )

    return response.data
  },

  async getByDevice(
    deviceId: string,
  ): Promise<DeviceApplication[]> {
    const response =
      await apiClient
        .get<DeviceApplication[]>(
          `/applications/device/${deviceId}`,
        )

    return response.data
  },

  async getPackages():
    Promise<SoftwarePackage[]> {
    const response =
      await apiClient
        .get<SoftwarePackage[]>(
          '/software-packages',
        )

    return response.data
  },

  async getDeployments():
    Promise<SoftwareDeployment[]> {
    const response =
      await apiClient
        .get<SoftwareDeployment[]>(
          '/software-packages/deployments',
        )

    return response.data
  },

  async uploadPackage(
    request:
      UploadSoftwarePackageRequest,
  ): Promise<SoftwarePackage> {
    const formData =
      new FormData()

    formData.append(
      'name',
      request.name.trim(),
    )

    formData.append(
      'version',
      request.version.trim(),
    )

    formData.append(
      'packageType',
      request.packageType,
    )

    if (
      request.installArguments
        ?.trim()
    ) {
      formData.append(
        'installArguments',
        request
          .installArguments
          .trim(),
      )
    }

    formData.append(
      'file',
      request.file,
      request.file.name,
    )

    /*
     * No establecemos Content-Type manualmente.
     * Axios/browser genera automáticamente el
     * boundary correcto del multipart/form-data.
     */
    const response =
      await apiClient
        .post<SoftwarePackage>(
          '/software-packages',
          formData,
        )

    return response.data
  },

  async deployPackage(
    packageId: string,
    request:
      DeploySoftwarePackageRequest,
  ): Promise<SoftwareDeployment> {
    const response =
      await apiClient
        .post<SoftwareDeployment>(
          `/software-packages/${packageId}/deploy`,
          request,
        )

    return response.data
  },
}