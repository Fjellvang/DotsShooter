// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

export interface ServerEndpoint {
  serverHost: string
  serverPort: number
  enableTls: boolean
  cdnBaseUrl: string
}

export interface ClientPatchVersionRequirement {
  forLogicVersion: number
  /**
   * Game-specific set of deployment platforms that this revision is required for.
   * @example "iOS"
   */
  forPlatform: string | null
  minPatchVersion: number
}

export interface ClientCompatibilitySettings {
  activeLogicVersionRange: {
    minVersion: number
    maxVersion: number
  }
  redirectEnabled: boolean
  redirectServerEndpoint: ServerEndpoint | null
  clientPatchVersionRequirements: ClientPatchVersionRequirement[]
}

export interface StatusResponse {
  $type: 'Metaplay.Server.AdminApi.Controllers.SystemStatusController+StatusResponse'
  clientCompatibilitySettings: ClientCompatibilitySettings
  maintenanceStatus: {
    $type: 'Metaplay.Server.MaintenanceStatus'
    scheduledMaintenanceMode: {
      $type: 'Metaplay.Server.ScheduledMaintenanceMode'
      startAt: string
      estimatedDurationInMinutes: number
      estimationIsValid: boolean
      platformExclusions: string[]
    }
    isInMaintenance: boolean
  }
  liveEntityCounts: Record<string, number | undefined>
  databaseStatus: {
    $type: 'Metaplay.Server.AdminApi.Controllers.SystemStatusController+StatusResponse+DatabaseConfig'
    backend: string
    activeShards: number
    totalShards: number
  }
  numConcurrents: number
  gameTimeOffset: string
}

export interface LogEventInfo {
  id: string
  timestamp: string
  message: string
  logEventType: string
  source: string
  sourceType: string
  exception: string
  stackTrace: string
}

export interface ErrorCountResponse {
  collectorRestartedWithinMaxAge: boolean
  maxAge: string
  collectorRestartTime: string
  errorCount: number
  errors: LogEventInfo[]
  overMaxErrorCount: boolean
}

// TODO: Share with portal?
export interface TelemetryMessageLink {
  text: string
  url: string
}

export interface TelemetryMessage {
  category: string
  level: string
  title: string
  body: string
  links: TelemetryMessageLink[]
}

/**
 * Telemetry messages that the server has received from the portal recently.
 */
export interface TelemetryMessagesResponse {
  updatedAt: string
  messages?: TelemetryMessage[]
}
