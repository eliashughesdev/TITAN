export type EnrollmentPlatformTab =
  | 'android'
  | 'windows'

interface EnrollmentPlatformTabsProps {
  activePlatform: EnrollmentPlatformTab
  canViewAndroid?: boolean
  canViewWindows?: boolean
  onPlatformChange:
    (platform: EnrollmentPlatformTab) => void
}

export default function EnrollmentPlatformTabs({
  activePlatform,
  canViewAndroid = true,
  canViewWindows = true,
  onPlatformChange,
}: EnrollmentPlatformTabsProps) {
  return (
    <div
      className="enrollment-platform-tabs"
      role="tablist"
      aria-label="Plataforma de inscripción"
    >
      {canViewAndroid && (
        <button
          type="button"
          role="tab"
          aria-selected={
            activePlatform === 'android'
          }
          className={
            activePlatform === 'android'
              ? 'enrollment-platform-tab enrollment-platform-tab-active'
              : 'enrollment-platform-tab'
          }
          onClick={() =>
            onPlatformChange('android')
          }
        >
          <span className="enrollment-platform-tab-icon">
            A
          </span>

          <span>
            <strong>Android</strong>

            <small>
              Enterprise y TitanMDM Agent
            </small>
          </span>
        </button>
      )}

      {canViewWindows && (
        <button
          type="button"
          role="tab"
          aria-selected={
            activePlatform === 'windows'
          }
          className={
            activePlatform === 'windows'
              ? 'enrollment-platform-tab enrollment-platform-tab-active'
              : 'enrollment-platform-tab'
          }
          onClick={() =>
            onPlatformChange('windows')
          }
        >
          <span className="enrollment-platform-tab-icon">
            W
          </span>

          <span>
            <strong>Windows</strong>

            <small>
              TitanMDM Windows Agent
            </small>
          </span>
        </button>
      )}
    </div>
  )
}