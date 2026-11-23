export function hasRole(token: string, expectedRole: string): boolean {
  try {
    const payload = token.split('.')[1]

    if (!payload) {
      return false
    }

    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')
    const claims: unknown = JSON.parse(atob(padded))

    if (typeof claims !== 'object' || claims === null || !('role' in claims)) {
      return false
    }

    const roles = claims.role

    return typeof roles === 'string'
      ? roles === expectedRole
      : Array.isArray(roles) && roles.includes(expectedRole)
  } catch {
    return false
  }
}
