import { useState } from 'react'
import { createProduct, UnauthorizedError } from '../api/products'

interface CreateProductFormProps {
  token: string
  onCreated: () => void
  onUnauthorized: () => void
}

export default function CreateProductForm({
  token,
  onCreated,
  onUnauthorized,
}: CreateProductFormProps) {
  const [name, setName] = useState('')
  const [sku, setSku] = useState('')
  const [price, setPrice] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleCreate() {
    setError('')
    setNotice('')

    const amount = Number(price)

    if (
      !name.trim() ||
      !sku.trim() ||
      !price.trim() ||
      !Number.isFinite(amount) ||
      amount < 0
    ) {
      setError('Enter a name, SKU, and valid non-negative price.')
      return
    }

    setIsSubmitting(true)

    try {
      await createProduct(token, {
        name: name.trim(),
        sku: sku.trim(),
        price: amount,
      })

      setName('')
      setSku('')
      setPrice('')
      setNotice('Product created with zero stock.')
      onCreated()
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
            : 'Unable to create the product.',
      )
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
      <h3>Create product</h3>

      <fieldset disabled={isSubmitting}>
        <div>
          <label htmlFor="product-name">Name</label>
          <input
            id="product-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={100}
            required
          />
        </div>

        <div>
          <label htmlFor="product-sku">SKU</label>
          <input
            id="product-sku"
            value={sku}
            onChange={(event) => setSku(event.target.value)}
            maxLength={50}
            required
          />
        </div>

        <div>
          <label htmlFor="product-price">Price</label>
          <input
            id="product-price"
            type="number"
            min="0"
            step="0.01"
            value={price}
            onChange={(event) => setPrice(event.target.value)}
            required
          />
        </div>

        <button type="submit">
          {isSubmitting ? 'Creating…' : 'Create product'}
        </button>
      </fieldset>

      {error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
    </form>
  )
}
