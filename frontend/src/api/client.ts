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

// Si le token est expiré/invalide, l'API renvoie 401 : on nettoie la session
// et on renvoie l'utilisateur vers le login.
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('bank_token')
      localStorage.removeItem('bank_customer_id')
      localStorage.removeItem('bank_full_name')
      if (window.location.pathname !== '/login') {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

// --- Types ---
export interface AccountDto {
  id: string
  iban: string
  balance: number
  currency: string
  customerName: string
}

export interface TransactionDto {
  id: string
  type: string
  amount: number
  label: string
  createdAt: string
}

export interface LoginResponse {
  token: string
  customerId: string
  fullName: string
  expiresAt: string
}

export interface FraudScoreResult {
  probability: number
  isSuspicious: boolean
  reasons: string[]
}

export interface TransferResult {
  transactionId: string
  newBalance: number
  fraudAlert: FraudScoreResult | null
}

// --- Authentification ---
export const login = async (email: string, password: string): Promise<LoginResponse> => {
  const { data } = await apiClient.post<LoginResponse>('/auth/login', { email, password })
  return data
}

export const register = async (fullName: string, email: string, password: string): Promise<LoginResponse> => {
  const { data } = await apiClient.post<LoginResponse>('/auth/register', { fullName, email, password })
  return data
}

// --- Comptes ---
export const getAccounts = async (): Promise<AccountDto[]> => {
  const { data } = await apiClient.get<AccountDto[]>('/accounts')
  return data
}

export const createAccount = async (currency: string): Promise<AccountDto> => {
  const { data } = await apiClient.post<AccountDto>('/accounts', { currency })
  return data
}

export const deleteAccount = async (accountId: string): Promise<void> => {
  await apiClient.delete(`/accounts/${accountId}`)
}

export const getAccountTransactions = async (accountId: string): Promise<TransactionDto[]> => {
  const { data } = await apiClient.get<TransactionDto[]>(`/accounts/${accountId}/transactions`)
  return data
}

// --- Virements ---
export const createTransfer = async (
  fromAccountId: string,
  toIban: string,
  amount: number,
  label?: string
): Promise<TransferResult> => {
  const { data } = await apiClient.post<TransferResult>('/transfers', {
    fromAccountId,
    toIban,
    amount,
    label
  })
  return data
}

// --- Chatbot ---
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
