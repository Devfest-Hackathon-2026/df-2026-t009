import { useState } from 'react'

export function Welcome({ onDone }: { onDone: (name: string) => void }) {
  const [name, setName] = useState('')
  return (
    <div className="overlay overlay--solid">
      <form
        className="welcome"
        onSubmit={(e) => {
          e.preventDefault()
          if (name.trim()) onDone(name.trim())
        }}
      >
        <div className="welcome__logo" aria-hidden>
          <span />
        </div>
        <h1>ここだけ</h1>
        <p className="welcome__lead">近づくと、ひらく。</p>
        <ol className="welcome__steps">
          <li>地図には、誰かが置いた投稿の「予告」だけが見えます</li>
          <li>本文と写真は、その場所の近くまで歩いた人だけが読めます</li>
          <li>あなたも、見つけたものを場所に置いていけます</li>
        </ol>
        <label className="field">
          <span>ニックネーム</span>
          <input value={name} maxLength={20} placeholder="例：さんぽ係" onChange={(e) => setName(e.target.value)} />
        </label>
        <button className="btn btn--primary btn--wide" disabled={!name.trim()}>
          はじめる
        </button>
        <p className="welcome__note">
          現在地は、近くにいるかの判定にだけ使います。ほかの人に共有されることはありません。
        </p>
      </form>
    </div>
  )
}
