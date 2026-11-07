import type { LatLng } from '../geo'
import type { Me } from '../useLocation'
import type { Drop } from '../types'

export type MapViewProps = {
  drops: Drop[]
  me: Me
  uid: string | undefined
  openedIds: Set<string>
  selectedId: string | null
  onSelect: (id: string | null) => void
  placing: boolean
  onCenterChange: (c: LatLng) => void
  onTapLocation: (p: LatLng) => void
  recenterSignal: number
  initial?: string
}
