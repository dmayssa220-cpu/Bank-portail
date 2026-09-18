import { createContext, useContext, useState, type ReactNode } from 'react'

interface AuthState {
  token: string | null
  customerId: string | null
  fullName: string | null
}

interface AuthContextValue extends AuthState {
  isAuthenticated: boolean
  login: (token: string, customerId: string, fullName: string) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function loadInitialState(): AuthState {
  return {
    token: localStorage.getItem('bank_token'),
    customerId: localStorage.getItem('bank_customer_id'),
    fullName: localStorage.getItem('bank_full_name')
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(loadInitialState())

  const login = (token: string, customerId: string, fullName: string) => {
    localStorage.setItem('bank_token', token)
    localStorage.setItem('bank_customer_id', customerId)
    localStorage.setItem('bank_full_name', fullName)
    setState({ token, customerId, fullName })
  }

  const logout = () => {
    localStorage.removeItem('bank_token')
    localStorage.removeItem('bank_customer_id')
    localStorage.removeItem('bank_full_name')
    setState({ token: null, customerId: null, fullName: null })
  }

  return (
    <AuthContext.Provider value={{ ...state, isAuthenticated: !!state.token, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth doit être utilisé à l\'intérieur de <AuthProvider>')
  return ctx
}
