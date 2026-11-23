import { useCallback, useEffect, useState } from 'react'
import type { LoginResponse } from './api/auth'
import LoginForm from './components/LoginForm'
import ProductsPage from './components/ProductsPage'
import './App.css'

export default function App() {
  const [session, setSession] = useState<LoginResponse | null>(null)
  const [notice, setNotice] = useState('')

  const handleSessionExpired = useCallback(() => {
    setSession(null)
    setNotice('Your session has expired. Please sign in again.')
  }, [])

  function handleLogin(nextSession: LoginResponse) {
    setNotice('')
    setSession(nextSession)
  }

  function handleLogout() {
    setSession(null)
    setNotice('')
  }

  useEffect(() => {
    if (!session) {
      return
    }

    const remainingTime = Math.max(
      0,
      Date.parse(session.expiresAtUtc) - Date.now(),
    )

    const timeout = window.setTimeout(
      handleSessionExpired,
      remainingTime,
    )

    return () => window.clearTimeout(timeout)
  }, [session, handleSessionExpired])

  return (
    <main className={session ? 'workspace' : undefined}>
      {session ? (
        <section>
          <header className="app-header">
            <h1>StockFlow</h1>
            <button onClick={handleLogout}>Sign out</button>
          </header>

          <ProductsPage
            token={session.accessToken}
            onUnauthorized={handleSessionExpired}
          />
        </section>
      ) : (
        <>
          {notice && <p role="status">{notice}</p>}
          <LoginForm onLogin={handleLogin} />
        </>
      )}
    </main>
  )
}
