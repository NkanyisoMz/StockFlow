import { useEffect, useState } from 'react'
import { getCustomers } from '../api/customers'
import type { Customer } from '../api/customers'
import { UnauthorizedError } from '../api/products'
import CustomerForm from './CustomerForm'

interface CustomersPageProps {
  token: string
  onUnauthorized: () => void
}

interface CustomerListProps extends CustomersPageProps {
  onEdit: (customer: Customer) => void
}

export default function CustomersPage({
  token,
  onUnauthorized,
}: CustomersPageProps) {
  const [formOpen, setFormOpen] = useState(false)
  const [selectedCustomer, setSelectedCustomer] =
    useState<Customer | undefined>(undefined)
  const [refreshVersion, setRefreshVersion] = useState(0)
  const [notice, setNotice] = useState('')

  if (formOpen) {
    return (
      <CustomerForm
        token={token}
        customer={selectedCustomer}
        onUnauthorized={onUnauthorized}
        onCancel={() => setFormOpen(false)}
        onSaved={() => {
          setFormOpen(false)
          setNotice('Customer saved.')
          setRefreshVersion((version) => version + 1)
        }}
      />
    )
  }

  return (
    <div>
      <button
        type="button"
        onClick={() => {
          setSelectedCustomer(undefined)
          setNotice('')
          setFormOpen(true)
        }}
      >
        Create customer
      </button>

      {notice && <p role="status">{notice}</p>}

      <CustomerList
        key={refreshVersion}
        token={token}
        onUnauthorized={onUnauthorized}
        onEdit={(customer) => {
          setSelectedCustomer(customer)
          setNotice('')
          setFormOpen(true)
        }}
      />
    </div>
  )
}

function CustomerList({
  token,
  onUnauthorized,
  onEdit,
}: CustomerListProps) {
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
                <th scope="col">ID</th>
                <th scope="col">Name</th>
                <th scope="col">Email</th>
                <th scope="col">Created</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>

            <tbody>
              {customers.map((customer) => (
                <tr key={customer.id}>
                  <td>{customer.id}</td>
                  <td>{customer.name}</td>
                  <td>{customer.email}</td>
                  <td>
                    {new Date(customer.createdAt).toLocaleDateString()}
                  </td>
                  <td>
                    <button type="button" onClick={() => onEdit(customer)}>
                        Edit
                    </button>
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
