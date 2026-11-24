import { useEffect, useState } from 'react'
import { getMovementHistory } from '../api/inventory'
import type { InventoryMovement } from '../api/inventory'
import { UnauthorizedError } from '../api/products'
import type { Product } from '../api/products'

interface MovementHistoryProps {
  token: string
  product: Product
  onBack: () => void
  onUnauthorized: () => void
}

export default function MovementHistory({
  token,
  product,
  onBack,
  onUnauthorized,
}: MovementHistoryProps) {
  const [movements, setMovements] =
    useState<InventoryMovement[] | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function loadHistory() {
      try {
        const result = await getMovementHistory(
          token,
          product.id,
          controller.signal,
        )

        if (!controller.signal.aborted) {
          setMovements(result)
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
              : 'Unable to load inventory history.',
        )
      }
    }

    void loadHistory()

    return () => controller.abort()
  }, [token, product.id, onUnauthorized])

  return (
    <div>
      <h3>Inventory history: {product.name}</h3>
      <p>Movements are listed newest first.</p>

      <button type="button" onClick={onBack}>
        Back to products
      </button>

      {error ? (
        <p role="alert">{error}</p>
      ) : movements === null ? (
        <p role="status">Loading history…</p>
      ) : movements.length === 0 ? (
        <p>No inventory movements recorded.</p>
      ) : (
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th scope="col">Date and time</th>
                <th scope="col">Type</th>
                <th scope="col">Quantity</th>
                <th scope="col">Reason</th>
              </tr>
            </thead>

            <tbody>
              {movements.map((movement) => (
                <tr key={movement.id}>
                  <td>
                    {new Date(movement.createdAt).toLocaleString()}
                  </td>
                  <td>
                    {movement.type === 0
                      ? 'Stock received'
                      : movement.type === 1
                        ? 'Stock issued'
                        : 'Unknown movement'}
                  </td>
                  <td>{movement.quantity}</td>
                  <td>{movement.reason}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
