export interface Product {
  id: number
  name: string
  sku: string
  price: number
  quantityInStock: number
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export class UnauthorizedError extends Error {
  constructor() {
    super('Your session has expired. Please sign in again.')
    this.name = 'UnauthorizedError'
  }
}

export async function getProducts(
  token: string,
  search: string,
  page: number,
  signal: AbortSignal,
): Promise<PagedResult<Product>> {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: '10',
  })

  if (search.trim()) {
    query.set('search', search.trim())
  }

  const response = await fetch(`/api/products?${query}`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
    signal,
  })

  if (response.status === 401) {
    throw new UnauthorizedError()
  }

  if (response.status === 403) {
    throw new Error('You do not have permission to view products.')
  }

  if (!response.ok) {
    throw new Error('Unable to load products.')
  }

  return response.json()
}

export interface CreateProductRequest {
  name: string
  sku: string
  price: number
}

export async function createProduct(
  token: string,
  product: CreateProductRequest,
): Promise<void> {
  const response = await fetch('/api/products', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(product),
  })

  if (response.status === 401) {
    throw new UnauthorizedError()
  }

  if (response.status === 403) {
    throw new Error('Only administrators can create products.')
  }

  if (response.status === 409) {
    throw new Error('A product with this SKU already exists.')
  }

  if (response.status === 400) {
    throw new Error(
      'Check the product details. Price must be non-negative and have at most two decimal places.',
    )
  }

  if (!response.ok) {
    throw new Error('Unable to create the product.')
  }
}
