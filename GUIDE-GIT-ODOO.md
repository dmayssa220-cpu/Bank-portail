# Git (push initial) + Intégration Odoo Community en Docker

---

## Partie 1 — Pousser le projet sur GitHub (Bank-Portail)

### 1.1 — Le `.gitignore`
Un fichier `.gitignore` a été ajouté à la racine du projet (`bank-app/.gitignore`). Il exclut :
- `.env` (vos secrets — **ne doit jamais être poussé**)
- `bin/`, `obj/` (fichiers compilés .NET, régénérés à chaque build)
- `node_modules/`, `dist/` (dépendances et build React, régénérés à chaque `npm install`/`build`)
- Fichiers d'éditeur/OS (`.vscode/`, `.idea/`, `.DS_Store`, etc.)
- Dossiers de volumes Docker si vous en créez en local (`pgdata/`, `odoo-data/`)

Copiez-le à la racine de votre dossier `bank-app/` s'il n'y est pas déjà (il est inclus dans l'archive livrée).

### 1.2 — Étapes pour initialiser et pousser

```bash
cd bank-app

# 1. Initialiser le dépôt local
git init

# 2. Vérifier que .env n'apparaît PAS dans la liste (sinon vérifiez le .gitignore)
git status

# 3. Ajouter tous les fichiers suivis
git add .

# 4. Premier commit
git commit -m "Initial commit: squelette .NET + React + PostgreSQL + Docker"

# 5. Renommer la branche par défaut en 'main' (si ce n'est pas déjà le cas)
git branch -M main

# 6. Lier votre dépôt local au dépôt distant GitHub
git remote add origin https://github.com/<votre-utilisateur>/Bank-portail.git

# 7. Pousser
git push -u origin main
```

> Remplacez `<votre-utilisateur>` par votre nom d'utilisateur GitHub. Si le dépôt distant a déjà un README/licence créé depuis GitHub, faites `git pull origin main --allow-unrelated-histories` avant l'étape 7 pour fusionner.

### 1.3 — Stratégie de branches (adaptée à un solo)

Pas besoin d'un Git Flow complet (inutilement lourd seul). Structure simple recommandée :

| Branche | Rôle |
|---|---|
| `main` | Toujours stable et fonctionnelle (ce qui tourne avec `docker compose up`) |
| `develop` (optionnelle) | Intégration des features avant de les fusionner dans `main` — utile si vous voulez garder `main` "démo-ready" en permanence |
| `feature/xxx` | Une branche par fonctionnalité (ex : `feature/odoo-integration`, `feature/ollama-chatbot`) |

Commandes pour créer votre prochaine branche de travail (celle qu'on va utiliser pour l'intégration Odoo) :

```bash
git checkout -b feature/odoo-integration
# ... vous développez, committez ...
git add .
git commit -m "Ajout intégration Odoo (JSON-RPC)"
git push -u origin feature/odoo-integration
# Puis ouvrez une Pull Request sur GitHub vers main pour garder un historique propre
# Sur un projet solo, vous pouvez aussi merger directement :
git checkout main
git merge feature/odoo-integration
git push
```

Pour un simple portfolio solo, même un unique commit direct sur `main` à chaque étape est acceptable — l'important est d'avoir un historique de commits clair (`git log --oneline`) qui montre la progression du projet.

---

## Partie 2 — Intégration Odoo Community en Docker

### Ce qui a été ajouté au projet (déjà dans l'archive mise à jour)
- Un service **`odoo-db`** (PostgreSQL dédié à Odoo — Odoo a besoin d'un utilisateur avec droit `CREATEDB`, on ne réutilise donc pas la base bancaire).
- Un service **`odoo`** (image officielle `odoo:17.0`).
- Un client **`OdooClient.cs`** dans le backend .NET qui parle le protocole **JSON-RPC** d'Odoo.
- Un contrôleur **`OdooSyncController`** avec deux endpoints de test.

### Étape 1 — Démarrer Odoo
```bash
docker compose up --build odoo-db odoo
```
(ou simplement `docker compose up --build` pour tout relancer, Odoo s'ajoutera aux 3 services existants)

Attendez de voir dans les logs une ligne du type `odoo.modules.loading: Modules loaded.`, puis ouvrez :
```
http://localhost:8069
```

### Étape 2 — Assistant de création de base de données Odoo
Au premier accès, Odoo affiche un formulaire de création de base :

| Champ | Valeur à saisir |
|---|---|
| Nom de la base de données | `bankportal` (doit correspondre à `ODOO_DATABASE` dans votre `.env`) |
| Email | n'importe quel email (ex : `admin@bankportail.local`) |
| Mot de passe | choisissez-en un, ce sera votre mot de passe **admin** Odoo |
| Langue | Français (ou autre) |
| Pays | Tunisie (ou autre) |
| Cocher "Charger des données de démonstration" | Optionnel — utile pour voir des exemples préremplis |

Cliquez sur **Créer une base de données**. Odoo redémarre et vous connecte automatiquement en tant qu'admin.

### Étape 3 — Installer les applications utiles
Dans le menu Odoo (icône grille en haut à gauche) → **Apps** :
- Installez **CRM** (gratuit, Community) — pour synchroniser vos clients bancaires comme contacts.
- Installez **Facturation** *("Invoicing")* — version gratuite du module comptable en Community (la "Comptabilité" complète est réservée à Enterprise, mais Invoicing suffit pour ce projet).

### Étape 4 — Mettre à jour votre `.env`
Complétez les champs Odoo déjà présents dans `.env` avec les valeurs choisies à l'étape 2 :

```bash
ODOO_URL=http://odoo:8069
ODOO_DATABASE=bankportal
ODOO_USERNAME=admin@bankportail.local
ODOO_API_PASSWORD=le-mot-de-passe-que-vous-avez-choisi
```

> `ODOO_URL` reste `http://odoo:8069` (nom du service Docker), et non `localhost`, car c'est le **backend** (à l'intérieur du réseau Docker) qui doit joindre Odoo — pas votre navigateur.

### Étape 5 — Relancer le backend pour prendre en compte les nouvelles variables
```bash
docker compose up --build backend
```

### Étape 6 — Tester la connexion
```bash
curl http://localhost:5000/api/odoosync/test-connection
```
Réponse attendue :
```json
{"message":"Connexion à Odoo réussie","uid":2}
```
Si vous obtenez une erreur `Échec d'authentification Odoo`, vérifiez `ODOO_DATABASE` / `ODOO_USERNAME` / `ODOO_API_PASSWORD` dans `.env`, puis relancez le backend.

### Étape 7 — Tester la synchronisation d'un client
```bash
curl -X POST "http://localhost:5000/api/odoosync/sync-customer?name=Ahmed%20Ben%20Salah&email=ahmed.bensalah@example.com"
```
Réponse attendue :
```json
{"message":"Client créé dans Odoo","odooPartnerId":15}
```

Vérifiez dans Odoo : menu **CRM** ou **Contacts** → vous devriez voir "Ahmed Ben Salah" apparaître avec la note *"Client synchronisé automatiquement depuis Bank Portail"*.

Relancez la même commande une seconde fois : la réponse doit maintenant être `"Client déjà synchronisé dans Odoo"` (le code évite les doublons via une recherche par email avant création).

### Étape 8 — Pour aller plus loin (à faire vous-même ensuite)
- Appeler `SyncCustomer` automatiquement dans `AuthController` ou `AccountsController` quand un nouveau client est créé.
- Créer une facture (`account.move`) dans Odoo à chaque virement, via `OdooClient.CreateAsync("account.move", ...)` — nécessite de configurer un plan comptable minimal dans Odoo au préalable (menu Facturation → Configuration).
- Ajouter la synchronisation dans un `IHostedService` en arrière-plan plutôt que dans les contrôleurs directement, pour ne pas ralentir les réponses API si Odoo est lent.

---

## Récapitulatif des commandes clés

```bash
# Git
git init && git add . && git commit -m "..." && git branch -M main
git remote add origin https://github.com/<user>/Bank-portail.git
git push -u origin main

# Odoo
docker compose up --build odoo-db odoo
# -> ouvrir http://localhost:8069, créer la base "bankportal"
# -> compléter .env, puis :
docker compose up --build backend
curl http://localhost:5000/api/odoosync/test-connection
```
