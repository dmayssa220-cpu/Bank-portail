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

export interface ChatTurn {
  role: 'user' | 'assistant'
  content: string
}

export const askChatbot = async (message: string, history: ChatTurn[]): Promise<string> => {
  const { data } = await apiClient.post<{ answer: string }>('/chat/ask', {
    message,
    history: history.map((h) => ({ role: h.role, content: h.content }))
  })
  return data.answer
}
