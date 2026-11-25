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

export interface OrderFilters {
  customerId?: number
  status?: number
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
  filters: OrderFilters,
  signal: AbortSignal,
): Promise<PagedResult<Order>> {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: '10',
  })

  if (filters.customerId !== undefined) {
    query.set('customerId', String(filters.customerId))
  }

  if (filters.status !== undefined) {
    query.set('status', String(filters.status))
  }

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

export async function getOrder(
  token: string,
  orderId: number,
  signal: AbortSignal,
): Promise<Order> {
  const response = await fetch(`/api/orders/${orderId}`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
    signal,
  })

  if (response.status === 401) {
    throw new UnauthorizedError()
  }

  if (response.status === 403) {
    throw new Error('You do not have permission to view this order.')
  }

  if (response.status === 404) {
    throw new Error('This order no longer exists.')
  }

  if (!response.ok) {
    throw new Error('Unable to load the order.')
  }

  return response.json()
}

export class OrderConflictError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'OrderConflictError'
  }
}

export async function changeOrderStatus(
  token: string,
  orderId: number,
  action: 'confirm' | 'cancel',
): Promise<Order> {
  const response = await fetch(`/api/orders/${orderId}/${action}`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
    },
  })

  if (response.status === 401) {
    throw new UnauthorizedError()
  }

  if (response.status === 403) {
    throw new Error('You do not have permission to change this order.')
  }

  if (response.status === 404) {
    throw new Error('The order or a referenced product no longer exists.')
  }

  if (response.status === 409) {
    throw new OrderConflictError(
      action === 'confirm'
        ? 'Confirmation failed. Stock may be insufficient, or the order is no longer pending.'
        : 'Cancellation failed. The order is no longer pending.',
    )
  }

  if (!response.ok) {
    throw new Error('Unable to update the order.')
  }

  return response.json()
}

export interface CreateOrderInput {
  customerId: number
  items: {
    productId: number
    quantity: number
  }[]
}

export async function createOrder(
  token: string,
  input: CreateOrderInput,
): Promise<Order> {
  const response = await fetch('/api/orders', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(input),
  })

  if (response.status === 401) {
    throw new UnauthorizedError()
  }

  if (response.status === 403) {
    throw new Error('You do not have permission to create orders.')
  }

  if (response.status === 404) {
    throw new Error('A selected customer or product no longer exists.')
  }

  if (response.status === 400) {
    throw new Error('Check the customer, products, and quantities.')
  }

  if (!response.ok) {
    throw new Error(
      'The order outcome could not be confirmed. Check the order list before submitting again.',
    )
  }

  return response.json()
}
