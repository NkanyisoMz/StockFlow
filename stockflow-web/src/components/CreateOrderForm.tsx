import { useState } from 'react'
import { createOrder } from '../api/orders'
import { UnauthorizedError } from '../api/products'

interface CreateOrderFormProps {
  token: string
  onCreated: (orderId: number) => void
  onCancel: () => void
  onUnauthorized: () => void
}

interface ItemInput {
  key: number
  productId: string
  quantity: string
}

export default function CreateOrderForm({
  token,
  onCreated,
  onCancel,
  onUnauthorized,
}: CreateOrderFormProps) {
  const [customerId, setCustomerId] = useState('')
  const [items, setItems] = useState<ItemInput[]>([
    { key: 0, productId: '', quantity: '1' },
  ])
  const [nextKey, setNextKey] = useState(1)
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [outcomeUnknown, setOutcomeUnknown] = useState(false)

  function updateItem(
    key: number,
    field: 'productId' | 'quantity',
    value: string,
  ) {
    setItems((current) =>
      current.map((item) =>
        item.key === key ? { ...item, [field]: value } : item,
      ),
    )
  }

  async function handleCreate() {
    setError('')

    const customer = Number(customerId)
    const parsedItems = items.map((item) => ({
      productId: Number(item.productId),
      quantity: Number(item.quantity),
    }))

    const isValidInteger = (value: number) =>
      Number.isInteger(value) && value > 0 && value <= 2_147_483_647

    if (
      !isValidInteger(customer) ||
      parsedItems.length === 0 ||
      parsedItems.some(
        (item) =>
          !isValidInteger(item.productId) ||
          !isValidInteger(item.quantity),
      )
    ) {
      setError('Enter positive whole-number IDs and quantities.')
      return
    }

    if (
      new Set(parsedItems.map((item) => item.productId)).size !==
      parsedItems.length
    ) {
      setError('Use one line per product. Combine duplicate quantities.')
      return
    }

    setIsSubmitting(true)

    try {
      const order = await createOrder(token, {
        customerId: customer,
        items: parsedItems,
      })

      onCreated(order.id)
    } catch (error) {
      if (error instanceof UnauthorizedError) {
        onUnauthorized()
        return
      }

      if (error instanceof TypeError || error instanceof SyntaxError) {
        setOutcomeUnknown(true)
        setError(
          'The outcome is uncertain. Return to the order list and check for the order before creating another.',
        )
      } else {
        setError(
          error instanceof Error
            ? error.message
            : 'Unable to create the order.',
        )

        // Unexpected server failures may occur after a write.
        // Leave the form only through Cancel if its outcome is uncertain.
        if (
          error instanceof Error &&
          error.message.includes('outcome could not be confirmed')
        ) {
          setOutcomeUnknown(true)
        }
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        void handleCreate()
      }}
    >
      <h2>Create order</h2>
      <p>
        Creating an order captures prices but does not reserve stock.
        Stock is deducted during confirmation.
      </p>

      <fieldset disabled={isSubmitting || outcomeUnknown}>
        <div>
          <label htmlFor="order-customer">Customer ID</label>
          <input
            id="order-customer"
            type="number"
            min="1"
            max="2147483647"
            step="1"
            value={customerId}
            onChange={(event) => setCustomerId(event.target.value)}
            required
          />
        </div>

        {items.map((item) => (
          <div key={item.key}>
            <label htmlFor={`product-${item.key}`}>Product ID</label>
            <input
              id={`product-${item.key}`}
              type="number"
              min="1"
              max="2147483647"
              step="1"
              value={item.productId}
              onChange={(event) =>
                updateItem(item.key, 'productId', event.target.value)
              }
              required
            />

            <label htmlFor={`quantity-${item.key}`}>Quantity</label>
            <input
              id={`quantity-${item.key}`}
              type="number"
              min="1"
              max="2147483647"
              step="1"
              value={item.quantity}
              onChange={(event) =>
                updateItem(item.key, 'quantity', event.target.value)
              }
              required
            />

            <button
              type="button"
              disabled={items.length === 1}
              onClick={() =>
                setItems((current) =>
                  current.filter((entry) => entry.key !== item.key),
                )
              }
            >
              Remove line
            </button>
          </div>
        ))}

        <button
          type="button"
          onClick={() => {
            setItems((current) => [
              ...current,
              { key: nextKey, productId: '', quantity: '1' },
            ])
            setNextKey((key) => key + 1)
          }}
        >
          Add product line
        </button>

        <button type="submit">
          {isSubmitting ? 'Creating…' : 'Create pending order'}
        </button>
      </fieldset>

      <button type="button" disabled={isSubmitting} onClick={onCancel}>
        Back to orders
      </button>

      {error && <p role="alert">{error}</p>}
    </form>
  )
}
