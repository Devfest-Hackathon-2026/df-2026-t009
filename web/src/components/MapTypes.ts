import type { LatLng } from '../geo'
import type { Me } from '../useLocation'
import type { TrailPoint } from '../useTrail'
import type { Drop, EventStop, Footprint } from '../types'
import type { AvatarLook } from "./Crystal"

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
  /** 今日の自分の足あと */
  trail?: TrailPoint[]
  /** みんなの踏み跡（マス目ごとの回数） */
  footprints?: Footprint[]
  /** 表示中のイベントの巡る順 */
  route?: EventStop[]
  /** デバッグ：立体表示 */
  tilt?: boolean
  /** 地図の配色（ホログラム＝暗い／ミルク＝明るい） */
  theme?: "holo" | "milk"
  /** 自分の人形 */
  look?: AvatarLook
}
