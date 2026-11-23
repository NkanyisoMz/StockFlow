export interface LoginResponse {
  accessToken: string
  expiresAtUtc: string
}

export async function login(
  email: string,
  password: string,
): Promise<LoginResponse> {
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ email, password }),
  })

  if (!response.ok) {
    if (response.status === 401) {
      throw new Error('Invalid email or password.')
    }

    if (response.status === 400) {
      throw new Error('Enter a valid email and password.')
    }

    throw new Error('Unable to sign in. Please try again.')
  }

  const result: LoginResponse = await response.json()

  if (
    typeof result.accessToken !== 'string' ||
    !result.accessToken ||
    typeof result.expiresAtUtc !== 'string' ||
    !Number.isFinite(Date.parse(result.expiresAtUtc))
  ) {
    throw new Error('The server returned an unexpected login response.')
  }

  return result
}
