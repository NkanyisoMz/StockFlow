import { useState } from 'react'
import { saveCustomer } from '../api/customers'
import type { Customer } from '../api/customers'
import { UnauthorizedError } from '../api/products'

interface CustomerFormProps {
  token: string
  customer?: Customer
  onSaved: () => void
  onCancel: () => void
  onUnauthorized: () => void
}

export default function CustomerForm({
  token,
  customer,
  onSaved,
  onCancel,
  onUnauthorized,
}: CustomerFormProps) {
  const [name, setName] = useState(customer?.name ?? '')
  const [email, setEmail] = useState(customer?.email ?? '')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSave() {
    setError('')

    if (!name.trim() || !email.trim()) {
      setError('Enter a customer name and email address.')
      return
    }

    setIsSubmitting(true)

    try {
      await saveCustomer(
        token,
        {
          name: name.trim(),
          email: email.trim(),
        },
        customer?.id,
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
            : 'Unable to save the customer.',
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
      <h3>{customer ? 'Edit customer' : 'Create customer'}</h3>

      <fieldset disabled={isSubmitting}>
        <div>
          <label htmlFor="customer-name">Name</label>
          <input
            id="customer-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={100}
            required
          />
        </div>

        <div>
          <label htmlFor="customer-email">Email</label>
          <input
            id="customer-email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            maxLength={150}
            required
          />
        </div>

        <button type="submit">
          {isSubmitting ? 'Saving…' : 'Save customer'}
        </button>

        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </fieldset>

      {error && <p role="alert">{error}</p>}
    </form>
  )
}
