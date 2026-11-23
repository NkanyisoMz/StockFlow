import { useState } from 'react'
import type { LoginResponse } from './api/auth'
import LoginForm from './components/LoginForm'
import './App.css'

export default function App() {
  const [session, setSession] = useState<LoginResponse | null>(null)

  return (
    <main>
      {session ? (
        <section>
          <h1>StockFlow</h1>
          <p>You are signed in.</p>
          <p>
            Session expires at{' '}
            {new Date(session.expiresAtUtc).toLocaleTimeString()}.
          </p>

          <button onClick={() => setSession(null)}>
            Sign out
          </button>
        </section>
      ) : (
        <LoginForm onLogin={setSession} />
      )}
    </main>
  )
}
