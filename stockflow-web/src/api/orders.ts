import { UnauthorizedError } from './products'
import type { PagedResult } from './products'

export interface OrderItem {
  id: number
  productId: number
  quantity: number
  unitPrice: number
  lineTotal: number
}

export interface Order {
  id: number
  customerId: number
  orderDate: string
  status: number
  totalAmount: number
  items: OrderItem[]
}

export function orderStatusLabel(status: number): string {
  switch (status) {
    case 0:
      return 'Pending'
    case 1:
      return 'Confirmed'
    case 2:
      return 'Cancelled'
    default:
      return 'Unknown'
  }
}

export async function getOrders(
  token: string,
  page: number,
  signal: AbortSignal,
): Promise<PagedResult<Order>> {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: '10',
  })

  const response = await fetch(`/api/orders?${query}`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
    signal,
  })

  if (response.status === 401) {
    throw new UnauthorizedError()
  }

  if (response.status === 403) {
    throw new Error('You do not have permission to view orders.')
  }

  if (!response.ok) {
    throw new Error('Unable to load orders.')
  }

  return response.json()
}
