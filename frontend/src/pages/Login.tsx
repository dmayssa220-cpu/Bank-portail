import { useState, type FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { login, loginWithTwoFactor, type LoginResponse } from '../api/client'
import { useAuth } from '../auth/AuthContext'

export function Login() {
  const [email, setEmail] = useState('ahmed.bensalah@example.com')
  const [password, setPassword] = useState('')
  const [pendingToken, setPendingToken] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const auth = useAuth()
  const navigate = useNavigate()

  const finishLogin = (result: LoginResponse) => {
    auth.login(result.token!, result.customerId!, result.fullName!)
    navigate('/dashboard')
  }

  // Étape 1 : email + mot de passe
  const handleCredentials = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoading(true)

    try {
      const result = await login(email, password)

      if (result.requiresTwoFactor && result.pendingToken) {
        setPendingToken(result.pendingToken) // on passe à l'étape 2
      } else {
        finishLogin(result)
      }
    } catch {
      setError('Email ou mot de passe incorrect.')
    } finally {
      setLoading(false)
    }
  }

  // Étape 2 : code à 6 chiffres de l'application d'authentification
  const handleTwoFactor = async (e: FormEvent) => {
    e.preventDefault()
    if (!pendingToken) return
    setError(null)
    setLoading(true)

    try {
      const result = await loginWithTwoFactor(pendingToken, code.trim())
      finishLogin(result)
    } catch {
      setError('Code incorrect ou session expirée.')
    } finally {
      setLoading(false)
    }
  }

  const backToCredentials = () => {
    setPendingToken(null)
    setCode('')
    setError(null)
  }

  if (pendingToken) {
    return (
      <div className="auth-page">
        <form className="auth-card" onSubmit={handleTwoFactor}>
          <h1>🔐 Vérification</h1>
          <p className="auth-subtitle">
            Saisissez le code à 6 chiffres affiché dans votre application d'authentification.
          </p>

          <label>
            Code de vérification
            <input
              value={code}
              onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
              inputMode="numeric"
              autoComplete="one-time-code"
              placeholder="123456"
              autoFocus
              required
            />
          </label>

          {error && <p className="error">{error}</p>}

          <button type="submit" disabled={loading || code.length !== 6}>
            {loading ? 'Vérification...' : 'Valider'}
          </button>

          <p className="auth-footer">
            <a href="#" onClick={(e) => { e.preventDefault(); backToCredentials() }}>← Retour</a>
          </p>
        </form>
      </div>
    )
  }

  return (
    <div className="auth-page">
      <form className="auth-card" onSubmit={handleCredentials}>
        <h1>🏦 Ma Banque</h1>
        <p className="auth-subtitle">Connectez-vous à votre espace</p>

        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </label>

        <label>
          Mot de passe
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </label>

        {error && <p className="error">{error}</p>}

        <button type="submit" disabled={loading}>
          {loading ? 'Connexion...' : 'Se connecter'}
        </button>

        <p className="auth-footer">
          Pas encore de compte ? <Link to="/register">Créer un compte</Link>
        </p>

        <p className="auth-hint">
          Compte de démo : <code>ahmed.bensalah@example.com</code> / <code>password123</code>
        </p>
      </form>
    </div>
  )
}
