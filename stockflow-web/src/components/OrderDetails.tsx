import { useEffect, useState } from 'react'
import { getOrder, orderStatusLabel } from '../api/orders'
import type { Order } from '../api/orders'
import { UnauthorizedError } from '../api/products'

interface OrderDetailsProps {
  token: string
  orderId: number
  onBack: () => void
  onUnauthorized: () => void
}

export default function OrderDetails({
  token,
  orderId,
  onBack,
  onUnauthorized,
}: OrderDetailsProps) {
  const [order, setOrder] = useState<Order | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function loadOrder() {
      try {
        const result = await getOrder(
          token,
          orderId,
          controller.signal,
        )

        if (!controller.signal.aborted) {
          setOrder(result)
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
              : 'Unable to load the order.',
        )
      }
    }

    void loadOrder()

    return () => controller.abort()
  }, [token, orderId, onUnauthorized])

  return (
    <div>
      <button type="button" onClick={onBack}>
        Back to orders
      </button>

      {error ? (
        <p role="alert">{error}</p>
      ) : !order ? (
        <p role="status">Loading order…</p>
      ) : (
        <>
          <h2>Order #{order.id}</h2>

          <dl>
            <dt>Customer ID</dt>
            <dd>{order.customerId}</dd>

            <dt>Created</dt>
            <dd>{new Date(order.orderDate).toLocaleString()}</dd>

            <dt>Status</dt>
            <dd>{orderStatusLabel(order.status)}</dd>

            <dt>Total</dt>
            <dd>{order.totalAmount.toFixed(2)}</dd>
          </dl>

          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th scope="col">Product ID</th>
                  <th scope="col">Quantity</th>
                  <th scope="col">Captured unit price</th>
                  <th scope="col">Line total</th>
                </tr>
              </thead>

              <tbody>
                {order.items.map((item) => (
                  <tr key={item.id}>
                    <td>{item.productId}</td>
                    <td>{item.quantity}</td>
                    <td>{item.unitPrice.toFixed(2)}</td>
                    <td>{item.lineTotal.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p>
            Prices were captured when this order was created.
            Pending orders do not reserve stock.
          </p>
        </>
      )}
    </div>
  )
}
