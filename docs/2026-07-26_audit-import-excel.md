# Audit — Module d’import Excel produits

**Projet :** Pharmacie (ASP.NET Core 9 MVC)  
**Date :** 2026-07-26  
**Portée :** lecture seule du code (aucune modification)  
**Rôles concernés :** Administrateur, Pharmacien  
**Entrée menu :** Catalogue → Import produits  

---

## 1. Parcours utilisateur exact

| Étape | URL / action | Ce que voit l’utilisateur | Ce qu’il fait | Suite |
|------|----------------|---------------------------|---------------|--------|
| 1 | `GET /ProductImports/Upload` | Formulaire « Import produits » : rappel des colonnes (CIP, REFHA, LIBELLE, QTEFACT, PX_FAB, PPH), champ fichier `.xlsx`, boutons **Analyser le fichier** / Retour catalogue | Choisit un `.xlsx` et clique **Analyser** | POST Upload |
| 2 | `POST Upload` | (pas de page) : validation fichier, puis `PrepareImportAsync` (lecture Excel → validation → matching → persistance `ImportBatch` + lignes + anomalies) | Attend (pas de spinner dédié dans la vue) | Redirect `Preview?id={batchId}` |
| 3 | `GET /ProductImports/Preview/{id}` | KPI (total, créations, MAJ prix, lots, ignorées, anomalies…), bandeau d’alerte, éventuellement carte **Confirmer**, tableau paginé des lignes | Lit la synthèse ; s’il y a des anomalies bloquantes non résolues → lien **Résoudre les anomalies** | Anomalies ou Confirm |
| 4 | `GET /ProductImports/Anomalies/{id}` | Tableau de **toutes** les lignes avec anomalie **bloquante non résolue** : CIP, libellé, type(s), radios **Ignorer** / **Forcer l’import**, PPH de remplacement si besoin | Décide ligne par ligne, puis **Enregistrer les décisions** | POST Anomalies |
| 5 | `POST Anomalies` | Applique les décisions, recalcule l’action de chaque ligne | — | Si il reste des bloquantes → redirect Anomalies + warning ; sinon → Preview + success |
| 6 | Retour Preview | Bandeau « Prêt pour confirmation » + carte verte **Confirmer l'import** (si `CanConfirmImport`) | Clique **Confirmer l'import** | POST Confirm |
| 7 | `POST Confirm` | `ConfirmImportAsync` : crée produits/lots, met à jour prix, entre stock (expiration provisoire +2 ans) | — | Redirect Result ; si bloquantes restantes → Anomalies + erreur TempData |
| 8 | `GET /ProductImports/Result/{id}` | « Import confirmé », bilans (produits créés, lots, prix maj, ignorées…), alerte dates d’expiration à corriger | Peut aller Nouvel import ou Catalogue | Fin |

### Actions du contrôleur (`ProductImportsController`)

| Action | HTTP | Signature | Comportement |
|--------|------|-----------|--------------|
| Upload | GET | `Upload()` | Vue formulaire |
| Upload | POST | `Upload(ProductImportUploadViewModel model)` | Valide `.xlsx` → `PrepareImportAsync` → redirect Preview |
| Preview | GET | `Preview(int id, int page = 1)` | Synthèse + lignes paginées (50/page) |
| Confirm | POST | `Confirm(int id)` | `ConfirmImportAsync` → Result / Anomalies / Preview selon erreur |
| Result | GET | `Result(int id)` | Récapitulatif post-confirmation |
| Anomalies | GET | `Anomalies(int id)` | Formulaire de résolution des bloquantes |
| Anomalies | POST | `Anomalies(ProductImportAnomalyViewModel model)` | Enregistre Ignorer / Forcer |

### Service (`ProductImportService`)

- **`PrepareImportAsync`** : crée le lot, lit Excel, valide, matche, enregistre lignes + anomalies, retourne l’id du lot.
- **`GetPreviewSummaryAsync`** : compteurs par `ResolvedAction` + totaux anomalies bloquantes / avertissements.
- **`ConfirmImportAsync`** : refuse si déjà confirmé/annulé ou si bloquante non résolue ; sinon applique le catalogue/stock.

### Vues (`Views/ProductImports/`)

| Vue | Rôle UX |
|-----|---------|
| `Upload.cshtml` | Choix du fichier + consignes colonnes |
| `Preview.cshtml` | Synthèse KPI, lien anomalies, Confirmer, tableau paginé |
| `Anomalies.cshtml` | Décisions Ignorer / Forcer (+ PPH de remplacement) |
| `Result.cshtml` | Bilan après confirmation + alerte dates d’expiration |

---

## 2. Gestion des anomalies — ce qui se passe concrètement

### Détection

Pendant `PrepareImportAsync`, **avant** toute confirmation.

**Bloquantes typiques :**

- CIP manquant
- Libellé vide
- PPH absent / égal à 0
- CIP identique avec libellé différent

**Avertissements** (n’empêchent **pas** Confirmer) :

- PPH ≤ prix fab.
- Quantité négative
- CIP dupliqué dans le fichier

### Quand il y a des bloquantes

- **Preview** : KPI « Anomalies bloquantes », bandeau avec lien **Résoudre les anomalies**, **pas** de bouton Confirmer  
  (`CanConfirmImport` = statut `EnAttenteValidation` **et** `UnresolvedBlockingAnomaliesCount == 0`).
- **Page Anomalies** : **toutes** les lignes bloquantes non résolues sur **une seule page** (pas de pagination).

### Ce que l’utilisateur doit faire par ligne

- **Ignorer** (valeur par défaut dans le ViewModel) → ligne passée en `Ignoree`.
- **Forcer l'import** → recalcul de l’action (création / MAJ prix / nouveau lot) ; si anomalie PPH zéro, **PPH de remplacement > 0 obligatoire**.
- Un seul bouton **Enregistrer les décisions** pour tout le tableau.

### Cas « 48 anomalies »

- Pas 48 pages : **1 écran** avec 48 lignes + **1 clic** d’enregistrement.
- Mais **48 décisions** à poser / vérifier.
- Le défaut **Ignorer** s’applique si on ne change pas les radios → risque d’ignorer massivement sans le vouloir.
- S’il reste des bloquantes après save → reste sur Anomalies avec message du type « X traitée(s), il reste Y… ».

### Ce qui bloque Confirmer

Toute anomalie `Severity = Bloquante` avec `ResolvedByUser = false`.

### Confirmer avec anomalies non résolues

- **UI** : bouton masqué.
- **Si POST forcé** : `ProductImportUnresolvedAnomaliesException` → redirect Anomalies + TempData erreur claire (« Des anomalies bloquantes non résolues… »).

---

## 3. Page de prévisualisation

| Aspect | Détail |
|--------|--------|
| Synthèse | 8 KPI : total, créations, MAJ prix, lots, ignorées, lignes en anomalie, bloquantes, avertissements |
| Lignes | N°, CIP, libellé, qté, PX_FAB, PPH, badge d’action, id produit matché, compteurs anom. / bloqu. / avert. |
| Pagination | **50 lignes / page** (`PreviewPageSize = 50`) ; Précédent / Suivant uniquement |
| Filtres | **Aucun** (pas de filtre anomalie / action / CIP) |
| Bouton Confirmer | Carte verte **au-dessus** du tableau ; visible seulement si `CanConfirmImport` ; masquée si bloquantes ou lot déjà confirmé (lien vers Result dans ce cas) |

---

## 4. Points de friction UX identifiés

1. **Analyse** après upload : pas d’indicateur de progression ; gros fichier = attente opaque.
2. **Anomalies** : toutes sur une page (lourd si dizaines) ; décision par défaut **Ignorer** = piège.
3. **Pas d’actions de masse** (« Tout forcer », « Tout ignorer », « Forcer tous les PPH manquants »).
4. **Preview** : beaucoup de pages si ~2000+ lignes ; pas de filtre « anomalies seulement ».
5. **Produit matché** = `#id` technique, peu lisible.
6. **Vocabulaire métier** dense (ResolvedAction, PPH, REFHA, « forcer ») pour un utilisateur caisse/pharmacie.
7. **Avertissements** visibles en KPI mais non listés pour revue ciblée.
8. **Après confirm** : expiration provisoire +2 ans → travail post-import manuel (signalé, mais charge réelle).
9. **Pas d’annulation de lot** exposée dans ces 4 vues (statut `Annule` existe en modèle, pas d’UI ici).
10. **Confirmer** bien placé quand prêt, mais le parcours anomalies ↔ preview peut faire plusieurs allers-retours.

---

## 5. Propositions de simplification concrètes (sans refonte)

1. **Actions de masse sur Anomalies** : « Tout ignorer », « Tout forcer (sauf PPH à saisir) », et défaut radio **neutre / non sélectionné** avec validation « décision obligatoire » pour éviter l’ignore silencieux.
2. **Filtres Preview** : « Bloquantes uniquement », « Créations », recherche CIP/libellé — pour ne pas paginer 50× sur un fichier entier.
3. **UX upload** : overlay « Analyse en cours… » + message « Ne fermez pas la page » dès le submit.
4. **Résumé anomalies en tête de Preview** : top 3 types d’anomalies + bouton unique « Traiter les N anomalies » (déjà partiellement là ; renforcer le CTA).
5. **Forçage PPH groupé** : pour les lignes « PPH zéro », proposer un PPH par défaut (ex. = PX_FAB × coefficient, ou saisie unique appliquée aux lignes sélectionnées).

---

## Fichiers lus (référence)

- `Controllers/ProductImportsController.cs`
- `Services/ProductImportService.cs`
- `Services/ImportValidationService.cs` (règles d’anomalies)
- `Models/ProductImportPreviewViewModel.cs`
- `Models/ProductImportAnomalyViewModel.cs`
- `Models/ImportEnums.cs`
- `Views/ProductImports/Upload.cshtml`
- `Views/ProductImports/Preview.cshtml`
- `Views/ProductImports/Anomalies.cshtml`
- `Views/ProductImports/Result.cshtml`
- `Views/Shared/_Layout.cshtml` (lien menu)
