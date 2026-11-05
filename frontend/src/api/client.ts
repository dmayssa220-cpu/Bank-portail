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
  token?: string | null
  customerId?: string | null
  fullName?: string | null
  expiresAt?: string | null
  requiresTwoFactor: boolean
  pendingToken?: string | null
}

export interface FraudScoreResult {
  probability: number
  isSuspicious: boolean
  reasons: string[]
}

export interface TransferResult {
  transactionId: string
  newBalance: number
  isInternal: boolean
  beneficiaryName: string | null
  replayed: boolean
  fraudAlert: FraudScoreResult | null
}

// --- Authentification ---
export const login = async (email: string, password: string): Promise<LoginResponse> => {
  const { data } = await apiClient.post<LoginResponse>('/auth/login', { email, password })
  return data
}

export const loginWithTwoFactor = async (pendingToken: string, code: string): Promise<LoginResponse> => {
  const { data } = await apiClient.post<LoginResponse>('/auth/login-2fa', { pendingToken, code })
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

export interface TransactionFilters {
  type?: string
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

// N'envoie pas les filtres vides au serveur
const cleanParams = (filters: TransactionFilters) =>
  Object.fromEntries(Object.entries(filters).filter(([, v]) => v !== '' && v !== undefined))

export const getAccount = async (accountId: string): Promise<AccountDto> => {
  const { data } = await apiClient.get<AccountDto>(`/accounts/${accountId}`)
  return data
}

export const getAccountTransactions = async (
  accountId: string,
  filters: TransactionFilters = {}
): Promise<PagedResult<TransactionDto>> => {
  const { data } = await apiClient.get<PagedResult<TransactionDto>>(`/accounts/${accountId}/transactions`, {
    params: cleanParams(filters)
  })
  return data
}

export const depositToAccount = async (accountId: string, amount: number, label?: string): Promise<number> => {
  const { data } = await apiClient.post<{ newBalance: number }>(`/accounts/${accountId}/deposit`, { amount, label })
  return data.newBalance
}

// Téléchargement du relevé CSV : on passe par axios (et non un simple lien) pour envoyer le jeton JWT
export const downloadTransactionsCsv = async (accountId: string, filters: TransactionFilters = {}): Promise<void> => {
  const { page, pageSize, ...exportFilters } = filters
  void page
  void pageSize

  const response = await apiClient.get(`/accounts/${accountId}/transactions/export`, {
    params: cleanParams(exportFilters),
    responseType: 'blob'
  })

  const disposition = response.headers['content-disposition'] as string | undefined
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i)
  const fileName = match ? decodeURIComponent(match[1]) : 'releve.csv'

  const url = URL.createObjectURL(response.data as Blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

// --- Virements ---
// Clé unique par tentative de virement : rejouer la même clé ne débite jamais deux fois
export const newIdempotencyKey = (): string =>
  typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`

export const createTransfer = async (
  fromAccountId: string,
  toIban: string,
  amount: number,
  label?: string,
  idempotencyKey?: string
): Promise<TransferResult> => {
  const { data } = await apiClient.post<TransferResult>(
    '/transfers',
    { fromAccountId, toIban, amount, label },
    idempotencyKey ? { headers: { 'Idempotency-Key': idempotencyKey } } : undefined
  )
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

// --- Crédit ---
export interface CreditSimulationResult {
  amount: number
  durationMonths: number
  annualRatePercent: number
  monthlyPayment: number
  totalCost: number
  totalInterest: number
}

export interface CreditRequestResult {
  id: string
  amount: number
  durationMonths: number
  monthlyPayment: number
  totalCost: number
  status: string
  createdAt: string
}

export const simulateCredit = async (amount: number, durationMonths: number): Promise<CreditSimulationResult> => {
  const { data } = await apiClient.post<CreditSimulationResult>('/credit/simulate', { amount, durationMonths })
  return data
}

export const applyForCredit = async (amount: number, durationMonths: number): Promise<CreditRequestResult> => {
  const { data } = await apiClient.post<CreditRequestResult>('/credit/apply', { amount, durationMonths })
  return data
}

export const getMyCreditRequests = async (): Promise<CreditRequestResult[]> => {
  const { data } = await apiClient.get<CreditRequestResult[]>('/credit/my-requests')
  return data
}

// --- Cartes ---
export interface CardDto {
  id: string
  cardNumberMasked: string
  cardHolderName: string
  expiryDate: string
  isBlocked: boolean
  dailyLimit: number
  accountIban: string
}

export const getCards = async (): Promise<CardDto[]> => {
  const { data } = await apiClient.get<CardDto[]>('/cards')
  return data
}

export const createCard = async (accountId: string): Promise<CardDto> => {
  const { data } = await apiClient.post<CardDto>('/cards', { accountId })
  return data
}

export const toggleCardBlock = async (cardId: string, block: boolean): Promise<CardDto> => {
  const { data } = await apiClient.patch<CardDto>(`/cards/${cardId}/${block ? 'block' : 'unblock'}`)
  return data
}

export const updateCardLimit = async (cardId: string, dailyLimit: number): Promise<CardDto> => {
  const { data } = await apiClient.patch<CardDto>(`/cards/${cardId}/limit`, { dailyLimit })
  return data
}

export const deleteCard = async (cardId: string): Promise<void> => {
  await apiClient.delete(`/cards/${cardId}`)
}

// --- Notifications ---
export interface NotificationDto {
  id: string
  title: string
  message: string
  type: 'Info' | 'Success' | 'Warning'
  isRead: boolean
  createdAt: string
}

export const getNotifications = async (): Promise<NotificationDto[]> => {
  const { data } = await apiClient.get<NotificationDto[]>('/notifications')
  return data
}

export const getUnreadCount = async (): Promise<number> => {
  const { data } = await apiClient.get<{ count: number }>('/notifications/unread-count')
  return data.count
}

export const markNotificationAsRead = async (id: string): Promise<void> => {
  await apiClient.patch(`/notifications/${id}/read`)
}

export const markAllNotificationsAsRead = async (): Promise<void> => {
  await apiClient.patch('/notifications/read-all')
}

// --- Historique des virements ---
export interface TransferHistoryItem {
  id: string
  type: 'TransferOut' | 'TransferIn' | string
  amount: number
  label: string
  createdAt: string
  accountIban: string
}

export const getTransferHistory = async (): Promise<TransferHistoryItem[]> => {
  const { data } = await apiClient.get<TransferHistoryItem[]>('/transfers')
  return data
}

// --- Profil & sécurité ---
export interface ProfileDto {
  fullName: string
  email: string
  twoFactorEnabled: boolean
  createdAt: string
}

export interface TwoFactorSetup {
  secret: string
  otpAuthUri: string
}

export const getProfile = async (): Promise<ProfileDto> => {
  const { data } = await apiClient.get<ProfileDto>('/profile')
  return data
}

export const updateProfile = async (fullName: string): Promise<ProfileDto> => {
  const { data } = await apiClient.put<ProfileDto>('/profile', { fullName })
  return data
}

export const changePassword = async (currentPassword: string, newPassword: string): Promise<void> => {
  await apiClient.put('/profile/password', { currentPassword, newPassword })
}

export const setupTwoFactor = async (): Promise<TwoFactorSetup> => {
  const { data } = await apiClient.post<TwoFactorSetup>('/profile/2fa/setup')
  return data
}

export const confirmTwoFactor = async (code: string): Promise<void> => {
  await apiClient.post('/profile/2fa/confirm', { code })
}

export const disableTwoFactor = async (password: string): Promise<void> => {
  await apiClient.post('/profile/2fa/disable', { password })
}

// Extrait le message d'erreur texte renvoyé par l'API (ex : "Solde insuffisant."), sinon un message par défaut
export const getApiErrorMessage = (err: unknown, fallback: string): string => {
  if (axios.isAxiosError(err) && typeof err.response?.data === 'string' && err.response.data.trim()) {
    return err.response.data
  }
  return fallback
}
