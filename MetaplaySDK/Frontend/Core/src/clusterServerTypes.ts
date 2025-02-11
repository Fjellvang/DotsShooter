export interface ClusterInfo {
  nodeSets: NodeSet[]
}

export type NodeState = 'NotConnected' | 'HardLimit' | 'SoftLimit' | 'ExpectedNotConnected' | 'Perfect'

export interface NodeSet {
  nodes: Node[]
  desiredNodeCount: number
  nodeCount: number
  entityKinds: string[]

  mostSevereStatus: NodeState

  scalingState: 'Idle' | 'ScalingUp' | 'ScalingDown' | 'AtMaxNodeCount'

  minNodeCount: number
  maxNodeCount: number

  scalingMode: 'Static' | 'DynamicLinear'
  name: string
  remotingPort: number
  hostName: string
  globalDnsSuffix: string
  connectedNodes: number

  aggregatedLiveEntityCounts: Record<string, number>

  nodeCountOutsideHardLimit: number
  nodeCountOutsideSoftLimit: number
}

export enum entityShardGroup {
  'BaseServices',
  'ServiceProxies',
  'Workloads',
}

export interface Node {
  name: string

  // The current status of the nodes, based on data gathered from multiple systems
  nodeStatus: NodeState
  nodeScalingState: 'UnknownState' | 'Idle' | 'Draining' | 'Killing'
  nodeAddress: string

  isWithinHardLimit: boolean
  isWithinSoftLimit: boolean

  publicIp: string

  serverStartedAt: string // MetaTime

  // This is internal data gathered from different systems, and thus it might have differing opinions on what the node is currently doing
  rawNodeData: RawNodeData

  // Url to the grafana log view filtering only logs from this node. Null if not available
  grafanaLogUrl: string | null
}

export interface RawNodeData {
  // Autoscaling
  scalingNodeState: 'Dead' | 'Working' | 'Draining' | 'Killing' | null
  lastWorkAt: string | null // MetaTime

  // ClusterConnectionManger
  clusterLocalPhase: 'Connected' | 'Starting' | 'Running' | 'Stopping' | 'Terminated' | null
  entityGroupPhases: Record<entityShardGroup, 'NotCreated' | 'Created' | 'Running'>
  isConnected: boolean

  // Workload tracking
  // eslint-disable-next-line @typescript-eslint/no-redundant-type-constituents
  rawWorkload: any | null // user data

  // Stats collector
  liveEntityCounts: Record<string, number>
}
