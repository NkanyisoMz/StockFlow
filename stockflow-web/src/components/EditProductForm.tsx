import { useState } from 'react'
import { UnauthorizedError, updateProduct } from '../api/products'
import type { Product } from '../api/products'

interface EditProductFormProps {
  token: string
  product: Product
  onSaved: () => void
  onCancel: () => void
  onUnauthorized: () => void
}

export default function EditProductForm({
  token,
  product,
  onSaved,
  onCancel,
  onUnauthorized,
}: EditProductFormProps) {
  const [name, setName] = useState(product.name)
  const [sku, setSku] = useState(product.sku)
  const [price, setPrice] = useState(String(product.price))
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSave() {
    setError('')

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
      await updateProduct(token, product.id, {
        name: name.trim(),
        sku: sku.trim(),
        price: amount,
      })
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
            : 'Unable to update the product.',
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
      <h3>Edit product</h3>
      <p>Stock changes must be made through inventory operations.</p>

      <fieldset disabled={isSubmitting}>
        <div>
          <label htmlFor="edit-product-name">Name</label>
          <input
            id="edit-product-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={100}
            required
          />
        </div>

        <div>
          <label htmlFor="edit-product-sku">SKU</label>
          <input
            id="edit-product-sku"
            value={sku}
            onChange={(event) => setSku(event.target.value)}
            maxLength={50}
            required
          />
        </div>

        <div>
          <label htmlFor="edit-product-price">Price</label>
          <input
            id="edit-product-price"
            type="number"
            min="0"
            step="0.01"
            value={price}
            onChange={(event) => setPrice(event.target.value)}
            required
          />
        </div>

        <button type="submit">
          {isSubmitting ? 'Saving…' : 'Save changes'}
        </button>

        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </fieldset>

      {error && <p role="alert">{error}</p>}
    </form>
  )
}
