// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * A list of all the available placements for injecting UI components into the dashboard.
 */
export const uiPlacementList = [
  // Players Views
  'Players/Details/Overview:Title',
  'Players/Details/Overview:Subtitle',
  'Players/Details/Overview:LeftPanel',
  'Players/Details/AdminActions:Gentle',
  'Players/Details/AdminActions:Disruptive',
  'Players/Details/AdminActions:Dangerous',
  'Players/Details/Tab0',
  'Players/Details/Tab1',
  'Players/Details/Tab2',
  'Players/Details/Tab3',
  'Players/Details/Tab4',
  // Guilds Views
  'Guilds/Details/AdminActions:Gentle',
  'Guilds/Details/AdminActions:Disruptive',
  'Guilds/Details/GameState',
  'Guilds/Details/GuildAdminLogs',
  // Incidents Views
  'PlayerIncidents/List',
  'PlayerIncidents/Details',
  'PlayerIncidents/ByType',
  // Leagues Views
  'Leagues/List',
  'Leagues/Details',
  'Leagues/Season/Details',
  'Leagues/Season/RankDivision/Details',
  // ScanJobs Views
  'ScanJobs/List',
  // Broadcasts Views
  'Broadcasts/Details',
  // Matchmakers Views
  'Matchmakers/List',
  'Matchmakers/Details',
  // System View
  'System/Common',
  'System/Advanced',
  // Localizations Views
  'Localizations/List',
  'Localizations/Details/Tab0',
  'Localizations/Details/Tab1',
  'Localizations/Diff',
  // GameConfigs Views
  'GameConfigs/List',
  'GameConfigs/Details/Tab0',
  'GameConfigs/Details/Tab1',
  'GameConfigs/Details/Tab2',
  'GameConfig/Diff',
  // Overview View
  'OverviewView',
  // Developer View
  'Developers/List',
  // LiveOps Event View
  'LiveOpsEvents/List',
  'LiveOpsEvents/Details',
] as const
