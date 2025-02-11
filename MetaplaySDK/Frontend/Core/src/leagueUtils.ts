// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type { Variant } from '@metaplay/meta-ui-next'

export function getPhaseVariant(phase: string): Variant {
  if (phase === 'Preview') return 'primary'
  if (phase === 'Active') return 'success'
  if (phase === 'EndingSoon') return 'warning'
  else return 'neutral'
}
