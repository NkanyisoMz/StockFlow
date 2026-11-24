import { useState } from 'react'
import { recordStockMovement } from '../api/inventory'
import type { StockOperation } from '../api/inventory'
import { UnauthorizedError } from '../api/products'
import type { Product } from '../api/products'

interface StockMovementFormProps {
  token: string
  product: Product
  onSaved: () => void
  onCancel: () => void
  onUnauthorized: () => void
}

export default function StockMovementForm({
  token,
  product,
  onSaved,
  onCancel,
  onUnauthorized,
}: StockMovementFormProps) {
  const [operation, setOperation] = useState<StockOperation>('stock-in')
  const [quantity, setQuantity] = useState('')
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSave() {
    setError('')

    const amount = Number(quantity)

    if (
      !Number.isInteger(amount) ||
      amount < 1 ||
      amount > 2_147_483_647 ||
      !reason.trim()
    ) {
      setError('Enter a positive whole-number quantity and a reason.')
      return
    }

    setIsSubmitting(true)

    try {
      await recordStockMovement(
        token,
        operation,
        product.id,
        amount,
        reason.trim(),
      )
    } catch (error) {
      if (error instanceof UnauthorizedError) {
        onUnauthorized()
        return
      }

      setError(
        error instanceof TypeError
          ? 'Cannot reach the API.'
          : error instanceof Error
            ? error.message
            : 'Unable to record the movement.',
      )

      return
    } finally {
      setIsSubmitting(false)
    }

    onSaved()
  }

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        void handleSave()
      }}
    >
      <h3>Manage stock: {product.name}</h3>
      <p>Stock when opened: {product.quantityInStock} units.</p>

      <fieldset disabled={isSubmitting}>
        <div>
          <label htmlFor="stock-operation">Operation</label>
          <select
            id="stock-operation"
            value={operation}
            onChange={(event) =>
              setOperation(
                event.target.value === 'stock-out'
                  ? 'stock-out'
                  : 'stock-in',
              )
            }
          >
            <option value="stock-in">Receive stock</option>
            <option value="stock-out">Issue stock</option>
          </select>
        </div>

        <div>
          <label htmlFor="stock-quantity">Quantity</label>
          <input
            id="stock-quantity"
            type="number"
            min="1"
            max="2147483647"
            step="1"
            value={quantity}
            onChange={(event) => setQuantity(event.target.value)}
            required
          />
        </div>

        <div>
          <label htmlFor="stock-reason">Reason</label>
          <input
            id="stock-reason"
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            maxLength={200}
            required
          />
        </div>

        <button type="submit">
          {isSubmitting ? 'Saving…' : 'Record movement'}
        </button>

        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </fieldset>

      {error && <p role="alert">{error}</p>}
    </form>
  )
}
