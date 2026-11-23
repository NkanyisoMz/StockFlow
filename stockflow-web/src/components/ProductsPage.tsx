import { useEffect, useState } from 'react'
import { getProducts, UnauthorizedError } from '../api/products'
import type { PagedResult, Product } from '../api/products'

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

  return (
    <div>
      <h2>Products</h2>

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
        key={JSON.stringify([search, page, token])}
        token={token}
        search={search}
        page={page}
        onPageChange={setPage}
        onUnauthorized={onUnauthorized}
      />
    </div>
  )
}

interface ProductResultsProps extends ProductsPageProps {
  search: string
  page: number
  onPageChange: (page: number) => void
}

function ProductResults({
  token,
  search,
  page,
  onPageChange,
  onUnauthorized,
}: ProductResultsProps) {
  const [result, setResult] = useState<PagedResult<Product> | null>(null)
  const [error, setError] = useState('')

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

  if (error) {
    return <p role="alert">{error}</p>
  }

  if (!result) {
    return <p role="status">Loading products…</p>
  }

  return (
    <div>
      <p role="status">{result.totalCount} matching products.</p>

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
              </tr>
            </thead>

            <tbody>
              {result.items.map((product) => (
                <tr key={product.id}>
                  <td>{product.name}</td>
                  <td>{product.sku}</td>
                  <td>{product.price.toFixed(2)}</td>
                  <td>{product.quantityInStock}</td>
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
