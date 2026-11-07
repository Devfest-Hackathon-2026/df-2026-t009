import { useState } from 'react'
import { CrystalFigure, POSES, TINTS, loadLook, type AvatarLook } from './Crystal'

/** 人形のポーズと色を選ぶ。はじめての画面とプロフィールで使う。 */
export function LookPicker({ look, onChange }: { look: AvatarLook; onChange: (l: AvatarLook) => void }) {
  return (
    <div className="lookpick">
      <div className="lookpick__stage">
        <span className="lookpick__grid" aria-hidden />
        <CrystalFigure look={look} size={150} />
      </div>
      <div className="lookpick__row" role="radiogroup" aria-label="ポーズ">
        {POSES.map((p) => (
          <button
            type="button"
            key={p.id}
            role="radio"
            aria-checked={look.pose === p.id}
            className={look.pose === p.id ? 'on' : ''}
            onClick={() => onChange({ ...look, pose: p.id })}
          >
            <CrystalFigure look={{ ...look, pose: p.id }} size={40} glow={false} />
            <span>{p.label}</span>
          </button>
        ))}
      </div>
      <div className="lookpick__row lookpick__row--tints" role="radiogroup" aria-label="色">
        {TINTS.map((t) => (
          <button
            type="button"
            key={t.id}
            role="radio"
            aria-checked={look.tint === t.id}
            className={look.tint === t.id ? 'on' : ''}
            onClick={() => onChange({ ...look, tint: t.id })}
          >
            <span className="lookpick__swatch" style={{ background: `linear-gradient(135deg, ${t.light}, ${t.mid} 55%, ${t.dark})` }} />
            <span>{t.label}</span>
          </button>
        ))}
      </div>
    </div>
  )
}

export function Welcome({ onDone }: { onDone: (name: string, look: AvatarLook) => void }) {
  const [name, setName] = useState('')
  const [look, setLook] = useState<AvatarLook>(loadLook)
  const [step, setStep] = useState<0 | 1>(0)
  return (
    <div className="overlay overlay--solid">
      <form
        className="welcome"
        onSubmit={(e) => {
          e.preventDefault()
          if (step === 0) return setStep(1)
          if (name.trim()) onDone(name.trim(), look)
        }}
      >
        <p className="welcome__kicker">ONSITE / FIELD LOG SYSTEM</p>
        <h1>onsite</h1>
        <p className="welcome__lead">近づくと、ひらく。</p>

        {step === 0 ? (
          <>
            <ol className="welcome__steps">
              <li>地図には、誰かがその場所に残した投稿の「予告」だけが見えます</li>
              <li>本文と写真は、実際に近くまで行った人だけがひらけます</li>
              <li>街歩きでも旅先でも。見つけたものを置いたり、巡るイベントを作ったりできます</li>
            </ol>
            <button className="btn btn--primary btn--wide">はじめる</button>
          </>
        ) : (
          <>
            <p className="welcome__label">YOUR FIGURE — 地図に立つ、あなたの人形</p>
            <LookPicker look={look} onChange={setLook} />
            <label className="field">
              <span>ニックネーム</span>
              <input value={name} maxLength={20} placeholder="例：たびびと" onChange={(e) => setName(e.target.value)} />
            </label>
            <button className="btn btn--primary btn--wide" disabled={!name.trim()}>
              この人形で出発する
            </button>
          </>
        )}
        <p className="welcome__note">現在地は、近くにいるかの判定にだけ使います。ほかの人に共有されることはありません。</p>
      </form>
    </div>
  )
}
