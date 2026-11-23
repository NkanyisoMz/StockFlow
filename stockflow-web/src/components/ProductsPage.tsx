import { useEffect, useState } from 'react'
import type { PagedResult, Product } from '../api/products'
import CreateProductForm from './CreateProductForm'
import { hasRole } from '../auth/roles'
import EditProductForm from './EditProductForm'
import {
    deleteProduct,
    getProducts,
    UnauthorizedError,
} from '../api/products'

interface ProductsPageProps {
  token: string
  onUnauthorized: () => void
}

export default function ProductsPage({
  token,
  onUnauthorized,
}: ProductsPageProps) {
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [refreshVersion, setRefreshVersion] = useState(0)

  return (
    <div>
      <h2>Products</h2>

      {hasRole(token, 'Admin') && (
        <CreateProductForm
            token={token}
            onUnauthorized={onUnauthorized}
            onCreated={() => {
            setSearchInput('')
            setSearch('')
            setPage(1)
            setRefreshVersion((version) => version + 1)
            }}
        />
        )}

      <form
        className="search-form"
        onSubmit={(event) => {
          event.preventDefault()
          setSearch(searchInput.trim())
          setPage(1)
        }}
      >
        <label htmlFor="product-search">Search by product name</label>

        <div className="search-controls">
          <input
            id="product-search"
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
          />

          <button type="submit">Search</button>

          <button
            type="button"
            onClick={() => {
              setSearchInput('')
              setSearch('')
              setPage(1)
            }}
          >
            Clear
          </button>
        </div>
      </form>

      <ProductResults
        key={JSON.stringify([search, page, token, refreshVersion])}
        token={token}
        search={search}
        page={page}
        onPageChange={setPage}
        onUnauthorized={onUnauthorized}
        onChanged={() => {
            setPage(1)
            setRefreshVersion((version) => version + 1)}}
      />
    </div>
  )
}

interface ProductResultsProps extends ProductsPageProps {
  search: string
  page: number
  onPageChange: (page: number) => void
  onChanged: () => void
}

function ProductResults({
  token,
  search,
  page,
  onPageChange,
  onUnauthorized,
  onChanged,
}: ProductResultsProps) {
  const [result, setResult] = useState<PagedResult<Product> | null>(null)
  const [error, setError] = useState('')
  const [deleteError, setDeleteError] = useState('')
  const [deletingId, setDeletingId] = useState<number | null>(null)
  const [editingProduct, setEditingProduct] = useState<Product | null>(null)

    async function handleDelete(product: Product) {
    if (!window.confirm(`Delete "${product.name}"? This cannot be undone.`)) {
        return
    }

    setDeleteError('')
    setDeletingId(product.id)

    try {
        await deleteProduct(token, product.id)
    } catch (error) {
        if (error instanceof UnauthorizedError) {
        onUnauthorized()
        return
        }

        setDeleteError(
        error instanceof TypeError
            ? 'Cannot reach the API.'
            : error instanceof Error
            ? error.message
            : 'Unable to delete the product.',
        )

        return
    } finally {
        setDeletingId(null)
    }

    onChanged()
    }
  useEffect(() => {
    const controller = new AbortController()

    async function loadProducts() {
      try {
        const products = await getProducts(
          token,
          search,
          page,
          controller.signal,
        )

        if (!controller.signal.aborted) {
          setResult(products)
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
            ? 'Cannot reach the API. Check that the backend is running.'
            : error instanceof Error
              ? error.message
              : 'Unable to load products.',
        )
      }
    }

    void loadProducts()

    return () => controller.abort()
  }, [token, search, page, onUnauthorized])

  if (editingProduct) {
    return (
        <EditProductForm
        key={editingProduct.id}
        token={token}
        product={editingProduct}
        onUnauthorized={onUnauthorized}
        onCancel={() => setEditingProduct(null)}
        onSaved={() => {
            setEditingProduct(null)
            onChanged()
        }}
        />
    )
    }

  if (error) {
    return <p role="alert">{error}</p>
  }

  if (!result) {
    return <p role="status">Loading products…</p>
  }

  return (
    <div>
      <p role="status">{result.totalCount} matching products.</p>

      {deleteError && <p role="alert">{deleteError}</p>}
      {result.items.length === 0 ? (
        <p>No products found.</p>
      ) : (
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">SKU</th>
                <th scope="col">Price</th>
                <th scope="col">Stock</th>
                {hasRole(token, 'Admin') && (
                    <th scope="col">Actions</th>
                )}
              </tr>
            </thead>

            <tbody>
              {result.items.map((product) => (
                <tr key={product.id}>
                  <td>{product.name}</td>
                  <td>{product.sku}</td>
                  <td>{product.price.toFixed(2)}</td>
                  <td>{product.quantityInStock}</td>
                  <button
                        type="button"
                        disabled={deletingId !== null}
                        onClick={() => setEditingProduct(product)}
                        >
                        Edit
                  </button>
                  {hasRole(token, 'Admin') && (
                  <td>
                        <button
                        type="button"
                        disabled={deletingId !== null}
                        onClick={() => void handleDelete(product)}
                        >
                        {deletingId === product.id ? 'Deleting…' : 'Delete'}
                        </button>
                  </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <nav className="pagination" aria-label="Product pages">
        <button
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          Previous
        </button>

        <span>
          {result.totalPages === 0
            ? 'No pages'
            : `Page ${result.page} of ${result.totalPages}`}
        </span>

        <button
          disabled={page >= result.totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          Next
        </button>
      </nav>
    </div>
  )
}
