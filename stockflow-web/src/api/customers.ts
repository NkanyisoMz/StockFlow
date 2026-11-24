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

export interface CustomerInput {
  name: string
  email: string
}

export async function saveCustomer(
  token: string,
  input: CustomerInput,
  customerId?: number,
): Promise<void> {
  const response = await fetch(
    customerId === undefined
      ? '/api/customers'
      : `/api/customers/${customerId}`,
    {
      method: customerId === undefined ? 'POST' : 'PUT',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(input),
    },
  )

  if (response.status === 401) {
    throw new UnauthorizedError()
  }

  if (response.status === 403) {
    throw new Error('You do not have permission to manage customers.')
  }

  if (response.status === 404) {
    throw new Error('This customer no longer exists.')
  }

  if (response.status === 409) {
    throw new Error('Another customer already uses this email.')
  }

  if (response.status === 400) {
    throw new Error('Check the customer name and email address.')
  }

  if (!response.ok) {
    throw new Error('Unable to save the customer.')
  }
}
