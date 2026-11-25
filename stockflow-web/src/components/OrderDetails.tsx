import { useEffect, useState } from 'react'
import {
  changeOrderStatus,
  getOrder,
  OrderConflictError,
  orderStatusLabel,
} from '../api/orders'
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
  const [actionError, setActionError] = useState('')
  const [notice, setNotice] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [needsReload, setNeedsReload] = useState(false)

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

  async function handleAction(action: 'confirm' | 'cancel') {
    const message = action === 'confirm'
        ? 'Confirm this order? Stock will be deducted.'
        : 'Cancel this pending order?'

    if (!window.confirm(message)) {
        return
    }

    setActionError('')
    setNotice('')
    setIsSubmitting(true)

    try {
        const updatedOrder = await changeOrderStatus(token, orderId, action)

        setOrder(updatedOrder)
        setNotice(
        action === 'confirm'
            ? 'Order confirmed. Inventory has been updated.'
            : 'Order cancelled. Inventory was not changed.',
        )
    } catch (error) {
        if (error instanceof UnauthorizedError) {
        onUnauthorized()
        return
        }

        if (error instanceof OrderConflictError) {
        try {
            // Another request may have changed the order's saved status.
            const latestOrder = await getOrder(
            token,
            orderId,
            new AbortController().signal,
            )

            setOrder(latestOrder)
            setActionError(error.message)
        } catch (refreshError) {
            if (refreshError instanceof UnauthorizedError) {
            onUnauthorized()
            return
            }

            setNeedsReload(true)
            setActionError(
            `${error.message} Return to the list and reopen the order to refresh it.`,
            )
        }

        return
        }

        // The server may have completed the operation before connection loss.
        setNeedsReload(true)
        setActionError(
        error instanceof TypeError
            ? 'Connection lost. Return to the list and reopen the order to check its saved state before retrying.'
            : error instanceof Error
            ? error.message
            : 'Unable to update the order.',
        )
    } finally {
        setIsSubmitting(false)
    }
    }

  return (
    <div>
      <button type="button" disabled={isSubmitting} onClick={onBack}>
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

          {order.status === 0 && (
            <div className="order-actions">
                <button
                type="button"
                disabled={isSubmitting || needsReload}
                onClick={() => void handleAction('confirm')}
                >
                Confirm order
                </button>

                <button
                type="button"
                disabled={isSubmitting || needsReload}
                onClick={() => void handleAction('cancel')}
                >
                Cancel order
                </button>
            </div>
            )}

            {isSubmitting && <p role="status">Updating order…</p>}
            {notice && <p role="status">{notice}</p>}
            {actionError && <p role="alert">{actionError}</p>}

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
