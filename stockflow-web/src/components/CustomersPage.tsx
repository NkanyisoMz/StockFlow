import { useEffect, useState } from 'react'
import { getCustomers } from '../api/customers'
import type { Customer } from '../api/customers'
import { UnauthorizedError } from '../api/products'

interface CustomersPageProps {
  token: string
  onUnauthorized: () => void
}

export default function CustomersPage({
  token,
  onUnauthorized,
}: CustomersPageProps) {
  const [customers, setCustomers] = useState<Customer[] | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function loadCustomers() {
      try {
        const result = await getCustomers(token, controller.signal)

        if (!controller.signal.aborted) {
          setCustomers(result)
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
              : 'Unable to load customers.',
        )
      }
    }

    void loadCustomers()

    return () => controller.abort()
  }, [token, onUnauthorized])

  return (
    <div>
      <h2>Customers</h2>
      <p>Business customers whose orders are managed by your team.</p>

      {error ? (
        <p role="alert">{error}</p>
      ) : customers === null ? (
        <p role="status">Loading customers…</p>
      ) : customers.length === 0 ? (
        <p>No customers found.</p>
      ) : (
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Email</th>
                <th scope="col">Created</th>
              </tr>
            </thead>

            <tbody>
              {customers.map((customer) => (
                <tr key={customer.id}>
                  <td>{customer.name}</td>
                  <td>{customer.email}</td>
                  <td>
                    {new Date(customer.createdAt).toLocaleDateString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
