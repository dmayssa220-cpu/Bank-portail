import { useEffect, useRef, useState } from 'react'
import {
  getNotifications,
  getUnreadCount,
  markNotificationAsRead,
  markAllNotificationsAsRead,
  type NotificationDto
} from '../api/client'

const TYPE_ICONS: Record<string, string> = {
  Info: 'ℹ️',
  Success: '✅',
  Warning: '⚠️'
}

function timeAgo(dateStr: string): string {
  const diffMs = Date.now() - new Date(dateStr).getTime()
  const minutes = Math.floor(diffMs / 60000)
  if (minutes < 1) return "à l'instant"
  if (minutes < 60) return `il y a ${minutes} min`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `il y a ${hours} h`
  return `il y a ${Math.floor(hours / 24)} j`
}

export function NotificationBell() {
  const [open, setOpen] = useState(false)
  const [notifications, setNotifications] = useState<NotificationDto[]>([])
  const [unreadCount, setUnreadCount] = useState(0)
  const ref = useRef<HTMLDivElement>(null)

  const refreshCount = () => {
    getUnreadCount().then(setUnreadCount).catch(() => {})
  }

  useEffect(() => {
    refreshCount()
    const interval = setInterval(refreshCount, 20000) // poll léger toutes les 20s
    return () => clearInterval(interval)
  }, [])

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const toggleOpen = async () => {
    const next = !open
    setOpen(next)
    if (next) {
      const data = await getNotifications()
      setNotifications(data)
    }
  }

  const handleItemClick = async (n: NotificationDto) => {
    if (!n.isRead) {
      await markNotificationAsRead(n.id)
      setNotifications((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)))
      setUnreadCount((c) => Math.max(0, c - 1))
    }
  }

  const handleMarkAllRead = async () => {
    await markAllNotificationsAsRead()
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })))
    setUnreadCount(0)
  }

  return (
    <div className="notification-bell" ref={ref}>
      <button className="notification-bell__toggle" onClick={toggleOpen} aria-label="Notifications">
        🔔
        {unreadCount > 0 && <span className="notification-bell__badge">{unreadCount > 9 ? '9+' : unreadCount}</span>}
      </button>

      {open && (
        <div className="notification-panel">
          <div className="notification-panel__header">
            <span>Notifications</span>
            {unreadCount > 0 && (
              <button onClick={handleMarkAllRead} className="notification-panel__mark-all">
                Tout marquer comme lu
              </button>
            )}
          </div>
          <div className="notification-panel__list">
            {notifications.length === 0 && <p className="notification-panel__empty">Aucune notification.</p>}
            {notifications.map((n) => (
              <div
                key={n.id}
                className={`notification-item ${n.isRead ? '' : 'notification-item--unread'}`}
                onClick={() => handleItemClick(n)}
              >
                <div className="notification-item__title">
                  <span>{TYPE_ICONS[n.type]} {n.title}</span>
                  <span className="notification-item__time">{timeAgo(n.createdAt)}</span>
                </div>
                <p className="notification-item__message">{n.message}</p>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
