import { UnauthorizedError } from './products'

export interface Customer {
  id: number
  name: string
  email: string
  createdAt: string
}

export async function getCustomers(
  token: string,
  signal: AbortSignal,
): Promise<Customer[]> {
  const response = await fetch('/api/customers', {
    headers: {
      Authorization: `Bearer ${token}`,
    },
    signal,
  })

  if (response.status === 401) {
    throw new UnauthorizedError()
  }

  if (response.status === 403) {
    throw new Error('You do not have permission to view customers.')
  }

  if (!response.ok) {
    throw new Error('Unable to load customers.')
  }

  return response.json()
}
