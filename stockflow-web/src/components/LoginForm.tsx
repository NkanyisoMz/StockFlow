import { useState } from 'react'
import { login } from '../api/auth'
import type { LoginResponse } from '../api/auth'

interface LoginFormProps {
  onLogin: (session: LoginResponse) => void
}

export default function LoginForm({ onLogin }: LoginFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleLogin() {
    setError('')
    setIsSubmitting(true)

    try {
      const session = await login(email.trim(), password)

      setPassword('')
      onLogin(session)
    } catch (error) {
      setError(
        error instanceof TypeError
          ? 'Cannot reach the API. Check that the backend is running.'
          : error instanceof Error
            ? error.message
            : 'Unable to sign in.',
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        void handleLogin()
      }}
    >
      <h1>Sign in to StockFlow</h1>

      <div>
        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          required
          disabled={isSubmitting}
        />
      </div>

      <div>
        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
          disabled={isSubmitting}
        />
      </div>

      {error && <p role="alert">{error}</p>}

      <button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Signing in…' : 'Sign in'}
      </button>
    </form>
  )
}
