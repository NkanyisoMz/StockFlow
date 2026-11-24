import { UnauthorizedError } from './products'

export type StockOperation = 'stock-in' | 'stock-out'

export async function recordStockMovement(
  token: string,
  operation: StockOperation,
  productId: number,
  quantity: number,
  reason: string,
): Promise<void> {
  const response = await fetch(`/api/inventory/${operation}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ productId, quantity, reason }),
  })

  if (response.status === 401) {
    throw new UnauthorizedError()
  }

  if (response.status === 403) {
    throw new Error('You do not have permission to change stock.')
  }

  if (response.status === 404) {
    throw new Error('This product no longer exists.')
  }

  if (response.status === 409) {
    throw new Error(
      operation === 'stock-in'
        ? 'This receipt would exceed the maximum stock of 100,000 units.'
        : 'There is not enough stock for this deduction.',
    )
  }

  if (!response.ok) {
    throw new Error('Unable to record the movement. Check the entered details.')
  }
}

export interface InventoryMovement {
  id: number
  productId: number
  type: number
  quantity: number
  reason: string
  createdAt: string
}

export async function getMovementHistory(
  token: string,
  productId: number,
  signal: AbortSignal,
): Promise<InventoryMovement[]> {
  const response = await fetch(
    `/api/inventory/products/${productId}/history`,
    {
      headers: {
        Authorization: `Bearer ${token}`,
      },
      signal,
    },
  )

  if (response.status === 401) {
    throw new UnauthorizedError()
  }

  if (response.status === 403) {
    throw new Error('You do not have permission to view inventory history.')
  }

  if (response.status === 404) {
    throw new Error('This product no longer exists.')
  }

  if (!response.ok) {
    throw new Error('Unable to load inventory history.')
  }

  return response.json()
}
