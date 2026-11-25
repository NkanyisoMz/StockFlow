import { useEffect, useState } from 'react'
import { getOrders, orderStatusLabel } from '../api/orders'
import type { Order } from '../api/orders'
import { UnauthorizedError } from '../api/products'
import type { PagedResult } from '../api/products'
import type { OrderFilters } from '../api/orders'
import OrderDetails from './OrderDetails'
import CreateOrderForm from './CreateOrderForm'

interface OrdersPageProps {
  token: string
  onUnauthorized: () => void
}

export default function OrdersPage({
  token,
  onUnauthorized,
}: OrdersPageProps) {
  const [page, setPage] = useState(1)
  const [customerInput, setCustomerInput] = useState('')
  const [statusInput, setStatusInput] = useState('')
  const [filters, setFilters] = useState<OrderFilters>({})
  const [filterError, setFilterError] = useState('')
  const [selectedOrderId, setSelectedOrderId] =
  useState<number | null>(null)
  const [creatingOrder, setCreatingOrder] = useState(false)

  if (creatingOrder) {
    return (
      <CreateOrderForm
        token={token}
        onUnauthorized={onUnauthorized}
        onCancel={() => setCreatingOrder(false)}
        onCreated={(orderId) => {
          setCreatingOrder(false)
          setSelectedOrderId(orderId)
        }}
      />
    )
  }

  if (selectedOrderId !== null) {
    return (
      <OrderDetails
        key={selectedOrderId}
        token={token}
        orderId={selectedOrderId}
        onUnauthorized={onUnauthorized}
        onBack={() => setSelectedOrderId(null)}
      />
    )
  }

  return (
    <div>
      <h2>Orders</h2>
      <button type="button" onClick={() => setCreatingOrder(true)}>
        Create order
      </button>
      <p>Customer orders managed by your team.</p>

      <form
        className="search-form"
        onSubmit={(event) => {
            event.preventDefault()
            setFilterError('')

            const customerId = customerInput.trim()
            ? Number(customerInput)
            : undefined

            if (
            customerId !== undefined &&
            (!Number.isInteger(customerId) ||
                customerId < 1 ||
                customerId > 2_147_483_647)
            ) {
            setFilterError('Enter a valid positive customer ID.')
            return
            }

            setFilters({
            customerId,
            status: statusInput === '' ? undefined : Number(statusInput),
            })

            setPage(1)
        }}
        >
        <div>
            <label htmlFor="order-customer-filter">Customer ID</label>
            <input
            id="order-customer-filter"
            type="number"
            min="1"
            max="2147483647"
            step="1"
            value={customerInput}
            onChange={(event) => setCustomerInput(event.target.value)}
            placeholder="All customers"
            />
        </div>

        <div>
            <label htmlFor="order-status-filter">Status</label>
            <select
            id="order-status-filter"
            value={statusInput}
            onChange={(event) => setStatusInput(event.target.value)}
            >
            <option value="">All statuses</option>
            <option value="0">Pending</option>
            <option value="1">Confirmed</option>
            <option value="2">Cancelled</option>
            </select>
        </div>

        <button type="submit">Apply filters</button>

        <button
            type="button"
            onClick={() => {
            setCustomerInput('')
            setStatusInput('')
            setFilters({})
            setFilterError('')
            setPage(1)
            }}
        >
            Clear
        </button>

        {filterError && <p role="alert">{filterError}</p>}
        </form>

      <OrderResults
        key={JSON.stringify([page, token, filters.customerId, filters.status])}
        token={token}
        page={page}
        filters={filters}
        onPageChange={setPage}
        onUnauthorized={onUnauthorized}
        onSelectOrder={setSelectedOrderId}
      />
    </div>
  )
}

interface OrderResultsProps extends OrdersPageProps {
  page: number
  filters: OrderFilters
  onPageChange: (page: number) => void
  onSelectOrder: (orderId: number) => void
}

function OrderResults({
  token,
  page,
  filters,
  onPageChange,
  onUnauthorized,
  onSelectOrder,
}: OrderResultsProps) {
  const [result, setResult] = useState<PagedResult<Order> | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function loadOrders() {
      try {
        const orders = await getOrders(
            token,
            page,
            filters,
            controller.signal,
            )

        if (!controller.signal.aborted) {
          setResult(orders)
        }
      } catch (error) {
        if (controller.signal.aborted) {
          return
        }

        if (error instanceof UnauthorizedError) {
          onUnauthorized()
          return
        }

        setError(
          error instanceof TypeError
            ? 'Cannot reach the API.'
            : error instanceof Error
              ? error.message
              : 'Unable to load orders.',
        )
      }
    }

    void loadOrders()

    return () => controller.abort()
  }, [token, page, filters, onUnauthorized])

  if (error) {
    return <p role="alert">{error}</p>
  }

  if (!result) {
    return <p role="status">Loading orders…</p>
  }

  return (
    <div>
      <p role="status">{result.totalCount} matching orders.</p>

      {result.items.length === 0 ? (
        <p>No orders found.</p>
      ) : (
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th scope="col">Order</th>
                <th scope="col">Customer ID</th>
                <th scope="col">Created</th>
                <th scope="col">Status</th>
                <th scope="col">Total</th>
              </tr>
            </thead>

            <tbody>
              {result.items.map((order) => (
                <tr key={order.id}>
                  <td>
                    <button
                      type="button"
                      onClick={() => onSelectOrder(order.id)}
                    >
                      View #{order.id}
                    </button>
                  </td>
                  <td>{order.customerId}</td>
                  <td>{new Date(order.orderDate).toLocaleString()}</td>
                  <td>{orderStatusLabel(order.status)}</td>
                  <td>{order.totalAmount.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <nav className="pagination" aria-label="Order pages">
        <button
          type="button"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          Previous
        </button>

        <span>
          {result.totalPages === 0
            ? 'No pages'
            : `Page ${result.page} of ${result.totalPages}`}
        </span>

        <button
          type="button"
          disabled={page >= result.totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          Next
        </button>
      </nav>
    </div>
  )
}
