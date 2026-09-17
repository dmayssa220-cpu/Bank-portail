import axios from 'axios'

// Client HTTP centralisé. En production, on passe par le même domaine (/api),
// géré par le reverse proxy Nginx du conteneur frontend.
export const apiClient = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json'
  }
})

// Injection automatique du token JWT stocké après login
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('bank_token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

export interface AccountDto {
  id: string
  iban: string
  balance: number
  currency: string
  customerName: string
}

export const getAccounts = async (): Promise<AccountDto[]> => {
  const { data } = await apiClient.get<AccountDto[]>('/accounts')
  return data
}

export const login = async (email: string, password: string) => {
  const { data } = await apiClient.post('/auth/login', { email, password })
  return data
}
