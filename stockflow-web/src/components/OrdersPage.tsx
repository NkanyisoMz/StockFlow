import { useEffect, useState } from 'react'
import { getOrders, orderStatusLabel } from '../api/orders'
import type { Order } from '../api/orders'
import { UnauthorizedError } from '../api/products'
import type { PagedResult } from '../api/products'

interface OrdersPageProps {
  token: string
  onUnauthorized: () => void
}

export default function OrdersPage({
  token,
  onUnauthorized,
}: OrdersPageProps) {
  const [page, setPage] = useState(1)

  return (
    <div>
      <h2>Orders</h2>
      <p>Customer orders managed by your team.</p>

      <OrderResults
        key={JSON.stringify([page, token])}
        token={token}
        page={page}
        onPageChange={setPage}
        onUnauthorized={onUnauthorized}
      />
    </div>
  )
}

interface OrderResultsProps extends OrdersPageProps {
  page: number
  onPageChange: (page: number) => void
}

function OrderResults({
  token,
  page,
  onPageChange,
  onUnauthorized,
}: OrderResultsProps) {
  const [result, setResult] = useState<PagedResult<Order> | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function loadOrders() {
      try {
        const orders = await getOrders(token, page, controller.signal)

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
  }, [token, page, onUnauthorized])

  if (error) {
    return <p role="alert">{error}</p>
  }

  if (!result) {
    return <p role="status">Loading orders…</p>
  }

  return (
    <div>
      <p role="status">{result.totalCount} orders in total.</p>

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
                  <td>#{order.id}</td>
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
