import { Routes, Route, Navigate, Link, useLocation } from 'react-router-dom'
import { Dashboard } from './pages/Dashboard'
import { Login } from './pages/Login'
import { Register } from './pages/Register'
import { Credit } from './pages/Credit'
import { Cards } from './pages/Cards'
import { ChatWidget } from './components/ChatWidget'
import { NotificationBell } from './components/NotificationBell'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { useAuth } from './auth/AuthContext'
import './App.css'

function NavLinks() {
  const location = useLocation()
  const isActive = (path: string) => (location.pathname === path ? 'nav-link nav-link--active' : 'nav-link')

  return (
    <nav className="app-nav">
      <Link className={isActive('/dashboard')} to="/dashboard">Comptes</Link>
      <Link className={isActive('/credit')} to="/credit">Crédit</Link>
      <Link className={isActive('/cards')} to="/cards">Cartes</Link>
    </nav>
  )
}

function App() {
  const { isAuthenticated } = useAuth()

  return (
    <div className="app">
      <header className="app-header">
        <span className="app-logo">🏦 Ma Banque</span>
        {isAuthenticated && (
          <div className="app-header__right">
            <NavLinks />
            <NotificationBell />
          </div>
        )}
      </header>

      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute>
              <Dashboard />
            </ProtectedRoute>
          }
        />
        <Route
          path="/credit"
          element={
            <ProtectedRoute>
              <Credit />
            </ProtectedRoute>
          }
        />
        <Route
          path="/cards"
          element={
            <ProtectedRoute>
              <Cards />
            </ProtectedRoute>
          }
        />
        <Route path="/" element={<Navigate to={isAuthenticated ? '/dashboard' : '/login'} replace />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>

      {isAuthenticated && <ChatWidget />}
    </div>
  )
}

export default App
