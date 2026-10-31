import { useEffect, useState, type FormEvent } from 'react'
import QRCode from 'qrcode'
import axios from 'axios'
import {
  getProfile,
  updateProfile,
  changePassword,
  setupTwoFactor,
  confirmTwoFactor,
  disableTwoFactor,
  type ProfileDto,
  type TwoFactorSetup
} from '../api/client'

// Extrait le message d'erreur renvoyé par l'API (texte brut) ou un message par défaut
function apiError(err: unknown, fallback: string): string {
  if (axios.isAxiosError(err) && typeof err.response?.data === 'string' && err.response.data) {
    return err.response.data
  }
  return fallback
}

export function Profile() {
  const [profile, setProfile] = useState<ProfileDto | null>(null)
  const [loading, setLoading] = useState(true)

  // --- Informations personnelles ---
  const [fullName, setFullName] = useState('')
  const [nameMsg, setNameMsg] = useState<{ ok: boolean; text: string } | null>(null)

  // --- Mot de passe ---
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [pwdMsg, setPwdMsg] = useState<{ ok: boolean; text: string } | null>(null)

  // --- 2FA ---
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null)
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null)
  const [twoFaCode, setTwoFaCode] = useState('')
  const [disablePassword, setDisablePassword] = useState('')
  const [showDisable, setShowDisable] = useState(false)
  const [twoFaMsg, setTwoFaMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const load = async () => {
    const data = await getProfile()
    setProfile(data)
    setFullName(data.fullName)
  }

  useEffect(() => {
    load().finally(() => setLoading(false))
  }, [])

  const handleSaveName = async (e: FormEvent) => {
    e.preventDefault()
    setNameMsg(null)
    try {
      const updated = await updateProfile(fullName)
      setProfile(updated)
      localStorage.setItem('bank_full_name', updated.fullName)
      setNameMsg({ ok: true, text: 'Nom mis à jour.' })
    } catch (err) {
      setNameMsg({ ok: false, text: apiError(err, 'Mise à jour impossible.') })
    }
  }

  const handleChangePassword = async (e: FormEvent) => {
    e.preventDefault()
    setPwdMsg(null)

    if (newPassword !== confirmPassword) {
      setPwdMsg({ ok: false, text: 'Les deux nouveaux mots de passe ne correspondent pas.' })
      return
    }

    try {
      await changePassword(currentPassword, newPassword)
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
      setPwdMsg({ ok: true, text: 'Mot de passe modifié avec succès.' })
    } catch (err) {
      setPwdMsg({ ok: false, text: apiError(err, 'Modification impossible.') })
    }
  }

  const handleStartSetup = async () => {
    setTwoFaMsg(null)
    try {
      const data = await setupTwoFactor()
      setSetup(data)
      // Le QR code est généré localement dans le navigateur : le secret ne transite par aucun service tiers
      setQrDataUrl(await QRCode.toDataURL(data.otpAuthUri, { width: 200, margin: 1 }))
    } catch (err) {
      setTwoFaMsg({ ok: false, text: apiError(err, "Impossible de démarrer l'activation.") })
    }
  }

  const handleConfirm = async (e: FormEvent) => {
    e.preventDefault()
    setTwoFaMsg(null)
    try {
      await confirmTwoFactor(twoFaCode.trim())
      setSetup(null)
      setQrDataUrl(null)
      setTwoFaCode('')
      await load()
      setTwoFaMsg({ ok: true, text: '✅ Double authentification activée.' })
    } catch (err) {
      setTwoFaMsg({ ok: false, text: apiError(err, 'Code incorrect.') })
    }
  }

  const handleDisable = async (e: FormEvent) => {
    e.preventDefault()
    setTwoFaMsg(null)
    try {
      await disableTwoFactor(disablePassword)
      setDisablePassword('')
      setShowDisable(false)
      await load()
      setTwoFaMsg({ ok: true, text: 'Double authentification désactivée.' })
    } catch (err) {
      setTwoFaMsg({ ok: false, text: apiError(err, 'Désactivation impossible.') })
    }
  }

  if (loading) return <main className="dashboard"><div className="spinner" /></main>
  if (!profile) return <main className="dashboard"><p className="error">Profil introuvable.</p></main>

  return (
    <main className="dashboard">
      <h1>Mon profil</h1>
      <p>Membre depuis le {new Date(profile.createdAt).toLocaleDateString('fr-FR')}</p>

      {/* ---------- Informations personnelles ---------- */}
      <div className="transfer-form-card">
        <h2>Informations personnelles</h2>
        <form className="transfer-form" onSubmit={handleSaveName}>
          <label>
            Email (non modifiable)
            <input value={profile.email} disabled />
          </label>
          <label>
            Nom complet
            <input value={fullName} onChange={(e) => setFullName(e.target.value)} required />
          </label>
          {nameMsg && <p className={nameMsg.ok ? 'success' : 'error'}>{nameMsg.text}</p>}
          <button type="submit">Enregistrer</button>
        </form>
      </div>

      {/* ---------- Mot de passe ---------- */}
      <div className="transfer-form-card">
        <h2>Changer le mot de passe</h2>
        <form className="transfer-form" onSubmit={handleChangePassword}>
          <label>
            Mot de passe actuel
            <input type="password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} required />
          </label>
          <label>
            Nouveau mot de passe
            <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} minLength={6} required />
          </label>
          <label>
            Confirmer le nouveau mot de passe
            <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} minLength={6} required />
          </label>
          {pwdMsg && <p className={pwdMsg.ok ? 'success' : 'error'}>{pwdMsg.text}</p>}
          <button type="submit">Modifier le mot de passe</button>
        </form>
      </div>

      {/* ---------- Double authentification ---------- */}
      <div className="transfer-form-card">
        <h2>🔐 Double authentification (2FA)</h2>
        <p className="security-status">
          Statut :{' '}
          <strong className={profile.twoFactorEnabled ? 'success' : 'error'}>
            {profile.twoFactorEnabled ? 'Activée' : 'Désactivée'}
          </strong>
        </p>
        <p className="security-help">
          Ajoute une seconde étape à la connexion : un code à 6 chiffres, renouvelé toutes les 30 secondes, généré par
          une application gratuite (Google Authenticator, Microsoft Authenticator, Authy…). Même si votre mot de passe
          est volé, personne ne peut se connecter sans votre téléphone.
        </p>

        {twoFaMsg && <p className={twoFaMsg.ok ? 'success' : 'error'}>{twoFaMsg.text}</p>}

        {/* Désactivée, pas de configuration en cours */}
        {!profile.twoFactorEnabled && !setup && (
          <button className="primary-btn" onClick={handleStartSetup}>Activer la double authentification</button>
        )}

        {/* Configuration en cours : QR code + confirmation */}
        {!profile.twoFactorEnabled && setup && (
          <div className="twofa-setup">
            <ol className="security-steps">
              <li>Installez une application d'authentification sur votre téléphone.</li>
              <li>Scannez ce QR code avec l'application :</li>
            </ol>
            {qrDataUrl && <img className="twofa-qr" src={qrDataUrl} alt="QR code d'activation 2FA" />}
            <p className="security-help">
              Impossible de scanner ? Saisissez cette clé manuellement : <code>{setup.secret}</code>
            </p>
            <form className="transfer-form" onSubmit={handleConfirm}>
              <label>
                3. Entrez le code à 6 chiffres affiché
                <input
                  value={twoFaCode}
                  onChange={(e) => setTwoFaCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  inputMode="numeric"
                  placeholder="123456"
                  required
                />
              </label>
              <button type="submit" disabled={twoFaCode.length !== 6}>Confirmer et activer</button>
            </form>
          </div>
        )}

        {/* Activée : possibilité de désactiver (mot de passe exigé) */}
        {profile.twoFactorEnabled && !showDisable && (
          <button className="danger-btn" onClick={() => setShowDisable(true)}>Désactiver la double authentification</button>
        )}

        {profile.twoFactorEnabled && showDisable && (
          <form className="transfer-form" onSubmit={handleDisable}>
            <label>
              Confirmez avec votre mot de passe
              <input type="password" value={disablePassword} onChange={(e) => setDisablePassword(e.target.value)} required />
            </label>
            <button type="submit" className="danger-btn">Confirmer la désactivation</button>
          </form>
        )}
      </div>
    </main>
  )
}
